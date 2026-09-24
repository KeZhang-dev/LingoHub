import type { RagAnswer } from "@/lib/api";
import AnswerText from "./AnswerText";

export type AskStatus = "idle" | "loading" | "done" | "error";

type Props = {
  status: AskStatus;
  question: string;
  answer: RagAnswer | null;
  error: string;
  suggestions: string[];
  onPickSuggestion: (question: string) => void;
  onRetry: () => void;
};

export default function AnswerPanel({
  status,
  question,
  answer,
  error,
  suggestions,
  onPickSuggestion,
  onRetry,
}: Props) {
  if (status === "idle") {
    if (suggestions.length === 0) return null;
    return (
      <div>
        <h2 className="text-xs font-medium text-muted">Try asking</h2>
        {/* A quiet list: thin dividers, muted text, an arrow that appears on hover/focus. */}
        <ul className="mt-3 divide-y divide-border border-y border-border">
          {suggestions.map((s) => (
            <li key={s}>
              <button
                type="button"
                onClick={() => onPickSuggestion(s)}
                className="group flex w-full items-center justify-between gap-4 py-3 text-left text-sm text-muted transition-colors hover:text-foreground focus-visible:text-foreground"
              >
                <span>{s}</span>
                <svg
                  className="h-3.5 w-3.5 shrink-0 -translate-x-1 opacity-0 transition group-hover:translate-x-0 group-hover:opacity-100 group-focus-visible:translate-x-0 group-focus-visible:opacity-100 motion-reduce:transition-none"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  aria-hidden
                >
                  <path d="M5 12h14M13 6l6 6-6 6" />
                </svg>
              </button>
            </li>
          ))}
        </ul>
      </div>
    );
  }

  return (
    <section aria-live="polite" aria-busy={status === "loading"} className="animate-fade-up">
      <p className="text-sm text-muted">{question}</p>

      {status === "loading" && (
        <div className="mt-4" aria-label="Loading answer">
          <p className="text-xs text-muted">Searching your materials and writing an answer…</p>
          <div className="mt-3 space-y-2.5">
            <div className="h-3 w-full animate-pulse rounded-sm bg-border" />
            <div className="h-3 w-11/12 animate-pulse rounded-sm bg-border" />
            <div className="h-3 w-2/3 animate-pulse rounded-sm bg-border" />
          </div>
        </div>
      )}

      {status === "error" && (
        <div className="mt-4 flex flex-wrap items-center justify-between gap-3 rounded-lg border border-border px-4 py-3">
          <p role="alert" className="text-sm text-red-600 dark:text-red-400">
            {error}
          </p>
          <button
            type="button"
            onClick={onRetry}
            className="inline-flex h-8 items-center rounded-md border border-border px-3.5 text-sm transition-colors hover:bg-foreground/5"
          >
            Try again
          </button>
        </div>
      )}

      {status === "done" && answer && <AnswerBody answer={answer} />}
    </section>
  );
}

function AnswerBody({ answer }: { answer: RagAnswer }) {
  // Ranks the answer actually cites, e.g. "[1]" and "[3]"; uncited sources are shown dimmer.
  const cited = new Set([...answer.answer.matchAll(/\[(\d+)\]/g)].map((m) => Number(m[1])));
  const seconds = (answer.embedMs + answer.searchMs + answer.generateMs) / 1000;

  return (
    <div className="mt-4">
      <AnswerText text={answer.answer} />

      {answer.sources.length > 0 && (
        <div className="mt-8 border-t border-border pt-4">
          <h2 className="text-xs font-medium text-muted">Sources</h2>
          <ol className="mt-1 divide-y divide-border">
            {answer.sources.map((s) => (
              <li key={s.chunkId} id={`source-${s.rank}`} className="scroll-mt-24">
                <details className="group">
                  <summary
                    className={`flex cursor-pointer list-none items-center gap-3 py-2.5 text-sm transition-colors hover:text-foreground [&::-webkit-details-marker]:hidden ${
                      cited.has(s.rank) ? "text-foreground" : "text-muted"
                    }`}
                  >
                    <span className="w-6 shrink-0 tabular-nums">[{s.rank}]</span>
                    <span className="min-w-0 flex-1 truncate">
                      {s.fileName} · chunk {s.chunkIndex}
                    </span>
                    <span className="shrink-0 text-xs text-muted tabular-nums">
                      similarity {s.similarity.toFixed(2)}
                    </span>
                    <svg
                      className="h-3.5 w-3.5 shrink-0 text-muted transition-transform group-open:rotate-90 motion-reduce:transition-none"
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
                  <p className="pb-3 pl-9 text-sm leading-relaxed whitespace-pre-line text-muted">
                    {s.content}
                  </p>
                </details>
              </li>
            ))}
          </ol>
        </div>
      )}

      <p className="mt-4 text-xs text-muted">
        Answered by {answer.llmModel} · retrieved with {answer.embeddingModel} · {seconds.toFixed(1)} s
      </p>
    </div>
  );
}
