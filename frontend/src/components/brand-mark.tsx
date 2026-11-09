export function BrandMark({ size = 22 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 22 22" aria-hidden="true">
      <rect x="1" y="1" width="20" height="20" rx="4" className="fill-primary" />
      <path d="M7 6v10M7 11l7-5M9.5 9.5L15 16" stroke="#FFFFFF" strokeWidth="2" fill="none" strokeLinecap="round" />
    </svg>
  );
}
