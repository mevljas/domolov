/** Hostname from a Watch search URL, or the raw string if parsing fails. */
export function urlHost(url: string): string {
  try {
    return new URL(url).host
  } catch {
    return url
  }
}
