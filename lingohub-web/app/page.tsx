"use client";

import { useState } from "react";
import ChatInput from "@/components/ChatInput";
import AnswerPanel from "@/components/AnswerPanel";
import { exampleQuestions, getMockAnswer, type Answer } from "@/lib/mockAnswer";

export default function HomePage() {
  const [input, setInput] = useState("");
  const [question, setQuestion] = useState("");
  const [answer, setAnswer] = useState<Answer | null>(null);
  const [status, setStatus] = useState<"idle" | "loading" | "done">("idle");

  async function ask() {
    setQuestion(input.trim());
    setAnswer(null);
    setStatus("loading");
    setAnswer(await getMockAnswer());
    setStatus("done");
  }

  return (
    <div className="space-y-8">
      <header>
        <h1 className="text-3xl font-semibold tracking-tight sm:text-4xl">
          What would you like to learn today?
        </h1>
        <p className="mt-2 text-muted">
          Your assistant for everyday and workplace English in Australia and New Zealand.
        </p>
      </header>

      <ChatInput
        value={input}
        onChange={setInput}
        onSubmit={ask}
        disabled={status === "loading"}
      />

      <AnswerPanel
        status={status}
        question={question}
        answer={answer}
        suggestions={exampleQuestions}
        onPickSuggestion={setInput}
      />
    </div>
  );
}
