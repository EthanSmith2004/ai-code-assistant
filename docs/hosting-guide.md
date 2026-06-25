# Hosting CodeSight on Render + Supabase (free tier)

This guide deploys CodeSight as two Render web services (the API and the Blazor
client) backed by a free Supabase PostgreSQL database. Everything below uses
free tiers only.

**What works in the cloud:** the bundled **demo codebases** (ASP.NET, Express,
FastAPI, Gin, Spring) — they ship inside the API image and are scannable online.
**What does not:** the *"Your own codebase…"* path option, because the server
can't read a visitor's local disk. That's expected — the demos are the showcase.

---

## 0. Prerequisites

- The repo pushed to **GitHub** (or GitLab) — see step 1.
- A free **Supabase** account → https://supabase.com
- A free **Render** account → https://render.com
- A free **Groq** API key → https://console.groq.com (for the AI explanations)

---

## 1. Commit & push everything

Render builds from your git repo, so all of the work (including the `samples/`
demo projects) must be committed and pushed.

```bash
git add -A
git commit -m "Prepare CodeSight for Render + Supabase hosting"
git push
```

> Make sure `samples/` is included (`git ls-files samples | wc -l` should be > 0).
> `Dockerfile.api` copies it into the image so the demo dropdown works online.

---

## 2. Create the Supabase database

1. In Supabase, **New project**. Pick a name, a **region close to your Render
   region**, and set a **database password** (save it — you'll need it).
2. Wait for the project to finish provisioning.
3. Go to **Project Settings → Database → Connection string**, and choose the
   **Session pooler** tab (NOT "Direct connection" — that's IPv6-only and Render
   can't reach it; NOT "Transaction pooler" — it breaks EF migrations).
4. It looks like this (Supavisor session pooler, port **5432**):

   ```
   postgresql://postgres.abcdefgh:[YOUR-PASSWORD]@aws-0-eu-central-1.pooler.supabase.com:5432/postgres
   ```

5. Convert it to the **Npgsql** form CodeSight uses (this is the value you'll
   paste into Render):

   ```
   Host=aws-0-eu-central-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.abcdefgh;Password=YOUR-PASSWORD;SSL Mode=Require;Trust Server Certificate=true
   ```

   Replace the host/region, the `postgres.abcdefgh` username, and the password
   with your own. No tables to create — the API builds the schema on first start
   (`Database.MigrateAsync()`).

---

## 3. Deploy to Render with the blueprint

1. In Render: **New → Blueprint**, and connect your GitHub repo. Render reads
   [`render.yaml`](../render.yaml) and proposes two services:
   `codesight-api` and `codesight-client`.
2. Click **Apply**. The first build will start (it'll likely fail health checks
   until you set the secrets in the next step — that's fine).

### Set the API secrets

Open the **codesight-api** service → **Environment**, and fill the three
`sync: false` variables:

| Key | Value |
|---|---|
| `ConnectionStrings__CodeSightDatabase` | the Npgsql string from step 2.5 |
| `Jwt__SigningKey` | any long random string, 32+ chars |
| `Groq__ApiKey` | your `gsk_…` key from console.groq.com |

`Samples__Path` is already `/app/samples` and `ASPNETCORE_ENVIRONMENT` is
`Production` from the blueprint — leave them.

Click **Save** — Render redeploys the API. Watch the logs; on success you'll see
the EF migrations apply and `Now listening on…`. Note the API's public URL, e.g.
`https://codesight-api.onrender.com`.

### Point the client at the API

Open the **codesight-client** service → **Environment** → set:

| Key | Value |
|---|---|
| `ApiBaseUrl` | the API's real URL from above, e.g. `https://codesight-api.onrender.com` |

Save → the client redeploys.

---

## 4. Verify

1. Open the client URL (e.g. `https://codesight-client.onrender.com`).
2. **Register** an account (the Supabase DB starts empty).
3. Go to **Graph Explorer**, pick a **Demo codebase** (e.g. *Task Manager ·
   FastAPI*), and confirm it scans into endpoints + flows, and that
   **"Explain this flow"** returns a real Groq explanation.

---

## 5. Free-tier notes & gotchas

- **Cold starts:** Render free web services sleep after ~15 min idle and take
  ~30–60s to wake. The first request after a nap is slow — that's normal.
- **Connection string:** must be the **Session pooler** (port 5432). The direct
  `db.<ref>.supabase.co` host is IPv6-only and Render can't reach it; the
  transaction pooler (port 6543) disables prepared statements and breaks EF.
- **Migrations** run automatically on every API start; they're idempotent.
- **Secrets:** the values marked `sync: false` are NOT in git — set them only in
  the Render dashboard. Rotate the JWT/Groq keys if they were ever shared.
- **Region:** keep the Supabase region and the Render region on the same
  continent to keep DB latency low.
- **Custom codebase scanning** is local-only by design (the cloud server has no
  access to a visitor's machine); the bundled demos cover the online experience.

---

## Local development

For running locally (portable Postgres, demo data, etc.) see
[local-setup.md](local-setup.md).
