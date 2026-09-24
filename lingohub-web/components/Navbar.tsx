"use client";

import Image from "next/image";
import Link from "next/link";
import { usePathname } from "next/navigation";
import ThemeToggle from "./ThemeToggle";

const links = [
  { href: "/", label: "Home" },
  { href: "/upload", label: "Upload" },
  { href: "/contact", label: "Contact" },
];

export default function Navbar() {
  const pathname = usePathname();

  return (
    <header className="sticky top-0 z-10 border-b border-border bg-background/80 backdrop-blur">
      <nav className="mx-auto flex h-16 w-full max-w-[1200px] items-center justify-between px-5">
        <Link href="/" className="flex items-center gap-2.5 text-xl font-semibold tracking-tight">
          {/* Both logos are rendered; CSS shows the one matching the theme. */}
          <Image src="/LingoHub-logo2.svg" alt="" width={32} height={32} loading="eager" className="dark:hidden" />
          <Image src="/LingoHub-logo.svg" alt="" width={32} height={32} loading="eager" className="hidden dark:block" />
          <span>
            Lingo<span className="text-accent">Hub</span>
          </span>
        </Link>
        <div className="flex items-center gap-1 sm:gap-2">
          {links.map(({ href, label }) => {
            const active = pathname === href;
            return (
              <Link
                key={href}
                href={href}
                aria-current={active ? "page" : undefined}
                // The underline is an ::after bar that fades in on the active page.
                className={`relative px-2.5 py-1.5 text-base transition-colors after:absolute after:inset-x-2.5 after:-bottom-0.5 after:h-0.5 after:rounded-full after:bg-accent after:transition-opacity sm:px-3 sm:after:inset-x-3 ${
                  active
                    ? "font-medium text-foreground after:opacity-100"
                    : "text-muted after:opacity-0 hover:text-foreground"
                }`}
              >
                {label}
              </Link>
            );
          })}
          <ThemeToggle />
        </div>
      </nav>
    </header>
  );
}
