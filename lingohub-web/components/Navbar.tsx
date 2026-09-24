"use client";

import Image from "next/image";
import Link from "next/link";
import { usePathname } from "next/navigation";
import ThemeToggle from "./ThemeToggle";

const links = [
  { href: "/", label: "Home" },
  { href: "/favorites", label: "Favorites" },
  { href: "/about", label: "About" },
];

export default function Navbar() {
  const pathname = usePathname();

  return (
    <header className="sticky top-0 z-10 border-b border-border bg-background/85 backdrop-blur">
      {/* Three columns: logo | centered links | theme toggle. The equal 1fr sides keep the links truly centered. */}
      <nav className="mx-auto grid h-16 w-full max-w-[1200px] grid-cols-[1fr_auto_1fr] items-center px-5">
        <Link
          href="/"
          aria-label="LingoHub home"
          className="flex items-center gap-2.5 justify-self-start text-[17px] font-semibold tracking-tight"
        >
          {/* public/LingoHub-logo.svg is square (97×97), so 28×28 keeps its natural ratio. */}
          <Image src="/LingoHub-logo.svg" alt="" width={28} height={28} loading="eager" className="shrink-0" />
          <span className="hidden sm:inline">LingoHub</span>
        </Link>

        <ul className="flex items-center gap-7 sm:gap-10">
          {links.map(({ href, label }) => {
            const active = pathname === href;
            return (
              <li key={href}>
                <Link
                  href={href}
                  aria-current={active ? "page" : undefined}
                  // The underline is a 1.5px ::after bar that fades in on the active page.
                  className={`relative block py-1.5 text-[15px] transition-colors after:absolute after:inset-x-0 after:-bottom-px after:h-[1.5px] after:bg-foreground after:transition-opacity ${
                    active
                      ? "text-foreground after:opacity-100"
                      : "text-muted after:opacity-0 hover:text-foreground"
                  }`}
                >
                  {label}
                </Link>
              </li>
            );
          })}
        </ul>

        <div className="justify-self-end">
          <ThemeToggle />
        </div>
      </nav>
    </header>
  );
}
