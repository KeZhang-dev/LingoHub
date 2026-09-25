"use client";

import { useState } from "react";

type Props = {
  page: number; // 1-based
  pageCount: number;
  onChange: (page: number) => void;
};

// Which page numbers to show: always the first and last page, the current page and its
// neighbours, and a steady run of `run` pages near either end (5 on desktop, 3 on phones).
// Gaps become "…" — except a gap of exactly one page, which is shown as that number.
function pageItems(page: number, pageCount: number, run: number): (number | "gap")[] {
  const pages = new Set([1, pageCount, page - 1, page, page + 1]);
  if (page < run) for (let p = 1; p <= run; p++) pages.add(p);
  if (page > pageCount - run + 1) for (let p = pageCount - run + 1; p <= pageCount; p++) pages.add(p);

  const sorted = [...pages].filter((p) => p >= 1 && p <= pageCount).sort((a, b) => a - b);
  const items: (number | "gap")[] = [];
  let previous = 0;
  for (const p of sorted) {
    if (p - previous === 2) items.push(previous + 1);
    else if (p - previous > 2) items.push("gap");
    items.push(p);
    previous = p;
  }
  return items;
}

const baseButton =
  "inline-flex h-8 min-w-8 items-center justify-center gap-1.5 rounded-md px-2 text-sm tabular-nums transition-colors";

export default function Pagination({ page, pageCount, onChange }: Props) {
  const [jump, setJump] = useState("");

  function renderPages(items: (number | "gap")[], visibility: string) {
    return items.map((item, i) =>
      item === "gap" ? (
        <li key={`${visibility}-gap-${i}`} aria-hidden className={`${visibility} w-6 text-center text-sm text-muted`}>
          …
        </li>
      ) : (
        <li key={`${visibility}-${item}`} className={visibility}>
          <button
            type="button"
            onClick={() => onChange(item)}
            aria-label={`Page ${item}`}
            aria-current={item === page ? "page" : undefined}
            className={`${baseButton} ${
              item === page
                ? "border border-border bg-surface font-medium text-foreground"
                : "text-muted hover:bg-foreground/5 hover:text-foreground"
            }`}
          >
            {item}
          </button>
        </li>
      ),
    );
  }

  function submitJump(e: React.FormEvent) {
    e.preventDefault();
    const target = Number(jump);
    if (Number.isInteger(target) && target >= 1 && target <= pageCount) onChange(target);
    setJump("");
  }

  return (
    <nav aria-label="Pagination" className="flex flex-wrap items-center justify-between gap-x-6 gap-y-4">
      <ul className="flex flex-wrap items-center gap-1">
        <li>
          <button
            type="button"
            onClick={() => onChange(page - 1)}
            disabled={page <= 1}
            aria-label="Previous page"
            className={`${baseButton} text-muted hover:bg-foreground/5 hover:text-foreground disabled:pointer-events-none disabled:opacity-40`}
          >
            <svg className="h-3.5 w-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
              <path d="M19 12H5M11 18l-6-6 6-6" />
            </svg>
            <span className="hidden sm:inline">Previous</span>
          </button>
        </li>

        {/* Two versions of the number row: a shorter one for phones, the full one from `sm` up. */}
        {renderPages(pageItems(page, pageCount, 3), "sm:hidden")}
        {renderPages(pageItems(page, pageCount, 5), "hidden sm:block")}

        <li>
          <button
            type="button"
            onClick={() => onChange(page + 1)}
            disabled={page >= pageCount}
            aria-label="Next page"
            className={`${baseButton} text-muted hover:bg-foreground/5 hover:text-foreground disabled:pointer-events-none disabled:opacity-40`}
          >
            <span className="hidden sm:inline">Next</span>
            <svg className="h-3.5 w-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
              <path d="M5 12h14M13 6l6 6-6 6" />
            </svg>
          </button>
        </li>
      </ul>

      {/* Jump straight to any page, e.g. 89 of 150. */}
      <form onSubmit={submitJump} className="flex items-center gap-2 text-sm text-muted">
        <label htmlFor="page-jump">Go to page</label>
        <input
          id="page-jump"
          type="number"
          inputMode="numeric"
          min={1}
          max={pageCount}
          value={jump}
          onChange={(e) => setJump(e.target.value)}
          placeholder={String(page)}
          className="h-8 w-16 rounded-md border border-border bg-surface px-2 text-center text-foreground tabular-nums outline-none transition-colors [appearance:textfield] focus:border-muted [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none"
        />
        <span className="tabular-nums">of {pageCount}</span>
      </form>
    </nav>
  );
}
