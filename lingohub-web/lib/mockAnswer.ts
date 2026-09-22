// Placeholder until the RAG endpoint exists. Replace with a call in lib/api.ts.
export type Answer = {
  text: string;
  sources: { title: string; page: number }[];
};

export const exampleQuestions = [
  "What does “touch base” mean in an Aussie workplace?",
  "How do I politely decline a meeting in New Zealand English?",
  "What's the difference between “arvo” and “afternoon”?",
];

export async function getMockAnswer(): Promise<Answer> {
  await new Promise((r) => setTimeout(r, 900));
  return {
    text: "This is a sample answer. Once the RAG pipeline is connected, LingoHub will search your uploaded materials and answer here, based on what your documents actually say.",
    sources: [
      { title: "Workplace English Guide.pdf", page: 12 },
      { title: "Everyday Aussie Phrases.pdf", page: 4 },
    ],
  };
}
