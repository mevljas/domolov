export interface DiffToken {
  text: string
  /** `same` appears on both sides; `added`/`removed` only on one. */
  kind: 'same' | 'added' | 'removed'
}

const MAX_TOKENS = 600

function tokenize(text: string): string[] {
  return text.split(/(\s+)/).filter((t) => t.length > 0)
}

const normalize = (token: string) => token.toLocaleLowerCase().replace(/[.,;:!?()"'«»„“]/g, '')

/**
 * Word-level diff (LCS) of two texts, returned per side so each can be highlighted.
 * Whitespace is kept as `same` so rendering preserves spacing. Long texts are truncated.
 */
export function wordDiff(left: string, right: string): { left: DiffToken[]; right: DiffToken[] } {
  const a = tokenize(left).slice(0, MAX_TOKENS)
  const b = tokenize(right).slice(0, MAX_TOKENS)
  const n = a.length
  const m = b.length
  const lcs: number[][] = Array.from({ length: n + 1 }, () => new Array<number>(m + 1).fill(0))
  for (let i = n - 1; i >= 0; i--) {
    for (let j = m - 1; j >= 0; j--) {
      lcs[i]![j] =
        normalize(a[i]!) === normalize(b[j]!) ? lcs[i + 1]![j + 1]! + 1 : Math.max(lcs[i + 1]![j]!, lcs[i]![j + 1]!)
    }
  }

  const outLeft: DiffToken[] = []
  const outRight: DiffToken[] = []
  let i = 0
  let j = 0
  while (i < n && j < m) {
    if (normalize(a[i]!) === normalize(b[j]!)) {
      outLeft.push({ text: a[i]!, kind: 'same' })
      outRight.push({ text: b[j]!, kind: 'same' })
      i++
      j++
    } else if (lcs[i + 1]![j]! >= lcs[i]![j + 1]!) {
      outLeft.push({ text: a[i]!, kind: /^\s+$/.test(a[i]!) ? 'same' : 'removed' })
      i++
    } else {
      outRight.push({ text: b[j]!, kind: /^\s+$/.test(b[j]!) ? 'same' : 'added' })
      j++
    }
  }
  for (; i < n; i++) outLeft.push({ text: a[i]!, kind: /^\s+$/.test(a[i]!) ? 'same' : 'removed' })
  for (; j < m; j++) outRight.push({ text: b[j]!, kind: /^\s+$/.test(b[j]!) ? 'same' : 'added' })
  return { left: merge(outLeft), right: merge(outRight) }
}

function merge(tokens: DiffToken[]): DiffToken[] {
  const out: DiffToken[] = []
  for (const token of tokens) {
    const last = out.at(-1)
    if (last && last.kind === token.kind) last.text += token.text
    else out.push({ ...token })
  }
  return out
}

/** Share of words in common, 0..1 — a quick similarity hint for the UI. */
export function wordOverlap(left: string, right: string): number {
  const a = new Set(tokenize(left).filter((t) => !/^\s+$/.test(t)).map(normalize))
  const b = new Set(tokenize(right).filter((t) => !/^\s+$/.test(t)).map(normalize))
  if (a.size === 0 && b.size === 0) return 1
  let common = 0
  for (const word of a) if (b.has(word)) common++
  return common / Math.max(a.size, b.size)
}
