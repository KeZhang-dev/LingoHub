const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5021";

export type UploadedDocument = {
  id: string;
  fileName: string;
  fileType: string;
  uploadedAt: string;
};

export async function uploadDocument(file: File): Promise<UploadedDocument> {
  const body = new FormData();
  body.append("file", file);

  let res: Response;
  try {
    res = await fetch(`${API_URL}/api/documents`, { method: "POST", body });
  } catch {
    throw new Error("Couldn't reach the server. Is the API running?");
  }

  if (!res.ok) {
    const data = await res.json().catch(() => null);
    throw new Error(data?.error ?? "Upload failed. Please try again.");
  }
  return res.json();
}
