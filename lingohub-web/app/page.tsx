"use client";

import { useState } from "react";
import AnswerPanel, { type AskStatus } from "@/components/AnswerPanel";
import ChatInput from "@/components/ChatInput";
import VocabularyGrid from "@/components/VocabularyGrid";
import { askQuestion, type RagAnswer } from "@/lib/api";
import { vocabulary } from "@/lib/vocabulary";

export default function HomePage() {
  // Supporting: ask a free-form question via the existing RAG endpoint, POST /api/ask.
  const [input, setInput] = useState("");
  const [question, setQuestion] = useState("");
  const [answer, setAnswer] = useState<RagAnswer | null>(null);
  const [error, setError] = useState("");
  const [status, setStatus] = useState<AskStatus>("idle");

  async function ask(q: string) {
    if (!q) return;
    setQuestion(q);
    setAnswer(null);
    setError("");
    setStatus("loading");
    try {
      setAnswer(await askQuestion(q));
      setStatus("done");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Something went wrong. Please try again.");
      setStatus("error");
    }
  }

  return (
    <div>
      <header className="max-w-2xl">
        <h1 className="text-[2rem] leading-[1.15] font-semibold tracking-[-0.02em] text-balance sm:text-[2.5rem]">
          Learn Australian &amp; New Zealand English
        </h1>
        <p className="mt-4 text-base leading-relaxed text-pretty text-muted sm:text-[17px]">
          {vocabulary.length.toLocaleString()} everyday and workplace words and phrases, each with a
          real example sentence and its Chinese translation.
        </p>
      </header>

      <section aria-label="Vocabulary" className="mt-12">
        <VocabularyGrid items={vocabulary} />
      </section>

      <section aria-labelledby="ask-heading" className="mt-20 max-w-2xl border-t border-border pt-10">
        <h2 id="ask-heading" className="text-sm font-medium">
          Have a question about a word?
        </h2>
        <p className="mt-1 text-sm text-muted">Answers come from your learning materials, with sources.</p>
        <div className="mt-4">
          <ChatInput
            value={input}
            onChange={setInput}
            onSubmit={() => ask(input.trim())}
            disabled={status === "loading"}
            placeholder="e.g. What does “knock off” mean?"
            label="Ask a question about a word"
          />
        </div>
        <div className="mt-6">
          <AnswerPanel
            status={status}
            question={question}
            answer={answer}
            error={error}
            suggestions={[]}
            onPickSuggestion={setInput}
            onRetry={() => ask(question)}
          />
        </div>
      </section>
    </div>
  );
}
