import type { Metadata } from "next";
import PageHeader from "@/components/PageHeader";
import UploadArea from "@/components/UploadArea";

export const metadata: Metadata = { title: "Upload" };

export default function UploadPage() {
  return (
    <div className="mx-auto max-w-2xl">
      <PageHeader
        title="Upload materials"
        description="Add English learning documents to ask questions about later."
      />
      <div className="mt-10">
        <UploadArea />
      </div>
    </div>
  );
}
