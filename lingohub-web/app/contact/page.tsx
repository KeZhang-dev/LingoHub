import type { Metadata } from "next";
import PageHeader from "@/components/PageHeader";

export const metadata: Metadata = { title: "Contact" };

export default function ContactPage() {
  return (
    <div className="mx-auto max-w-2xl">
      <PageHeader title="Contact" description="Questions, feedback or ideas? We'd like to hear from you." />
      <dl className="mt-10 divide-y divide-border border-y border-border">
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
