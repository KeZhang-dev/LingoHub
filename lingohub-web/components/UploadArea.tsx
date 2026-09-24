"use client";

import { useRef, useState } from "react";
import { uploadDocument } from "@/lib/api";

const MAX_SIZE = 20 * 1024 * 1024;

type Status =
  | { state: "idle" }
  | { state: "uploading" }
  | { state: "success" }
  | { state: "error"; message: string };

function formatSize(bytes: number) {
  return bytes < 1024 * 1024
    ? `${Math.max(1, Math.round(bytes / 1024))} KB`
    : `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

export default function UploadArea() {
  const inputRef = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [dragging, setDragging] = useState(false);
  const [status, setStatus] = useState<Status>({ state: "idle" });

  function resetInput() {
    if (inputRef.current) inputRef.current.value = "";
  }

  function select(f: File | undefined) {
    if (!f) return;
    if (!f.name.toLowerCase().endsWith(".pdf")) {
      setFile(null);
      setStatus({ state: "error", message: "Only PDF files are supported." });
    } else if (f.size > MAX_SIZE) {
      setFile(null);
      setStatus({ state: "error", message: "The file is larger than 20 MB." });
    } else {
      setFile(f);
      setStatus({ state: "idle" });
    }
    resetInput();
  }

  function clear() {
    setFile(null);
    setStatus({ state: "idle" });
  }

  async function upload() {
    if (!file) return;
    setStatus({ state: "uploading" });
    try {
      await uploadDocument(file);
      setStatus({ state: "success" });
      setFile(null);
    } catch (e) {
      setStatus({
        state: "error",
        message: e instanceof Error ? e.message : "Upload failed.",
      });
    }
  }

  const uploading = status.state === "uploading";

  return (
    <div className="space-y-4">
      <div
        onDragOver={(e) => {
          e.preventDefault();
          setDragging(true);
        }}
        onDragLeave={() => setDragging(false)}
        onDrop={(e) => {
          e.preventDefault();
          setDragging(false);
          if (!uploading) select(e.dataTransfer.files[0]);
        }}
        className={`flex flex-col items-center rounded-2xl border-2 border-dashed px-6 py-14 text-center transition-colors ${
          dragging ? "border-accent bg-accent-soft" : "border-border bg-surface"
        }`}
      >
        <svg
          className="h-8 w-8 text-muted"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.6"
          strokeLinecap="round"
          strokeLinejoin="round"
          aria-hidden
        >
          <path d="M12 16V4M7 9l5-5 5 5M4 20h16" />
        </svg>
        <p className="mt-3 font-medium">Drag and drop a PDF here</p>
        <p className="mt-1 text-sm text-muted">
          or{" "}
          <button
            type="button"
            onClick={() => inputRef.current?.click()}
            disabled={uploading}
            className="font-medium text-accent underline-offset-2 hover:underline disabled:opacity-40"
          >
            browse files
          </button>{" "}
          · PDF, up to 20 MB
        </p>
        <input
          ref={inputRef}
          type="file"
          accept="application/pdf,.pdf"
          onChange={(e) => select(e.target.files?.[0])}
          className="hidden"
          aria-label="Choose a PDF file"
        />
      </div>

      {file && (
        <div className="animate-fade-up flex items-center justify-between gap-3 rounded-xl border border-border bg-surface px-4 py-3">
          <div className="min-w-0">
            <p className="truncate text-sm font-medium">{file.name}</p>
            <p className="text-xs text-muted">{formatSize(file.size)}</p>
          </div>
          <button
            type="button"
            onClick={clear}
            disabled={uploading}
            className="text-sm text-muted hover:text-foreground disabled:opacity-40"
          >
            Remove
          </button>
        </div>
      )}

      <div className="flex items-center gap-4">
        <button
          type="button"
          onClick={upload}
          disabled={!file || uploading}
          className="rounded-lg bg-accent px-5 py-2 text-sm font-medium text-accent-foreground transition-colors hover:bg-accent-hover disabled:cursor-not-allowed disabled:opacity-40"
        >
          {uploading ? "Uploading…" : "Upload"}
        </button>
        <p
          role="status"
          className={`flex items-center gap-2 text-sm ${
            status.state === "error" ? "text-red-600 dark:text-red-400" : "text-muted"
          }`}
        >
          {status.state === "success" && (
            <>
              <span className="h-2 w-2 rounded-full bg-secondary" aria-hidden />
              {"Uploaded. Processing isn't enabled yet."}
            </>
          )}
          {status.state === "error" && status.message}
        </p>
      </div>
    </div>
  );
}
