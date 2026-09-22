import type { Answer } from "@/lib/mockAnswer";

type Props = {
  status: "idle" | "loading" | "done";
  question: string;
  answer: Answer | null;
  suggestions: string[];
  onPickSuggestion: (question: string) => void;
};

export default function AnswerPanel({
  status,
  question,
  answer,
  suggestions,
  onPickSuggestion,
}: Props) {
  if (status === "idle") {
    return (
      <div className="pt-2">
        <p className="text-sm text-muted">
          Ask a question about the English learning materials you&apos;ve uploaded.
          Try one of these:
        </p>
        <ul className="mt-3 flex flex-col items-start gap-2">
          {suggestions.map((s) => (
            <li key={s}>
              <button
                type="button"
                onClick={() => onPickSuggestion(s)}
                className="rounded-full border border-border px-4 py-1.5 text-left text-sm text-muted transition-colors hover:border-accent hover:text-foreground"
              >
                {s}
              </button>
            </li>
          ))}
        </ul>
      </div>
    );
  }

  return (
    <section aria-live="polite" className="animate-fade-up">
      <p className="text-sm text-muted">{question}</p>
      {status === "loading" || !answer ? (
        <div className="mt-4 space-y-2.5" aria-label="Loading answer">
          <div className="h-3.5 w-full animate-pulse rounded bg-border" />
          <div className="h-3.5 w-11/12 animate-pulse rounded bg-border" />
          <div className="h-3.5 w-2/3 animate-pulse rounded bg-border" />
        </div>
      ) : (
        <div className="mt-4">
          <p className="leading-relaxed">{answer.text}</p>
          {answer.sources.length > 0 && (
            <div className="mt-5 border-t border-border pt-4">
              <h2 className="text-xs font-medium uppercase tracking-wide text-muted">
                Sources
              </h2>
              <ul className="mt-2 space-y-1 text-sm text-muted">
                {answer.sources.map((s) => (
                  <li key={`${s.title}-${s.page}`}>
                    {s.title} · p. {s.page}
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>
      )}
    </section>
  );
}
