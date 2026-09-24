import { Fragment, type ReactNode } from "react";

// Renders the small Markdown subset the LLM uses — paragraphs, "* "/"- " bullets, **bold**, *italic* —
// and turns [1]-style citations into links to the matching source. Builds React elements; never injects HTML.

const BULLET = /^[*-]\s+/;
const INLINE = /(\*\*[^*]+\*\*|\*[^*\s][^*]*\*|\[\d+\])/g;

function renderInline(text: string, key: string): ReactNode[] {
  return text
    .split(INLINE)
    .filter(Boolean)
    .map((part, i) => {
      const k = `${key}-${i}`;
      const cite = part.match(/^\[(\d+)\]$/);
      if (cite)
        return (
          <sup key={k} className="ml-0.5">
            <a
              href={`#source-${cite[1]}`}
              className="text-[0.7em] font-medium text-muted tabular-nums no-underline transition-colors hover:text-foreground"
            >
              [{cite[1]}]
            </a>
          </sup>
        );
      if (part.length > 4 && part.startsWith("**") && part.endsWith("**"))
        return (
          <strong key={k} className="font-semibold">
            {part.slice(2, -2)}
          </strong>
        );
      if (part.length > 2 && part.startsWith("*") && part.endsWith("*"))
        return <em key={k}>{part.slice(1, -1)}</em>;
      return <Fragment key={k}>{part}</Fragment>;
    });
}

export default function AnswerText({ text }: { text: string }) {
  const blocks = text.trim().split(/\n\s*\n/);

  return (
    <div className="space-y-3 text-[15px] leading-7">
      {blocks.map((block, b) => {
        // Group consecutive bullet lines into one list; everything else is a paragraph.
        const groups: { list: boolean; lines: string[] }[] = [];
        for (const line of block.split("\n").map((l) => l.trim()).filter(Boolean)) {
          const list = BULLET.test(line);
          const last = groups.at(-1);
          if (last && last.list === list) last.lines.push(line);
          else groups.push({ list, lines: [line] });
        }

        return groups.map((g, gi) =>
          g.list ? (
            <ul key={`${b}-${gi}`} className="list-disc space-y-1 pl-5 marker:text-muted">
              {g.lines.map((l, i) => (
                <li key={i}>{renderInline(l.replace(BULLET, ""), `${b}-${gi}-${i}`)}</li>
              ))}
            </ul>
          ) : (
            <p key={`${b}-${gi}`}>
              {g.lines.map((l, i) => (
                <Fragment key={i}>
                  {i > 0 && <br />}
                  {renderInline(l, `${b}-${gi}-${i}`)}
                </Fragment>
              ))}
            </p>
          ),
        );
      })}
    </div>
  );
}
