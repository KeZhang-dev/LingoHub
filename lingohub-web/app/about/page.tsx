import type { Metadata } from "next";
import PageHeader from "@/components/PageHeader";

export const metadata: Metadata = { title: "About" };

type Step = { title: string; detail: string };

// What happens to a document once it's added (DocumentService on the backend).
const ingestSteps: Step[] = [
  { title: "Upload PDF", detail: "POST /api/documents" },
  { title: "Extract text", detail: "PdfPig reads each page" },
  { title: "Clean", detail: "Drop page numbers, fix spacing and odd characters" },
  { title: "Chunk", detail: "~800 characters, 150 overlap" },
  { title: "Embed", detail: "Voyage voyage-4 turns each chunk into 1024 numbers" },
  { title: "Store", detail: "PostgreSQL + pgvector" },
];

// What happens when a learner asks a question (RagService on the backend).
const askSteps: Step[] = [
  { title: "Question", detail: "Typed into the search box" },
  { title: "Embed question", detail: "Same model as the chunks" },
  { title: "Vector search", detail: "Cosine distance, top 5 chunks" },
  { title: "Build prompt", detail: "Rules + 5 passages + question" },
  { title: "Generate", detail: "Gemini writes the answer" },
  { title: "Answer", detail: "With [1]-style citations and sources" },
];

type Faq = { question: string; answer: string[] };

const faqs: Faq[] = [
  {
    question: "A learner asks something that isn't in the documents. What happens, and why?",
    answer: [
      "Two layers. First, the prompt tells Gemini to answer only from the retrieved passages and, if they don't contain the answer, to start with one fixed sentence: “The provided document does not contain enough information to answer this question.” It must never guess.",
      "Second, the backend checks for that sentence and marks the answer as “not found”. The page then asks the learner whether they want an answer from the AI's general knowledge. Only after they say yes does the app send a second request. That answer skips retrieval, has no sources and is clearly labelled as not coming from the materials.",
      "Why: a grounded answer is the whole point of the app. A learner can't easily tell a plausible invented definition from a real one, especially for slang. A fixed sentence is something code can check reliably, unlike free text. Asking first keeps the learner in control, and the extra LLM call only happens when they want it.",
      "Known gap: retrieval always returns the top 5 chunks, even when none are close, so today the LLM decides whether they're relevant. The next step is a similarity threshold, tuned on an evaluation set, that skips the LLM call when nothing is close enough.",
    ],
  },
  {
    question: "Why 800 characters with a 150-character overlap? Where do those numbers come from?",
    answer: [
      "From the shape of the document. Each vocabulary entry (word, Chinese meaning, part of speech, example sentence, translation) is about 70 characters. So 800 characters holds about 11 entries; the stored chunks average 785 characters.",
      "Why that size: the PDF groups words by topic (praise, apologies, meetings…), so a chunk of about 11 entries stays on one theme. That keeps its embedding focused, while still giving the model neighbouring words for context. Top 5 chunks is about 4,000 characters, or about 55 entries, per question, which keeps the prompt small and cheap.",
      "Why that overlap: the chunker splits at paragraph, then line, then sentence boundaries, so entries are rarely cut. The 150-character overlap (about two entries) guarantees that an entry near a boundary still appears whole in at least one chunk. It's about 19% overlap, within the usual 10–20% guidance. The extra embedding cost is negligible at this size.",
      "To be honest, it's a reasoned starting point, not a tuned optimum. The right way to confirm it is to compare, say, 400 / 800 / 1200 on an evaluation set (see the next question).",
    ],
  },
  {
    question: "Retrieval doesn't always find the right answer. How do you evaluate whether the RAG is good?",
    answer: [
      "Evaluate the two halves separately, because they fail differently.",
      "Build a small test set of about 50 questions, each with the entry that should answer it. Mix exact words (“arvo”), paraphrases (“what do Kiwis call the afternoon?”), questions in Chinese, and questions that are deliberately out of scope.",
      "Retrieval: hit rate@5 (is the right chunk in the top 5?) and MRR (how high does it rank?). This isolates embedding, chunking and topK choices from the LLM.",
      "Generation: faithfulness (is every claim supported by the passage it cites?), correctness, citation accuracy, and refusals: does it say “not found” for out-of-scope questions without refusing in-scope ones? Review a small set by hand; use an LLM-as-judge to scale, and spot-check it.",
      "Run the set whenever the chunk size, model, prompt or topK changes, like a regression test. In production, log the signals the API already returns (similarity scores, the not-found rate, tokens, latency) plus how often learners ask for general-knowledge answers. Today, checking is manual through the Sources panel; the test set is the next step.",
    ],
  },
  {
    question: "Why embeddings and vector search instead of plain SQL or keyword search?",
    answer: [
      "Learners ask by meaning, not by the exact word. “What do Kiwis call the afternoon?” never contains “arvo”, so LIKE or keyword search finds nothing. An embedding places text with similar meaning close together, so the question lands near the right entry. It also works across languages: a question in Chinese still matches English entries.",
      "pgvector keeps the vectors in the same PostgreSQL as everything else. That means one database, the same transactions as the documents and chunks, and normal SQL filters alongside the distance (we only compare vectors made by the same embedding model).",
      "Trade-offs: keyword search is cheaper, easier to explain, and better at exact rare tokens such as names or codes. Embeddings cost one API call per question and can occasionally rank an exact match lower than a looser one.",
      "The strongest option is hybrid search: PostgreSQL full-text search plus vector search, with the two result lists merged (for example with reciprocal rank fusion). That's a natural next step.",
    ],
  },
  {
    question: "What if the project grows from 325 chunks to 3 million? Does the architecture still work?",
    answer: [
      "The design holds (extract → chunk → embed → pgvector → LLM), but several parts need to change.",
      "Search: today there's no vector index, so every question computes its distance to every chunk. That's instant for 325 but not for 3 million: 3M × 1024 floats is about 12 GB of vectors to scan. The fix is an HNSW index in pgvector (approximate nearest neighbour), which brings queries back to milliseconds. Tune its recall/speed settings, and plan the memory: half-precision vectors (halfvec), or fewer dimensions if the model supports it, cut the size roughly in half or more.",
      "Ingestion: uploads currently chunk and embed during the HTTP request. At 3 million chunks (about 23,000 embedding calls at 128 per batch), that has to move to a background job queue with rate-limit handling and resumable progress. The existing “embed only missing chunks” endpoint is already a good basis for that.",
      "Quality: with many more documents, the top 5 can fill up with near-duplicates. Add metadata filters (per user or per document), hybrid search, and a reranking step after retrieval.",
      "Operations: connection pooling, read replicas, and partitioning by tenant if it's multi-user. 3 million vectors is comfortably within what pgvector handles with HNSW and enough RAM. A dedicated vector database only becomes worth it at much larger scale or with very high query rates.",
    ],
  },
];

function Flow({ label, steps }: { label: string; steps: Step[] }) {
  return (
    <div>
      <h3 className="text-sm font-medium">{label}</h3>
      <ol className="mt-3 flex flex-col items-stretch lg:flex-row">
        {steps.map((step, i) => (
          <li key={step.title} className="flex flex-col items-stretch lg:flex-1 lg:flex-row">
            <div className="flex-1 rounded-lg border border-border bg-surface px-3 py-2.5">
              <p className="text-xs text-muted tabular-nums">{i + 1}</p>
              <p className="text-sm font-medium">{step.title}</p>
              <p className="mt-0.5 text-xs leading-snug text-muted">{step.detail}</p>
            </div>
            {i < steps.length - 1 && (
              <span aria-hidden className="flex items-center justify-center py-1 text-muted lg:px-1.5 lg:py-0">
                <svg
                  className="h-3.5 w-3.5 rotate-90 lg:rotate-0"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                >
                  <path d="M5 12h14M13 6l6 6-6 6" />
                </svg>
              </span>
            )}
          </li>
        ))}
      </ol>
    </div>
  );
}

export default function AboutPage() {
  return (
    <div>
      <div className="max-w-2xl">
        <PageHeader
          title="About LingoHub"
          description="LingoHub helps you learn the English people actually use in Australia and New Zealand — the everyday phrases, local expressions and workplace language that textbooks tend to miss."
        />
      </div>

      <section aria-labelledby="how-heading" className="mt-12">
        <h2 id="how-heading" className="text-xs font-medium text-muted">
          How it works
        </h2>
        <p className="mt-3 max-w-2xl text-[15px] leading-relaxed">
          LingoHub is a RAG (retrieval-augmented generation) app. Instead of letting an AI answer from memory, it first
          finds the passages in your learning materials that match your question, then asks the AI to answer using
          only those passages — and shows you which ones it used.
        </p>

        <div className="mt-6 space-y-8">
          <Flow label="When a document is added" steps={ingestSteps} />
          <Flow label="When you ask a question" steps={askSteps} />
        </div>

        <p className="mt-4 max-w-2xl text-sm leading-relaxed text-muted">
          The two flows meet in the database: the chunks stored in step 6 of the first flow are what the vector search
          looks through in the second. If none of the passages answer the question, LingoHub says so and asks before
          answering from the AI&apos;s general knowledge.
        </p>
      </section>

      <section aria-labelledby="faq-heading" className="mt-12 max-w-2xl">
        <h2 id="faq-heading" className="text-xs font-medium text-muted">
          Design questions
        </h2>
        <ul className="mt-3 divide-y divide-border border-y border-border">
          {faqs.map((faq) => (
            <li key={faq.question}>
              <details className="group">
                <summary className="flex cursor-pointer list-none items-start justify-between gap-4 py-3.5 text-[15px] font-medium [&::-webkit-details-marker]:hidden">
                  <span>{faq.question}</span>
                  <svg
                    className="mt-1 h-3.5 w-3.5 shrink-0 text-muted transition-transform group-open:rotate-90 motion-reduce:transition-none"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    aria-hidden
                  >
                    <path d="M9 6l6 6-6 6" />
                  </svg>
                </summary>
                <div className="space-y-3 pb-4 text-[15px] leading-relaxed text-muted">
                  {faq.answer.map((paragraph) => (
                    <p key={paragraph}>{paragraph}</p>
                  ))}
                </div>
              </details>
            </li>
          ))}
        </ul>
      </section>

      <section className="mt-12">
        <h2 className="text-xs font-medium text-muted">Contact</h2>
        <p className="mt-3 text-[15px]">
          Questions or feedback?{" "}
          <a
            href="mailto:hello@lingohub.example"
            className="underline decoration-border underline-offset-4 transition-colors hover:decoration-foreground"
          >
            hello@lingohub.example
          </a>
        </p>
      </section>
    </div>
  );
}
