import type { Metadata } from "next";
import Link from "next/link";
import PageHeader from "@/components/PageHeader";

export const metadata: Metadata = { title: "About" };

const steps = [
  {
    title: "Add your materials",
    body: (
      <>
        <Link href="/upload" className="text-foreground underline decoration-border underline-offset-4 transition-colors hover:decoration-foreground">
          Upload PDFs
        </Link>{" "}
        such as vocabulary lists, workplace guides or course notes.
      </>
    ),
  },
  {
    title: "Ask in your own words",
    body: "Ask about a word, a phrase or a situation — in English or in your first language.",
  },
  {
    title: "Get grounded answers",
    body: "Answers come from your documents, with sources. When your materials don't cover a question, LingoHub says so instead of guessing.",
  },
];

export default function AboutPage() {
  return (
    <div className="mx-auto max-w-2xl">
      <PageHeader
        title="About LingoHub"
        description="LingoHub helps you learn the English people actually use in Australia and New Zealand — the everyday phrases, local expressions and workplace language that textbooks tend to miss."
      />

      <section className="mt-12">
        <h2 className="text-xs font-medium text-muted">How it works</h2>
        <ol className="mt-3 divide-y divide-border border-y border-border">
          {steps.map((step, i) => (
            <li key={step.title} className="flex gap-6 py-5">
              <span className="w-6 shrink-0 pt-px font-mono text-xs text-muted tabular-nums">
                {String(i + 1).padStart(2, "0")}
              </span>
              <div>
                <h3 className="text-[15px] font-medium">{step.title}</h3>
                <p className="mt-1 text-sm leading-relaxed text-muted">{step.body}</p>
              </div>
            </li>
          ))}
        </ol>
      </section>

      <section className="mt-12">
        <h2 className="text-xs font-medium text-muted">Contact</h2>
        <p className="mt-3 text-[15px]">
          Questions or feedback?{" "}
          <a
            href="mailto:hello@lingohub.example"
            className="underline decoration-border underline-offset-4 transition-colors hover:decoration-foreground"
          >
            hello@lingohub.example
          </a>
        </p>
      </section>
    </div>
  );
}
