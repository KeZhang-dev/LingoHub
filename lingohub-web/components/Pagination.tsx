type Props = {
  page: number; // 1-based
  pageCount: number;
  onChange: (page: number) => void;
};

const buttonClass =
  "inline-flex h-8 items-center gap-1.5 rounded-md border border-border px-3 text-sm transition-colors hover:bg-foreground/5 disabled:pointer-events-none disabled:opacity-40";

export default function Pagination({ page, pageCount, onChange }: Props) {
  return (
    <nav aria-label="Pagination" className="flex items-center justify-between gap-4">
      <button type="button" onClick={() => onChange(page - 1)} disabled={page <= 1} className={buttonClass}>
        <svg className="h-3.5 w-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
          <path d="M19 12H5M11 18l-6-6 6-6" />
        </svg>
        Previous
      </button>
      <p className="text-sm text-muted tabular-nums" aria-live="polite">
        <span className="sr-only">Page </span>
        {page} / {pageCount}
      </p>
      <button type="button" onClick={() => onChange(page + 1)} disabled={page >= pageCount} className={buttonClass}>
        Next
        <svg className="h-3.5 w-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
          <path d="M5 12h14M13 6l6 6-6 6" />
        </svg>
      </button>
    </nav>
  );
}
