export const seriesColors = [
  'var(--s1)',
  'var(--s2)',
  'var(--s3)',
  'var(--s4)',
  'var(--s5)',
] as const

export const seriesColor = (index: number): string =>
  seriesColors[index % seriesColors.length]
