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

### Method 1: Manual Install (Recommended)

1. **Download** the latest `Jellyfin.Plugin.ShowTracker.dll` from the [Releases](../../releases) page

2. **Find your Jellyfin plugins directory:**

   | Platform | Default Path |
   |---|---|
   | **Linux (bare-metal)** | `/var/lib/jellyfin/plugins/` |
   | **Linux (Docker)** | Your volume mount, typically `/config/plugins/` inside the container |
   | **Windows** | `C:\Users\{YourUser}\AppData\Local\jellyfin\plugins\` |
   | **macOS** | `~/.local/share/jellyfin/plugins/` |

   > **Docker users**: If you mount a config volume like `-v /path/to/config:/config`, the plugins directory is at `/path/to/config/plugins/` on your host.

3. **Create a subfolder** named `ShowTracker` inside the plugins directory:

   ```bash
   # Linux example
   sudo mkdir -p /var/lib/jellyfin/plugins/ShowTracker

   # Docker example (from host)
   mkdir -p /path/to/config/plugins/ShowTracker
   ```

4. **Copy the DLL** into the `ShowTracker` folder:

   ```bash
   # Linux example
   sudo cp Jellyfin.Plugin.ShowTracker.dll /var/lib/jellyfin/plugins/ShowTracker/

   # Docker example (from host)
   cp Jellyfin.Plugin.ShowTracker.dll /path/to/config/plugins/ShowTracker/
   ```

5. **Restart Jellyfin**:

   ```bash
   # Systemd
   sudo systemctl restart jellyfin

   # Docker
   docker restart jellyfin
   ```

6. **Verify**: Navigate to **Dashboard → Plugins** — "Show Tracker" should appear in the list.

### Method 2: Custom Plugin Repository

1. Go to **Dashboard → Plugins → Repositories**
2. Click **Add** and enter:
   - **Name**: `Show Tracker`
   - **URL**: `https://raw.githubusercontent.com/{your-username}/jellyfin-show-tracker-plugin/main/manifest.json`
3. Go to **Catalog** and find "Show Tracker" under General
4. Click **Install** and **restart** Jellyfin

### Building from Source

```bash
# Clone the repository
git clone https://github.com/{your-username}/jellyfin-show-tracker-plugin.git
cd jellyfin-show-tracker-plugin

# Build the plugin
dotnet build Jellyfin.Plugin.ShowTracker.sln --configuration Release

# The DLL will be at:
# Jellyfin.Plugin.ShowTracker/bin/Release/net9.0/Jellyfin.Plugin.ShowTracker.dll
```

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
