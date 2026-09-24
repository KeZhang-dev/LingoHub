type Props = {
  title: string;
  description?: string;
};

// Shared title + subtitle for the secondary pages, so they all share one type scale.
export default function PageHeader({ title, description }: Props) {
  return (
    <header>
      <h1 className="text-2xl font-semibold tracking-tight sm:text-[1.75rem]">{title}</h1>
      {description && <p className="mt-2 text-[15px] leading-relaxed text-muted">{description}</p>}
    </header>
  );
}
