import type { Lesson } from "@/lib/lessons";

type Props = {
  lesson: Lesson;
  onNext: () => void;
};

// The homepage's primary element: one word or phrase, taught.
export default function LessonCard({ lesson, onNext }: Props) {
  const meta = [lesson.partOfSpeech, lesson.register].filter(Boolean).join(" · ");

  return (
    <article aria-labelledby="lesson-term" className="rounded-lg border border-border bg-surface">
      <div className="px-6 pt-6 pb-8 sm:px-10 sm:pt-8 sm:pb-10">
        <div className="flex items-center justify-between gap-4">
          <h2 className="text-xs font-medium text-muted">Today&apos;s lesson</h2>
          <span className="rounded-sm border border-border px-2 py-0.5 text-xs text-muted">
            {lesson.context}
          </span>
        </div>

        {/* key={term} re-runs the fade when the lesson changes. */}
        <div key={lesson.term} className="animate-fade-up">
          <p
            id="lesson-term"
            className="mt-8 text-[2.5rem] leading-none font-semibold tracking-[-0.025em] sm:text-5xl"
          >
            {lesson.term}
          </p>
          <p className="mt-3 text-sm text-muted">{meta}</p>

          <p className="mt-7 max-w-prose text-base leading-relaxed text-pretty sm:text-[17px]">
            {lesson.explanation}
          </p>

          <blockquote className="mt-6 border-l-2 border-accent pl-4 text-[15px] leading-relaxed text-muted">
            “{lesson.example}”
          </blockquote>
        </div>
      </div>

      <div className="flex justify-end border-t border-border px-6 py-3 sm:px-10">
        <button
          type="button"
          onClick={onNext}
          className="group inline-flex h-8 items-center gap-1.5 text-sm font-medium text-muted transition-colors hover:text-foreground"
        >
          Next lesson
          <svg
            className="h-3.5 w-3.5 transition-transform group-hover:translate-x-0.5 motion-reduce:transition-none"
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
      </div>
    </article>
  );
}
