"use client";

import { useSyncExternalStore } from "react";
import { vocabulary, type VocabularyItem } from "./vocabulary";

// A random order of the whole vocabulary list for the Home page.
// Created once per page load and replaced by reshuffle(). It only exists in the browser:
// the server renders nothing, so server and client HTML never disagree about the order.

type Shuffle = { items: VocabularyItem[]; version: number };

let current: Shuffle | null = null;
const listeners = new Set<() => void>();

// Fisher–Yates shuffle on a copy of the list.
function shuffled(): VocabularyItem[] {
  const items = [...vocabulary];
  for (let i = items.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [items[i], items[j]] = [items[j], items[i]];
  }
  return items;
}

function getSnapshot(): Shuffle {
  current ??= { items: shuffled(), version: 0 };
  return current;
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function reshuffle() {
  current = { items: shuffled(), version: (current?.version ?? 0) + 1 };
  listeners.forEach((listener) => listener());
}

// null on the server and during hydration; the shuffled list once the browser takes over.
export function useShuffledVocabulary(): Shuffle | null {
  return useSyncExternalStore(subscribe, getSnapshot, () => null);
}
