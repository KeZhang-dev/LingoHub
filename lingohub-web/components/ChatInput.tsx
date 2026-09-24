"use client";

type Props = {
  value: string;
  onChange: (value: string) => void;
  onSubmit: () => void;
  disabled?: boolean;
  placeholder?: string;
  label?: string;
};

// Compact single-line question field with an inline send button.
export default function ChatInput({
  value,
  onChange,
  onSubmit,
  disabled,
  placeholder = "Ask a question…",
  label = "Your question",
}: Props) {
  const canSubmit = value.trim().length > 0 && !disabled;

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        if (canSubmit) onSubmit();
      }}
      className="flex items-center gap-2 rounded-lg border border-border bg-surface py-1.5 pr-1.5 pl-4 transition-colors focus-within:border-muted"
    >
      <input
        type="text"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        aria-label={label}
        disabled={disabled}
        className="h-8 min-w-0 flex-1 bg-transparent text-[15px] outline-none placeholder:text-muted disabled:opacity-60"
      />
      <button
        type="submit"
        disabled={!canSubmit}
        aria-label={disabled ? "Waiting for the answer" : "Ask"}
        className="inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-md bg-[#E36E59] text-white transition-colors hover:bg-[#CF5A46] disabled:cursor-not-allowed disabled:opacity-40"
      >
        {disabled ? (
          <span className="h-3.5 w-3.5 animate-spin rounded-full border-2 border-current border-r-transparent motion-reduce:animate-none" />
        ) : (
          <svg
            className="h-3.5 w-3.5"
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
        )}
      </button>
    </form>
  );
}
