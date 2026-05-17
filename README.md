# 📺 Jellyfin Show Tracker Plugin

A Jellyfin plugin that scans your TV library and cross-references it with [TVMaze](https://www.tvmaze.com) to identify missing episodes and seasons.

## Features

- **Automatic scanning** — Runs nightly (configurable) to detect missing episodes and new seasons
- **Dashboard page** — View a full report with summary cards, search, filtering, and sorting
- **REST API** — Expose scan results as JSON for external integrations (Sonarr, scripts, bots)
- **Smart matching** — Uses TheTVDB and IMDB IDs from your Jellyfin metadata to find exact matches on TVMaze
- **Respectful API usage** — Built-in rate limiting (≤20 calls/10s), response caching (12h default), and retry with exponential back-off
- **Configurable** — Include/exclude specials, unaired episodes, specific libraries, or individual shows

## Screenshots

After installing, navigate to **Dashboard → Plugins → Show Tracker** to see the report:

- **Summary cards** show total tracked shows, complete shows, missing seasons, and missing episodes at a glance
- **Color-coded indicators** — 🔴 many missing, 🟡 few missing, 🟢 complete
- **Expandable show details** — Click any show to see exactly which episodes are missing
- **Search and filter** — Find specific shows, filter by status (missing/complete/running/ended)

## Installation

### Prerequisites

- Jellyfin Server **10.11.x** or later
- Your TV shows should have metadata from **TheTVDB** or **IMDB** (this is how the plugin matches shows to TVMaze)

### Method 1: Plugin Repository (Recommended — auto-updates)

This is the easiest path. Jellyfin will check the repository URL periodically and notify you when a new version is available.

1. Open Jellyfin → **Dashboard → Plugins → Repositories**
2. Click **+** and enter:
   - **Repository Name**: `Show Tracker`
   - **Repository URL**: `https://raw.githubusercontent.com/thenchev/jellyfin-show-tracker-plugin/main/manifest.json`
3. Save, then go to **Dashboard → Plugins → Catalog**
4. Find **Show Tracker** under *General* and click **Install**
5. Restart Jellyfin when prompted
6. Updates: whenever a new version is published, the Catalog will show an **Update** button.

### Method 2: Manual install (one-off DLL drop)

Use this if you don't want to add a repository, or to test a build that isn't released yet.

1. **Download** the latest `showtracker_<version>.zip` from the [Releases](../../releases) page and extract `Jellyfin.Plugin.ShowTracker.dll`.

2. **Find your Jellyfin plugins directory:**

   | Platform | Default Path |
   |---|---|
   | **Linux (bare-metal)** | `/var/lib/jellyfin/plugins/` |
   | **Linux (Docker)** | Your volume mount, typically `/config/plugins/` inside the container |
   | **Windows** | `C:\Users\{YourUser}\AppData\Local\jellyfin\plugins\` |
   | **macOS** | `~/.local/share/jellyfin/plugins/` |

   > **Docker users**: If you mount a config volume like `-v /path/to/config:/config`, the plugins directory is at `/path/to/config/plugins/` on your host.

3. Create a subfolder `ShowTracker_<version>` (e.g. `ShowTracker_1.0.0.0`) inside the plugins directory and drop the DLL into it.

4. **Restart Jellyfin** (`sudo systemctl restart jellyfin` or `docker restart jellyfin`) and verify it appears under **Dashboard → Plugins**.

### Building from Source

```bash
git clone https://github.com/thenchev/jellyfin-show-tracker-plugin.git
cd jellyfin-show-tracker-plugin

dotnet test  Jellyfin.Plugin.ShowTracker.sln --configuration Release
dotnet build Jellyfin.Plugin.ShowTracker/Jellyfin.Plugin.ShowTracker.csproj --configuration Release

# Output: Jellyfin.Plugin.ShowTracker/bin/Release/net9.0/Jellyfin.Plugin.ShowTracker.dll
```

## Releasing a new version

The repo ships a GitHub Actions workflow (`.github/workflows/release.yml`) that runs on tag push:

1. Add a section to `CHANGELOG.md` titled `## v<new-version>` (e.g. `## v1.0.1.0`).
2. Commit and push to `main`.
3. Tag and push the tag:
   ```bash
   git tag v1.0.1.0
   git push origin v1.0.1.0
   ```
4. The workflow will:
   - Run `dotnet test`
   - Build the plugin with the tag version stamped into the assembly
   - Create a GitHub Release containing `showtracker_<version>.zip`
   - Prepend the new version to `manifest.json` (with MD5 checksum and changelog) and commit it back to `main`
5. Within a few minutes, every Jellyfin instance subscribed to the repository will see the update in the Catalog.

> **Note**: tag names must start with `v` (e.g. `v1.0.1`, `v2.0`). Versions shorter than 4 segments are zero-padded (`1.0.1` → `1.0.1.0`).

## Usage

### Running a Scan

**Option A — Automatic:** The plugin runs a scan every night at 3:00 AM by default. You can change the schedule in **Dashboard → Scheduled Tasks → Check Missing Episodes (TVMaze)**.

**Option B — Manual:** 
- Go to **Dashboard → Scheduled Tasks**, find "Check Missing Episodes (TVMaze)" and click **Run**
- Or use the **Scan Now** button on the plugin's dashboard page
- Or call the REST API: `POST /ShowTracker/Scan`

### Viewing Results

Navigate to **Dashboard → Plugins → Show Tracker** to see the full report. The dashboard shows:

- **Summary cards** with aggregate counts
- **Filterable list** of all shows with missing episode details
- **Expandable entries** — click any show to see the specific missing episodes

### REST API

The plugin exposes REST endpoints for external integrations:

| Endpoint | Method | Auth | Description |
|---|---|---|---|
| `/ShowTracker/Results` | GET | User | Full scan results |
| `/ShowTracker/Results/{seriesId}` | GET | User | Results for one show |
| `/ShowTracker/Summary` | GET | User | Aggregate counts only |
| `/ShowTracker/Scan` | POST | Admin | Trigger an immediate scan |
| `/ShowTracker/Dismiss/{seriesId}` | POST | Admin | Dismiss a show from reports |
| `/ShowTracker/Dismiss/{seriesId}` | DELETE | Admin | Un-dismiss a show |

### Configuration

The plugin has the following settings (editable in `plugin.xml` or via the Jellyfin API):

| Setting | Default | Description |
|---|---|---|
| `CacheDurationHours` | 12 | How long TVMaze API responses are cached |
| `IncludeSpecials` | false | Include special episodes in the comparison |
| `IncludeUnaired` | false | Include unaired future episodes as "missing" |
| `ExcludedLibraries` | [] | Library names to skip during scanning |
| `DismissedShows` | [] | Jellyfin series IDs to ignore |

## TVMaze API Compliance

This plugin follows all [TVMaze API guidelines](https://www.tvmaze.com/api):

- **Rate limiting**: Maximum 18 calls per 10 seconds (below their stated minimum of 20/10s)
- **HTTP 429 handling**: Automatic exponential back-off and retry
- **Caching**: 12-hour default cache for API responses (memory + disk)
- **User-Agent**: Custom header identifying the plugin
- **Connection management**: Single HttpClient instance, no idle connections
- **Attribution**: TVMaze is credited with a link on the dashboard page per CC BY-SA 4.0

## Data & Privacy

- All data is stored locally on your Jellyfin server
- The plugin only makes outbound HTTP requests to `api.tvmaze.com`
- No personal data is sent — only TheTVDB/IMDB IDs are used for lookups
- Cache files are stored in `{jellyfin-config}/plugins/configurations/ShowTracker/cache/`
- Scan results are stored in `{jellyfin-config}/plugins/configurations/ShowTracker/scan_results.json`

## License

Data from [TVMaze](https://www.tvmaze.com) is licensed under [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/).

This plugin is licensed under the [GPL-3.0 License](LICENSE).
