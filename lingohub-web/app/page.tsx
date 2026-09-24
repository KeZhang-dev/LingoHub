"use client";

import { useState } from "react";
import AnswerPanel, { type AskStatus } from "@/components/AnswerPanel";
import ChatInput from "@/components/ChatInput";
import LessonCard from "@/components/LessonCard";
import { askQuestion, type RagAnswer } from "@/lib/api";
import { sampleLessons } from "@/lib/lessons";

export default function HomePage() {
  // Lesson (primary). Mock data for now; "Next lesson" just steps through the samples.
  const [lessonIndex, setLessonIndex] = useState(0);
  const lesson = sampleLessons[lessonIndex];

  // Ask about it (secondary). Uses the real RAG endpoint, POST /api/ask.
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

  function nextLesson() {
    setLessonIndex((i) => (i + 1) % sampleLessons.length);
    setInput("");
    setQuestion("");
    setAnswer(null);
    setError("");
    setStatus("idle");
  }

  return (
    <div className="mx-auto max-w-2xl pt-4 sm:pt-12">
      <header>
        <h1 className="text-[2rem] leading-[1.15] font-semibold tracking-[-0.02em] text-balance sm:text-[2.5rem]">
          Learn English for life and work in Australia &amp; New Zealand.
        </h1>
      </header>

      <div className="mt-12">
        <LessonCard lesson={lesson} onNext={nextLesson} />
      </div>

      <section aria-labelledby="ask-heading" className="mt-12">
        <h2 id="ask-heading" className="text-sm font-medium">
          Have a question about this lesson?
        </h2>
        <div className="mt-3">
          <ChatInput
            value={input}
            onChange={setInput}
            onSubmit={() => ask(input.trim())}
            disabled={status === "loading"}
            placeholder={`Ask about “${lesson.term}”…`}
            label="Ask about this lesson"
          />
        </div>

        <div className="mt-6">
          <AnswerPanel
            status={status}
            question={question}
            answer={answer}
            error={error}
            suggestions={lesson.prompts}
            onPickSuggestion={setInput}
            onRetry={() => ask(question)}
          />
        </div>
      </section>
    </div>
  );
}
