import type { VocabularyItem } from "@/lib/vocabulary";
import FavoriteButton from "./FavoriteButton";

type Props = {
  item: VocabularyItem;
  favorite: boolean;
  onToggleFavorite: () => void;
};

// One word, compact enough for a 4-column grid. All text comes straight from the source data.
export default function VocabularyCard({ item, favorite, onToggleFavorite }: Props) {
  return (
    <article className="flex h-full flex-col rounded-lg border border-border bg-surface p-4">
      <div className="flex items-start justify-between gap-2">
        <h3 className="text-[17px] leading-snug font-semibold tracking-tight">{item.term}</h3>
        <FavoriteButton active={favorite} label={item.term} onToggle={onToggleFavorite} />
      </div>
      <p className="text-xs text-muted">{item.partOfSpeech}</p>

      {/* The source has a Chinese meaning, not an English definition. */}
      <p lang="zh" className="mt-3 text-sm">
        {item.meaningZh}
      </p>

      <div className="mt-auto pt-3">
        <div className="border-t border-border pt-3 text-[13px] leading-relaxed">
          <p>“{item.example}”</p>
          <p lang="zh" className="mt-1 text-muted">
            {item.exampleZh}
          </p>
        </div>
      </div>
    </article>
  );
}
