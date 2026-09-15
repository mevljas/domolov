# Discord notifications for a Watch

Attach a Discord **NotificationRoute** to a **Watch** so Domolov posts embeds when listings match your triggers. Domolov uses Discord **incoming webhooks** (not a bot).

## Prerequisites

- Domolov is running and you can sign in as the operator
- A Discord channel where you can manage integrations (create webhooks)

## 1. Create a Discord webhook

1. Open the target Discord channel → **Edit Channel** → **Integrations** → **Webhooks**.
2. Create a webhook (name it something recognizable, e.g. `Domolov`).
3. Copy the webhook URL (`https://discord.com/api/webhooks/...`).

Prefer a **dedicated channel** for Domolov so listing noise stays out of general chat.

## 2. Attach a NotificationRoute to a Watch

1. Sign in to Domolov and open the **Watches** page.
2. Find the Watch you want (create one first if needed).
3. Under **Notification routes**:
   - Set **Channel** to `Discord`.
   - Paste the webhook URL into **Destination**.
   - Click **Add route**.

The route appears in the list as `Discord → <webhook URL> (...)`.

There is no separate Discord env var. The webhook URL lives on the NotificationRoute as **Destination**.

## 3. What fires today

When you add a route in the UI, Domolov sets triggers to **new listing** and **price decreased** only. The Watches UI does not offer a trigger picker for price increases or “any price change.”

## 4. Baseline ScanRun

The first successful **ScanRun** for a Watch is a silent **baseline**: listings are stored, but no Discord messages are sent. Later ScanRuns notify according to the NotificationRoute triggers.

Use **Run now** (or wait for the schedule) after adding the route. Expect silence on that first successful run if the Watch has not completed baseline yet.

## 5. Treat the webhook URL as a secret

Anyone with the URL can post to your channel. Domolov stores it in PostgreSQL as NotificationRoute.Destination (not in env).

- Do not commit webhook URLs to git or paste them into public tickets.
- To rotate: delete or regenerate the webhook in Discord, then delete the old NotificationRoute in Domolov and add a new one with the new URL.

## Troubleshooting

| Symptom | What to check |
|---------|----------------|
| No messages on first run | Baseline ScanRun — expected; wait for the next scan after baseline completes. |
| Still nothing later | Watch is enabled; Discord NotificationRoute exists; Destination is the full webhook URL. |
| Messages for new listings but not price increases | Current UI triggers are new listing + price decreased only. |
| Webhook errors in logs | Look for `Discord webhook failed` (status + body). Common causes: revoked webhook, typo in Destination, Discord outage. |

After fixing Destination or recreating the webhook, run the Watch again (or wait for the schedule) to confirm embeds arrive.
