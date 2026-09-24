"use client";

import { useRef, useState, type ReactNode } from "react";
import { useFavorites } from "@/lib/favorites";
import type { VocabularyItem } from "@/lib/vocabulary";
import Pagination from "./Pagination";
import VocabularyCard from "./VocabularyCard";

const PAGE_SIZE = 20; // 4 columns × 5 rows on desktop

type Props = {
  items: VocabularyItem[];
  emptyState?: ReactNode;
};

// Paginated card grid with favorites. Used by both Home (all words) and Favorites (saved words).
export default function VocabularyGrid({ items, emptyState }: Props) {
  const { favorites, toggleFavorite } = useFavorites();
  const [page, setPage] = useState(1);
  const topRef = useRef<HTMLDivElement>(null);

  const pageCount = Math.max(1, Math.ceil(items.length / PAGE_SIZE));
  // Clamp: on Favorites, removing the last word on the last page shrinks pageCount.
  const current = Math.min(page, pageCount);
  const start = (current - 1) * PAGE_SIZE;
  const visible = items.slice(start, start + PAGE_SIZE);

  function goTo(next: number) {
    setPage(Math.min(Math.max(1, next), pageCount));
    topRef.current?.scrollIntoView({ block: "start" });
  }

  if (items.length === 0) return <>{emptyState}</>;

  return (
    <div ref={topRef} className="scroll-mt-24">
      <p className="text-xs text-muted tabular-nums">
        {start + 1}–{start + visible.length} of {items.length.toLocaleString()}{" "}
        {items.length === 1 ? "word" : "words"}
      </p>

      <ul className="mt-3 grid grid-cols-1 gap-3 min-[480px]:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 lg:gap-4">
        {visible.map((item) => (
          <li key={item.id}>
            <VocabularyCard
              item={item}
              favorite={favorites.has(item.id)}
              onToggleFavorite={() => toggleFavorite(item.id)}
            />
          </li>
        ))}
      </ul>

      {pageCount > 1 && (
        <div className="mt-8">
          <Pagination page={current} pageCount={pageCount} onChange={goTo} />
        </div>
      )}
    </div>
  );
}
