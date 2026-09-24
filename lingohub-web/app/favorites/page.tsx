import type { Metadata } from "next";
import FavoritesView from "@/components/FavoritesView";
import PageHeader from "@/components/PageHeader";

export const metadata: Metadata = { title: "Favorites" };

export default function FavoritesPage() {
  return (
    <div>
      <div className="max-w-2xl">
        <PageHeader title="Favorites" description="Words you've saved to review later." />
      </div>
      <div className="mt-10">
        <FavoritesView />
      </div>
    </div>
  );
}
