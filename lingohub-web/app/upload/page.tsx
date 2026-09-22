import type { Metadata } from "next";
import UploadArea from "@/components/UploadArea";

export const metadata: Metadata = { title: "Upload" };

export default function UploadPage() {
  return (
    <div className="space-y-8">
      <header>
        <h1 className="text-3xl font-semibold tracking-tight">Upload materials</h1>
        <p className="mt-2 text-muted">
          Add English learning documents to ask questions about later.
        </p>
      </header>
      <UploadArea />
    </div>
  );
}
