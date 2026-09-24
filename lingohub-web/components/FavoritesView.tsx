"use client";

import Link from "next/link";
import { useMemo } from "react";
import { useFavorites } from "@/lib/favorites";
import { vocabulary } from "@/lib/vocabulary";
import VocabularyGrid from "./VocabularyGrid";

// Saved words, in the same order as the vocabulary list.
export default function FavoritesView() {
  const { favorites } = useFavorites();
  const saved = useMemo(() => vocabulary.filter((item) => favorites.has(item.id)), [favorites]);

  return (
    <VocabularyGrid
      items={saved}
      emptyState={
        <div className="rounded-lg border border-border px-6 py-16 text-center">
          <p className="text-sm font-medium">No favorites yet.</p>
          <p className="mt-1 text-sm text-muted">Save words you want to review later.</p>
          <Link
            href="/"
            className="mt-6 inline-flex h-8 items-center rounded-md border border-border px-3.5 text-sm transition-colors hover:bg-foreground/5"
          >
            Browse vocabulary
          </Link>
        </div>
      }
    />
  );
}
