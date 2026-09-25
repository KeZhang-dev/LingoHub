type Props = {
  onClick: () => void;
};

// "换一批": show a new random set of words.
export default function ShuffleButton({ onClick }: Props) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="group inline-flex h-8 items-center gap-1.5 rounded-md border border-border px-3 text-sm text-muted transition-colors hover:bg-foreground/5 hover:text-foreground"
    >
      <svg
        className="h-3.5 w-3.5 transition-transform duration-300 group-active:rotate-180 motion-reduce:transition-none"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
        aria-hidden
      >
        <path d="M16 3h5v5M4 20 21 3M21 16v5h-5M15 15l6 6M4 4l5 5" />
      </svg>
      Shuffle
    </button>
  );
}
