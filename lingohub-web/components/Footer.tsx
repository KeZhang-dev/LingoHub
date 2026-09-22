export default function Footer() {
  return (
    <footer className="border-t border-border">
      <div className="mx-auto w-full max-w-3xl px-5 py-6 text-sm text-muted">
        © {new Date().getFullYear()} LingoHub · English for Australia &amp; New Zealand
      </div>
    </footer>
  );
}
