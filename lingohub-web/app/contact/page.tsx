import type { Metadata } from "next";

export const metadata: Metadata = { title: "Contact" };

export default function ContactPage() {
  return (
    <div className="space-y-8">
      <header>
        <h1 className="text-3xl font-semibold tracking-tight">Contact</h1>
        <p className="mt-2 text-muted">
          Questions, feedback or ideas? We&apos;d like to hear from you.
        </p>
      </header>
      <dl className="divide-y divide-border border-y border-border">
        <div className="flex flex-col gap-1 py-4 sm:flex-row sm:gap-8">
          <dt className="w-28 shrink-0 text-sm text-muted">Email</dt>
          <dd>
            <a
              href="mailto:hello@lingohub.example"
              className="text-accent underline-offset-2 hover:underline"
            >
              hello@lingohub.example
            </a>
          </dd>
        </div>
        <div className="flex flex-col gap-1 py-4 sm:flex-row sm:gap-8">
          <dt className="w-28 shrink-0 text-sm text-muted">Response time</dt>
          <dd>Within two business days</dd>
        </div>
      </dl>
    </div>
  );
}
