import raw from "@/data/kiwi_it_words_v2.json";

// Source: data/kiwi_it_words_v2.json — a verbatim copy of the project's 3,000-word list
// (repo root: kiwi_it_words_v2.json). Nothing here is invented; fields are only renamed and split.
//   word      "sweet as (Phrase)"  → term + part of speech
//   cn        Chinese meaning       (the source has no English definition)
//   eg        English example sentence
//   Translate Chinese translation of the example sentence
type RawEntry = { word: string; cn: string; eg: string; Translate: string };

export type VocabularyItem = {
  id: string; // the term itself — unique across the list, and stable for saved favorites
  term: string;
  partOfSpeech: string;
  meaningZh: string;
  example: string;
  exampleZh: string;
};

const TERM_AND_POS = /^(.*?)\s*\(([^()]+)\)\s*$/;

export const vocabulary: VocabularyItem[] = (raw as RawEntry[]).map((entry) => {
  const match = entry.word.match(TERM_AND_POS);
  const term = match ? match[1] : entry.word;
  return {
    id: term,
    term,
    partOfSpeech: match ? match[2].toLowerCase() : "",
    meaningZh: entry.cn,
    example: entry.eg,
    exampleZh: entry.Translate,
  };
});
