import { ThemeToggle } from "@/components/theme-toggle";

export function PageHeader({
  title,
  description,
  children,
}: {
  title: React.ReactNode;
  description?: React.ReactNode;
  children?: React.ReactNode;
}) {
  return (
    <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
      <div className="min-w-0">
        <h1 className="text-2xl font-bold tracking-tight">{title}</h1>
        {description && <p className="mt-1 text-sm text-muted-foreground">{description}</p>}
      </div>
      <div className="flex shrink-0 items-center gap-2">
        {children}
        <ThemeToggle className="hidden lg:inline-flex" />
      </div>
    </div>
  );
}
