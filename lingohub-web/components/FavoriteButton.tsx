type Props = {
  active: boolean;
  label: string; // the word, for the accessible name
  onToggle: () => void;
};

// Small star toggle. Filled when saved.
export default function FavoriteButton({ active, label, onToggle }: Props) {
  return (
    <button
      type="button"
      onClick={onToggle}
      aria-pressed={active}
      aria-label={active ? `Remove “${label}” from favorites` : `Save “${label}” to favorites`}
      title={active ? "Remove from favorites" : "Save to favorites"}
      className={`-mt-1 -mr-1.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-md transition-colors hover:bg-foreground/5 ${
        active ? "text-accent" : "text-muted hover:text-foreground"
      }`}
    >
      <svg
        className="h-4 w-4"
        viewBox="0 0 24 24"
        fill={active ? "currentColor" : "none"}
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinejoin="round"
        aria-hidden
      >
        <path d="M12 3.5l2.6 5.3 5.9.9-4.25 4.1 1 5.85L12 16.9l-5.25 2.75 1-5.85L3.5 9.7l5.9-.9L12 3.5z" />
      </svg>
    </button>
  );
}
