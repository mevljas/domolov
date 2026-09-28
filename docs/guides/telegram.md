# Telegram notifications

Domolov talks to Telegram as a **bot**: one token in the environment, a chat id on each NotificationRoute.

## 1. Create a bot

1. In Telegram, open [@BotFather](https://t.me/BotFather) and send `/newbot`.
2. Copy the token (`123456:ABC-DEF…`).
3. Put it in `.env`:

   ```dotenv
   DOMOLOV_TELEGRAM_BOT_TOKEN=123456:ABC-DEF
   ```

4. Recreate the api and worker so they pick it up: `docker compose up -d api worker`.

Keep the token out of git.

## 2. Get a chat id

**Private chat with the bot**

1. Open the bot and press Start (`/start`). Bots cannot message you first.
2. Visit `https://api.telegram.org/bot<token>/getUpdates` in a browser (or `curl`).
3. Read `result[0].message.chat.id` (a number, sometimes negative).

**Group**

1. Add the bot to the group.
2. Send a short message in the group.
3. Call `getUpdates` again. Group ids are negative.

## 3. Attach a route

1. Open the Watch → **Notifications**.
2. Channel: **Telegram**.
3. Destination: the chat id, nothing else.
4. Pick triggers. Save. **Send test**.

If the token is missing the test fails immediately with “Telegram bot token is not configured.”

## 4. What the message looks like

Markdown: bold title, body, listing URL, then location / type / rooms / size / year / price. Telegram's own link preview is left on so a photo can appear.

## 5. When it fires

Same rules as every channel: no messages on baseline; later ScanRuns honour the route triggers; Dismissed Homes are silent.

## Troubleshooting

| Symptom | Check |
|---|---|
| `Telegram bot token is not configured` | Env on *both* api and worker; containers recreated after the edit |
| `chat not found` / 400 | You never pressed Start; wrong chat id; bot kicked from the group |
| Test works, hunts do not | Baseline, or the event is not in the trigger list |
| Bot replies to `/start` but Domolov never posts | Destination is a username — it must be the numeric chat id |
