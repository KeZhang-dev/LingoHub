// MOCK lesson data. Entries and example sentences come from the Kiwi IT Workplace English list;
// the explanations are hand-written placeholders. Replace with backend lesson generation later.

export type Lesson = {
  term: string;
  partOfSpeech: string;
  register?: string;
  context: "Everyday English" | "Workplace English";
  explanation: string;
  example: string;
  // Suggested follow-up questions shown under "Ask about this lesson".
  prompts: string[];
};

export const sampleLessons: Lesson[] = [
  {
    term: "arvo",
    partOfSpeech: "noun",
    register: "informal",
    context: "Everyday English",
    explanation: "A short form of “afternoon”, commonly used in Australian and New Zealand English.",
    example: "Let's catch up this arvo.",
    prompts: ["What's the difference between “arvo” and “afternoon”?", "Can I use “arvo” at work?"],
  },
  {
    term: "knock off",
    partOfSpeech: "phrasal verb",
    register: "informal",
    context: "Workplace English",
    explanation: "To finish work for the day. “Knock off early” means to leave work earlier than usual.",
    example: "I'll knock off early today.",
    prompts: ["What does “knock off early” mean?", "How do I ask my manager to knock off early?"],
  },
  {
    term: "touch base",
    partOfSpeech: "phrase",
    context: "Workplace English",
    explanation: "To make brief contact with someone to check in or share an update.",
    example: "Let's touch base tomorrow morning.",
    prompts: ["What does “touch base” mean in an Aussie workplace?", "What's a similar phrase to “touch base”?"],
  },
  {
    term: "heaps",
    partOfSpeech: "adverb",
    register: "informal",
    context: "Everyday English",
    explanation: "A lot, or very. Very common in casual Australian and New Zealand speech.",
    example: "We've got heaps of tickets this sprint.",
    prompts: ["What does “heaps” mean?", "Is “heaps” too casual for an email?"],
  },
  {
    term: "keen",
    partOfSpeech: "adjective",
    context: "Everyday English",
    explanation: "Eager or interested in doing something.",
    example: "I'm keen to try the new framework.",
    prompts: ["What does “keen” mean?", "How do I say I'm keen on a project?"],
  },
];
