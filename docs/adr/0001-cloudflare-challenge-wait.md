# Wait through CloudflareChallenge; strike only on CloudflareBlock

Domolov crawls nepremicnine.net behind Cloudflare. We wait a bounded time for a transient CloudflareChallenge to clear (like a normal browser), and only raise CloudflareBlockedException / apply a CloudflareStrike when it becomes a CloudflareBlock. We do not add CAPTCHA solvers or proxy rotation in v1; fingerprint realism and pacing are the allowed mitigations.
