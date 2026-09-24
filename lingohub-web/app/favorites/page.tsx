import type { Metadata } from "next";
import Link from "next/link";
import PageHeader from "@/components/PageHeader";

export const metadata: Metadata = { title: "Favorites" };

export default function FavoritesPage() {
  return (
    <div className="mx-auto max-w-2xl">
      <PageHeader
        title="Favorites"
        description="Words, phrases and answers you save will appear here for quick review."
      />

      {/* Empty state: saving answers isn't built yet, so say so plainly. */}
      <div className="mt-10 rounded-lg border border-border px-6 py-14 text-center">
        <p className="text-sm font-medium">No favorites yet</p>
        <p className="mt-1 text-sm text-muted">Saving answers is coming soon.</p>
        <Link
          href="/"
          className="mt-6 inline-flex h-8 items-center rounded-md border border-border px-3.5 text-sm transition-colors hover:bg-foreground/5"
        >
          Ask a question
        </Link>
      </div>
    </div>
  );
}
