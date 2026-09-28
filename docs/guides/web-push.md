# Web Push notifications

Domolov can ping every browser that has subscribed, using the [Web Push](https://web.dev/articles/push-notifications-overview) protocol and a VAPID key pair. The toggle lives in **Settings**. The destination on a Watch route is almost always `all`.

This only works on a **secure origin** (HTTPS, or `localhost`).

## 1. Generate VAPID keys

```bash
npx --yes web-push generate-vapid-keys
```

Put them in `.env`:

```dotenv
DOMOLOV_VAPID_PUBLIC_KEY=BNxxx...
DOMOLOV_VAPID_PRIVATE_KEY=xxxx...
DOMOLOV_VAPID_SUBJECT=mailto:you@example.com
```

`SUBJECT` is a contact URI the push service may show if something is wrong. `mailto:` or `https:` both work.

Recreate api and worker: `docker compose up -d api worker`.

Never commit the private key. Rotating keys invalidates every stored subscription — people have to toggle Push off and on again.

## 2. Subscribe this browser

1. Sign in on the HTTPS origin (or localhost).
2. Open **Settings → Notifications**.
3. Enable **Web Push**. The app asks the browser for permission, registers the service worker, and `POST`s `{ endpoint, p256dh, auth }` to `/api/push-subscriptions`.
4. Add a **Web Push** route on a Watch with destination `all` (the UI default). **Send test**.

If VAPID is not configured the toggle explains that and stays off.

## 3. What a push looks like

A notification with the listing title, a one-line body (`blurb · location · price`), and the listing photo when we have one. Clicking it opens the path from the payload (usually `/homes/{id}`). The service worker is already wired for this.

## 4. Dead subscriptions

If a push service answers **404** or **410**, Domolov deletes that subscription on the next send. You do not have to clean them up. The daily retention job also drops stale sessions; subscriptions without a browser simply 410 themselves.

## 5. Several browsers

Each browser (phone, laptop, second profile) is its own subscription. Destination `all` fans out to every row in `WebPushSubscriptions`. Signing out does not remove the subscription — turn the toggle off, or delete the row from **Settings** if you want silence on that device.

## Troubleshooting

| Symptom | Check |
|---|---|
| Toggle disabled | VAPID keys missing; containers not recreated |
| Permission denied | Browser site settings; not HTTPS |
| Test fails with 400/401 from the push service | Public/private key pair mismatch; `SUBJECT` empty or invalid |
| Works on desktop, not on iOS | iOS needs the PWA added to the Home Screen, then permission from the installed app |
| Pushes stop after a redeploy | Keys changed, or the service worker never updated — refresh once while online |
