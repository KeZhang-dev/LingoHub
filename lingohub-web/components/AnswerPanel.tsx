"use client";

import { useState } from "react";
import type { RagAnswer } from "@/lib/api";
import AnswerText from "./AnswerText";

export type AskStatus = "idle" | "loading" | "done" | "error";

type Props = {
  status: AskStatus;
  question: string;
  answer: RagAnswer | null;
  error: string;
  suggestions: string[];
  // True while the current question is being answered from the LLM's general knowledge.
  generalKnowledge: boolean;
  onPickSuggestion: (question: string) => void;
  onRetry: () => void;
  // The learner approved answering from the LLM's general knowledge.
  onApproveGeneral: () => void;
};

export default function AnswerPanel({
  status,
  question,
  answer,
  error,
  suggestions,
  generalKnowledge,
  onPickSuggestion,
  onRetry,
  onApproveGeneral,
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
          <p className="text-xs text-muted">
            {generalKnowledge
              ? "Asking the AI to answer from its general knowledge…"
              : "Searching your materials and writing an answer…"}
          </p>
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

      {status === "done" && answer && <AnswerBody answer={answer} onApproveGeneral={onApproveGeneral} />}
    </section>
  );
}

function AnswerBody({ answer, onApproveGeneral }: { answer: RagAnswer; onApproveGeneral: () => void }) {
  // Ranks the answer actually cites, e.g. "[1]" and "[3]"; uncited sources are shown dimmer.
  const cited = new Set([...answer.answer.matchAll(/\[(\d+)\]/g)].map((m) => Number(m[1])));
  const seconds = (answer.embedMs + answer.searchMs + answer.generateMs) / 1000;
  const general = answer.answerSource === "general";
  // The learner said "No thanks" to a general-knowledge answer; a new question remounts this and resets it.
  const [declined, setDeclined] = useState(false);

  return (
    <div className="mt-4">
      {general && (
        <p className="mb-3 border-l-2 border-[#E36E59] pl-3 text-xs text-muted">
          Not from your learning materials — answered from the AI&apos;s general knowledge, so there are no sources to
          check it against. Double-check anything important.
        </p>
      )}

      <AnswerText text={answer.answer} />

      {answer.answerSource === "notFound" && !declined && (
        <div className="mt-4 rounded-lg border border-border px-4 py-3">
          <p className="text-sm">
            Your learning materials don&apos;t cover this. Do you want the AI to answer from its general knowledge
            instead?
          </p>
          <p className="mt-1 text-xs text-muted">
            That answer won&apos;t come from your materials and won&apos;t have sources to check it against.
          </p>
          <div className="mt-3 flex flex-wrap gap-2">
            <button
              type="button"
              onClick={onApproveGeneral}
              className="inline-flex h-8 items-center rounded-md bg-[#E36E59] px-3.5 text-sm font-medium text-white transition-colors hover:bg-[#CF5A46]"
            >
              Yes, ask the AI
            </button>
            <button
              type="button"
              onClick={() => setDeclined(true)}
              className="inline-flex h-8 items-center rounded-md border border-border px-3.5 text-sm transition-colors hover:bg-foreground/5"
            >
              No thanks
            </button>
          </div>
        </div>
      )}

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
        {general
          ? `Answered by ${answer.llmModel} · general knowledge · ${seconds.toFixed(1)} s`
          : `Answered by ${answer.llmModel} · retrieved with ${answer.embeddingModel} · ${seconds.toFixed(1)} s`}
      </p>
    </div>
  );
}
