// Backend base URL. Set NEXT_PUBLIC_API_URL (see .env.example) when the API runs somewhere else;
// the backend's Cors:AllowedOrigins must include this frontend's origin.
const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5021";

const UNREACHABLE = "Couldn't reach the server. Is the API running?";

// Mirrors the backend's RetrievedChunkDto (one retrieved passage).
export type Source = {
  rank: number;
  chunkId: string;
  documentId: string;
  fileName: string;
  chunkIndex: number;
  distance: number;
  similarity: number;
  content: string;
};

// Where an answer came from: the learner's materials, nowhere (not in the materials), or — only after the
// learner approves — the LLM's general knowledge, which has no sources.
export type AnswerSource = "documents" | "notFound" | "general";

// Mirrors the backend's RagAnswerDto (POST /api/ask). Citations like [1] in `answer` refer to `sources[].rank`.
export type RagAnswer = {
  question: string;
  answer: string;
  answerSource: AnswerSource;
  llmModel: string;
  embeddingModel: string;
  finishReason: string;
  promptTokens: number;
  answerTokens: number;
  thinkingTokens: number;
  embedMs: number;
  searchMs: number;
  generateMs: number;
  sources: Source[];
};

// Turns a failed response into a message for the learner. 4xx errors carry a readable { error } from
// our own validation; 5xx/502 bodies can contain raw upstream (Voyage/Gemini) details, so those go
// to the console and the learner gets a plain sentence.
async function errorMessage(res: Response, fallback: string): Promise<string> {
  const data = await res.json().catch(() => null);
  const detail = typeof data?.error === "string" ? data.error : "";
  if (res.status >= 500) {
    if (detail) console.error(`API ${res.status}:`, detail);
    return res.status === 502
      ? "The AI service is busy or has reached its usage limit. Please try again later."
      : "The server ran into a problem. Please try again.";
  }
  return detail || fallback;
}

// useGeneralKnowledge: answer from the LLM's own knowledge instead of the materials. Only send it after the
// learner has approved it for this question.
export async function askQuestion(
  question: string,
  { useGeneralKnowledge = false }: { useGeneralKnowledge?: boolean } = {},
): Promise<RagAnswer> {
  let res: Response;
  try {
    res = await fetch(`${API_URL}/api/ask`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ question, useGeneralKnowledge }),
    });
  } catch {
    throw new Error(UNREACHABLE);
  }

  if (!res.ok) throw new Error(await errorMessage(res, "Something went wrong. Please try again."));

  const data = await res.json().catch(() => null);
  if (typeof data?.answer !== "string" || !data.answer.trim() || !Array.isArray(data.sources)) {
    throw new Error("The server returned an empty or invalid answer. Please try again.");
  }
  return data as RagAnswer;
}
