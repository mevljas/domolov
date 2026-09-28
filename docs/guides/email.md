# Email notifications

Domolov sends plain-text mail over SMTP. There is no inbound mailbox and no HTML template.

## 1. Configure SMTP

In `.env`:

```dotenv
DOMOLOV_SMTP_HOST=smtp.example.com
DOMOLOV_SMTP_PORT=587
DOMOLOV_SMTP_USER=domolov@example.com
DOMOLOV_SMTP_PASSWORD=app-password-not-your-login
DOMOLOV_SMTP_FROM=domolov@example.com
```

- Port **587** with STARTTLS is the default and the only combination we use (`EnableSsl = true`).
- `DOMOLOV_SMTP_FROM` must be an address the server will accept as a sender.
- Leave `DOMOLOV_SMTP_USER` empty only if the server allows unauthenticated relay from your host (rare, and usually a mistake).

Recreate api and worker after editing: `docker compose up -d api worker`.

### Common providers

| Provider | Host | Notes |
|---|---|---|
| Mailgun, Postmark, SES SMTP | their SMTP hostname | Use the SMTP credential they give you, not the API key |
| Gmail | `smtp.gmail.com` | Needs an [app password](https://support.google.com/accounts/answer/185833); 2FA must be on |
| iCloud | `smtp.mail.me.com` | App-specific password |

## 2. Attach a route

1. Open the Watch → **Notifications**.
2. Channel: **Email**.
3. Destination: the **To** address (one address per route; add more routes for more people).
4. Pick triggers. Save. **Send test**.

Subject line is `[Domolov] <title>`. The body is the listing blurb, the listing URL, an **Open in Domolov** line when `DOMOLOV_PUBLIC_URL` is set, then the usual metadata (location, type, rooms, size, year, price).

## 3. When it fires

Baseline is silent. Later ScanRuns honour the route. One email per matching event — there is no daily digest (that is on the [later list](../improvements.md)).

## Troubleshooting

| Symptom | Check |
|---|---|
| `SMTP is not configured` | `DOMOLOV_SMTP_HOST` and `DOMOLOV_SMTP_FROM` are both set on api *and* worker |
| Timeout / connection refused | Port, firewall, and that 587 is not blocked outbound |
| 535 / authentication failed | App password, not the account password; username is the full address |
| Mail is accepted but never arrives | Spam folder; `FROM` not allowed on that account; provider signing (SPF/DKIM) |
| Test works, hunts do not | Baseline, or the trigger list |
