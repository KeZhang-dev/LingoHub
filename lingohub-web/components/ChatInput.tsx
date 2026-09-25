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
        className="h-10 min-w-0 flex-1 bg-transparent text-[15px] outline-none placeholder:text-muted disabled:opacity-60"
      />
      <button
        type="submit"
        disabled={!canSubmit}
        aria-label={disabled ? "Waiting for the answer" : undefined}
        className="inline-flex h-10 w-[120px] shrink-0 items-center justify-center rounded-md bg-[#E36E59] text-[15px] font-medium text-white transition-colors hover:bg-[#CF5A46] disabled:cursor-not-allowed disabled:hover:bg-[#E36E59]"
      >
        {disabled ? (
          <span className="h-4.5 w-4.5 animate-spin rounded-full border-2 border-current border-r-transparent motion-reduce:animate-none" />
        ) : (
          "Search"
        )}
      </button>
    </form>
  );
}
