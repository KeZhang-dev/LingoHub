import type { Metadata } from "next";
import PageHeader from "@/components/PageHeader";

export const metadata: Metadata = { title: "About" };

export default function AboutPage() {
  return (
    <div>
      <PageHeader
        title="About LingoHub"
        description="LingoHub helps you learn the English people actually use in Australia and New Zealand — the everyday phrases, local expressions and workplace language that textbooks tend to miss."
      />

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
