# Discord notifications

Domolov posts embeds to a channel through a Discord **incoming webhook**. There is no bot, no gateway, no OAuth.

## 1. Create a webhook

1. Open the target channel → **Edit Channel** → **Integrations** → **Webhooks**.
2. Create a webhook. Name it something you will recognise (`Domolov`).
3. Copy the URL (`https://discord.com/api/webhooks/…`).

Use a **dedicated channel**. Listing noise does not belong in general chat.

Anyone with the URL can post to that channel. Treat it like a password.

## 2. Attach a route to a Watch

1. Open the Watch → **Notifications** (or the last step of **New Watch**).
2. Channel: **Discord**.
3. Destination: the webhook URL.
4. Triggers: at least **new listing** and **price decreased**. Add **reposted** if you care about ads that come back.
5. Save. Click **Send test**. You should see a Domolov embed in the channel within a second.

There is no `DOMOLOV_DISCORD_*` environment variable. The webhook URL *is* the secret, stored as `NotificationRoute.Destination`.

## 3. What an embed contains

Title (linked to the listing), a short body, the listing photo when we have one, and inline fields: location, type, rooms, size, year, price, previous price. If `DOMOLOV_PUBLIC_URL` is set, the body can include an Open in Domolov path.

## 4. When it fires

The first successful ScanRun of a Watch is a silent **baseline**. Nothing is posted.

Later ScanRuns notify only for events that match the route's triggers. A price drop on a Dismissed Home is not sent. A bookmarked Home in stage Rejected *is* still notified.

## 5. Rotate or revoke

1. Delete or regenerate the webhook in Discord.
2. Delete the old route in Domolov and add a new one with the new URL.

Do not commit webhook URLs. Do not paste them into public issues.

## Troubleshooting

| Symptom | Check |
|---|---|
| Silence on the first run | Baseline — expected |
| Silence later | Watch not paused; route enabled; Destination is the full URL; trigger matches the event |
| `Discord webhook failed` in logs | Revoked webhook, typo, or Discord outage. Status + body are in the ScanRun |
| Test works, scans do not | You added the route *after* the events already happened; press **Run now** |

The older path `docs/discord-notifications.md` points here.
