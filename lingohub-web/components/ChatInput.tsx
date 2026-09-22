"use client";

type Props = {
  value: string;
  onChange: (value: string) => void;
  onSubmit: () => void;
  disabled?: boolean;
};

export default function ChatInput({ value, onChange, onSubmit, disabled }: Props) {
  const canSubmit = value.trim().length > 0 && !disabled;

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        if (canSubmit) onSubmit();
      }}
      className="rounded-2xl border border-border bg-surface p-3 shadow-sm transition-colors focus-within:border-accent"
    >
      <textarea
        value={value}
        onChange={(e) => onChange(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === "Enter" && !e.shiftKey && !e.nativeEvent.isComposing) {
            e.preventDefault();
            if (canSubmit) onSubmit();
          }
        }}
        rows={4}
        placeholder="Ask anything about your English learning materials…"
        aria-label="Your question"
        className="w-full resize-none bg-transparent px-2 py-1 text-base outline-none placeholder:text-muted"
      />
      <div className="flex items-center justify-between pt-2">
        <span className="hidden px-2 text-xs text-muted sm:block">
          Enter to send · Shift+Enter for a new line
        </span>
        <button
          type="submit"
          disabled={!canSubmit}
          className="ml-auto rounded-lg bg-accent px-5 py-2 text-sm font-medium text-background transition-colors hover:bg-accent-hover disabled:cursor-not-allowed disabled:opacity-40"
        >
          {disabled ? "Thinking…" : "Ask"}
        </button>
      </div>
    </form>
  );
}
