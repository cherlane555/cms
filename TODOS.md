# TODOs

## Security

- **[security] Add `app.UseHttpsRedirection()` / HSTS in `Program.cs`.** Flagged by `/cso`
  (2026-07-17, both the daily and full-project runs) — no deploy target exists yet, so it's
  sub-threshold today (nothing to exploit locally), but should land before any real deployment.
  See `.gstack/security-reports/2026-07-17-114801.json`.
