"use client";

import { useSyncExternalStore } from "react";

// Favorites are a list of vocabulary ids in localStorage (no accounts yet).
// One small store shared by every component, so Home and Favorites always agree —
// including across browser tabs (via the "storage" event).

const STORAGE_KEY = "lingohub:favorites";
const CHANGE_EVENT = "lingohub:favorites-change";
const EMPTY: ReadonlySet<string> = new Set();

// useSyncExternalStore needs the same object back until the data really changes.
let cachedRaw: string | null | undefined;
let cachedSet: ReadonlySet<string> = EMPTY;

function readFavorites(): ReadonlySet<string> {
  let raw: string | null = null;
  try {
    raw = localStorage.getItem(STORAGE_KEY);
  } catch {
    // Storage blocked (e.g. privacy settings): behave as if nothing is saved.
  }
  if (raw !== cachedRaw) {
    cachedRaw = raw;
    try {
      const ids: unknown = JSON.parse(raw ?? "[]");
      cachedSet = new Set(Array.isArray(ids) ? ids.filter((id) => typeof id === "string") : []);
    } catch {
      cachedSet = EMPTY;
    }
  }
  return cachedSet;
}

function subscribe(onChange: () => void) {
  const onStorage = (e: StorageEvent) => {
    if (e.key === STORAGE_KEY || e.key === null) onChange();
  };
  window.addEventListener("storage", onStorage);
  window.addEventListener(CHANGE_EVENT, onChange);
  return () => {
    window.removeEventListener("storage", onStorage);
    window.removeEventListener(CHANGE_EVENT, onChange);
  };
}

export function toggleFavorite(id: string) {
  const next = new Set(readFavorites());
  if (next.has(id)) next.delete(id);
  else next.add(id);
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify([...next]));
  } catch {
    return; // Can't persist; leave the state unchanged rather than pretend it saved.
  }
  window.dispatchEvent(new Event(CHANGE_EVENT));
}

// The server has no localStorage, so it renders "nothing saved"; the client fills in after hydration.
export function useFavorites() {
  const favorites = useSyncExternalStore(subscribe, readFavorites, () => EMPTY);
  return { favorites, toggleFavorite };
}
