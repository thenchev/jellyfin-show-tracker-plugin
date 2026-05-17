# Changelog

## v1.0.1.1
- Fix: the "Scan Now" button was partially covered by Jellyfin's fixed top-right header — the top and right edges intercepted clicks. The page now reserves padding to clear the AppBar and the Cast/Search/User icon cluster.

## v1.0.1.0
- Fix: dashboard page no longer stuck on "Loading scan results..." — corrected the embedded script's URL, moved inline styles into the page body so Jellyfin's config-page loader doesn't strip them, and normalized PascalCase responses on the client.
- Improvement: smarter episode comparison that handles libraries using a different season layout than TVMaze. Falls back to title/airdate matching, detects absolute (anime-style) numbering, and flags shows where the TVMaze match is likely wrong (e.g. anime vs. live-action remake with the same name).
- New per-show `MatchMode` and `MatchConfidence` fields, surfaced in the dashboard as a "Match info" note.

## v1.0.0.0
- Initial release
- TVMaze integration with rate limiting and caching
- Dashboard page with missing episode reports
- Scheduled nightly scanning
- REST API for external integrations
