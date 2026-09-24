export default function Footer() {
  return (
    <footer className="border-t border-border">
      <p className="mx-auto w-full max-w-[1200px] px-5 py-8 text-center text-xs text-balance text-muted">
        © {new Date().getFullYear()} LingoHub · English for life and work in Australia &amp; New Zealand
      </p>
    </footer>
  );
}
