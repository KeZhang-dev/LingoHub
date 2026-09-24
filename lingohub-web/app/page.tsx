"use client";

import { useState } from "react";
import AnswerPanel, { type AskStatus } from "@/components/AnswerPanel";
import ChatInput from "@/components/ChatInput";
import VocabularyGrid from "@/components/VocabularyGrid";
import { askQuestion, type RagAnswer } from "@/lib/api";
import { vocabulary } from "@/lib/vocabulary";

export default function HomePage() {
  // Supporting: ask a free-form question via the existing RAG endpoint, POST /api/ask.
  const [input, setInput] = useState("");
  const [question, setQuestion] = useState("");
  const [answer, setAnswer] = useState<RagAnswer | null>(null);
  const [error, setError] = useState("");
  const [status, setStatus] = useState<AskStatus>("idle");

  async function ask(q: string) {
    if (!q) return;
    setQuestion(q);
    setAnswer(null);
    setError("");
    setStatus("loading");
    try {
      setAnswer(await askQuestion(q));
      setStatus("done");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Something went wrong. Please try again.");
      setStatus("error");
    }
  }

  return (
    <div>
      <section aria-labelledby="ask-heading">
        <h1 id="ask-heading" className="text-xl font-semibold tracking-tight">
          Have a question about a word?
        </h1>
        <p className="mt-1 text-sm text-muted">Answers come from your learning materials, with sources.</p>
        <div className="mt-4">
          <ChatInput
            value={input}
            onChange={setInput}
            onSubmit={() => ask(input.trim())}
            disabled={status === "loading"}
            placeholder="e.g. What does “knock off” mean?"
            label="Ask a question about a word"
          />
        </div>
        <div className="mt-6">
          <AnswerPanel
            status={status}
            question={question}
            answer={answer}
            error={error}
            suggestions={[]}
            onPickSuggestion={setInput}
            onRetry={() => ask(question)}
          />
        </div>
      </section>

      <section aria-label="Vocabulary" className="mt-10">
        <VocabularyGrid items={vocabulary} />
      </section>
    </div>
  );
}
