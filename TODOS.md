# TODOs

## UX

- **[ux] Course list (and likely other `p-datatable-scrollable` grids) hides the
  操作 action column past the viewport edge with no visible scroll affordance.**
  Flagged by `/qa` (2026-07-17) while testing the Course PDF feature — the column is
  reachable via horizontal scroll on `.p-datatable-table-container`, but nothing
  hints it's there at rest. See `.gstack/qa-reports/qa-report-localhost-4200-2026-07-17.md`.

## Security

- **[security] Add `app.UseHttpsRedirection()` / HSTS in `Program.cs`.** Flagged by `/cso`
  (2026-07-17, both the daily and full-project runs) — no deploy target exists yet, so it's
  sub-threshold today (nothing to exploit locally), but should land before any real deployment.
  See `.gstack/security-reports/2026-07-17-114801.json`.
