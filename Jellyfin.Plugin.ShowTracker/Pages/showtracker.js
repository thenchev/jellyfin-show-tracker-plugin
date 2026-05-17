(function () {
    'use strict';

    // State
    let scanResult = null;
    let currentFilter = 'all';
    let currentSort = 'most-missing';
    let searchQuery = '';

    // DOM Elements
    const loadingEl = document.getElementById('stLoading');
    const contentEl = document.getElementById('stContent');
    const metaEl = document.getElementById('stMeta');
    const summaryEl = document.getElementById('stSummary');
    const showListEl = document.getElementById('stShowList');
    const emptyEl = document.getElementById('stEmpty');
    const searchEl = document.getElementById('stSearch');
    const filterEl = document.getElementById('stFilter');
    const sortEl = document.getElementById('stSort');
    const scanBtn = document.getElementById('stScanBtn');
    const clearBtn = document.getElementById('stClearFilter');

    // Jellyfin serializes our ASP.NET models as PascalCase. Normalize to
    // camelCase so the rest of this file can use idiomatic JS names.
    function normalizeKeys(v) {
        if (Array.isArray(v)) return v.map(normalizeKeys);
        if (v && typeof v === 'object' && v.constructor === Object) {
            var out = {};
            for (var k in v) {
                if (Object.prototype.hasOwnProperty.call(v, k)) {
                    out[k.charAt(0).toLowerCase() + k.slice(1)] = normalizeKeys(v[k]);
                }
            }
            return out;
        }
        return v;
    }

    // API helpers
    function getApiUrl(path) {
        return ApiClient.getUrl(path);
    }

    function apiFetch(path) {
        return ApiClient.getJSON(getApiUrl(path)).then(normalizeKeys);
    }

    function apiPost(path) {
        return ApiClient.ajax({
            url: getApiUrl(path),
            type: 'POST',
            dataType: 'json'
        });
    }

    // Initialize
    async function init() {
        try {
            scanResult = await apiFetch('ShowTracker/Results');
            renderAll();
        } catch (err) {
            if (err && (err.status === 404 || (err.statusCode && err.statusCode === 404))) {
                showEmpty();
            } else {
                console.error('ShowTracker: Failed to load results', err);
                showEmpty();
            }
        }
    }

    function showEmpty() {
        loadingEl.style.display = 'none';
        contentEl.style.display = 'block';
        emptyEl.style.display = 'block';
        showListEl.style.display = 'none';
        summaryEl.innerHTML = '';
    }

    function renderAll() {
        loadingEl.style.display = 'none';
        contentEl.style.display = 'block';
        emptyEl.style.display = 'none';
        showListEl.style.display = 'flex';

        renderMeta();
        renderSummary();
        renderShowList();
    }

    // Render metadata
    function renderMeta() {
        if (!scanResult) return;
        const date = new Date(scanResult.scanCompletedUtc);
        const formatted = date.toLocaleDateString(undefined, {
            year: 'numeric', month: 'short', day: 'numeric',
            hour: '2-digit', minute: '2-digit'
        });
        metaEl.textContent = 'Last scan: ' + formatted;
    }

    // Render summary cards
    function renderSummary() {
        if (!scanResult) return;

        const cards = [
            { value: scanResult.totalShows || 0, label: 'Shows Tracked', cls: 'blue' },
            { value: scanResult.completeShows || 0, label: 'Complete', cls: 'green' },
            { value: scanResult.totalMissingSeasons || 0, label: 'Missing Seasons', cls: 'orange' },
            { value: scanResult.totalMissingEpisodes || 0, label: 'Missing Episodes', cls: 'red' }
        ];

        summaryEl.innerHTML = cards.map(function (c) {
            return '<div class="st-card">' +
                '<div class="st-card-value ' + c.cls + '">' + c.value + '</div>' +
                '<div class="st-card-label">' + c.label + '</div>' +
                '</div>';
        }).join('');
    }

    // Filter & sort shows
    function getFilteredShows() {
        if (!scanResult || !scanResult.shows) return [];

        var shows = scanResult.shows.slice();

        // Filter
        if (currentFilter === 'missing') {
            shows = shows.filter(function (s) { return !s.isComplete; });
        } else if (currentFilter === 'complete') {
            shows = shows.filter(function (s) { return s.isComplete; });
        } else if (currentFilter === 'running') {
            shows = shows.filter(function (s) { return s.status === 'Running'; });
        } else if (currentFilter === 'ended') {
            shows = shows.filter(function (s) { return s.status === 'Ended'; });
        }

        // Search
        if (searchQuery) {
            var q = searchQuery.toLowerCase();
            shows = shows.filter(function (s) {
                return s.showName.toLowerCase().indexOf(q) !== -1;
            });
        }

        // Sort
        if (currentSort === 'most-missing') {
            shows.sort(function (a, b) { return b.totalMissingCount - a.totalMissingCount; });
        } else if (currentSort === 'name-asc') {
            shows.sort(function (a, b) { return a.showName.localeCompare(b.showName); });
        } else if (currentSort === 'name-desc') {
            shows.sort(function (a, b) { return b.showName.localeCompare(a.showName); });
        } else if (currentSort === 'complete-first') {
            shows.sort(function (a, b) {
                if (a.isComplete !== b.isComplete) return a.isComplete ? -1 : 1;
                return a.showName.localeCompare(b.showName);
            });
        }

        return shows;
    }

    // Render show list
    function renderShowList() {
        var shows = getFilteredShows();

        if (shows.length === 0) {
            showListEl.innerHTML = '<div class="st-empty"><p>No shows match your filters.</p></div>';
            return;
        }

        showListEl.innerHTML = shows.map(function (show) {
            return renderShow(show);
        }).join('');

        // Attach click handlers
        var headers = showListEl.querySelectorAll('.st-show-header');
        headers.forEach(function (header) {
            header.addEventListener('click', function () {
                var parent = header.closest('.st-show');
                parent.classList.toggle('expanded');
            });
        });

        // Attach dismiss buttons
        var dismissBtns = showListEl.querySelectorAll('.st-dismiss-btn');
        dismissBtns.forEach(function (btn) {
            btn.addEventListener('click', function (e) {
                e.stopPropagation();
                dismissShow(btn.dataset.id, btn);
            });
        });
    }

    function renderShow(show) {
        var indicatorClass = show.isComplete ? 'complete'
            : show.totalMissingCount > 5 ? 'many-missing' : 'few-missing';

        var badgeClass = show.isComplete ? 'complete'
            : show.totalMissingCount > 5 ? 'missing' : 'few';

        var badgeText = show.isComplete ? '✓ Complete'
            : show.missingSeasons.length > 0
                ? show.missingSeasons.length + ' season(s), ' + show.missingEpisodes.length + ' ep(s) missing'
                : show.missingEpisodes.length + ' episode(s) missing';

        var posterHtml = show.imageUrl
            ? '<img class="st-show-poster" src="' + escapeHtml(show.imageUrl) + '" alt="" loading="lazy">'
            : '<div class="st-show-poster"></div>';

        var statusText = (show.status || 'Unknown') +
            ' · ' + show.totalEpisodesLocal + '/' + show.totalEpisodesOnTvMaze + ' eps' +
            ' · ' + show.totalSeasonsLocal + '/' + show.totalSeasonsOnTvMaze + ' seasons';

        var matchHint = describeMatchMode(show.matchMode, show.matchConfidence);

        // Details
        var detailsHtml = '';

        if (matchHint) {
            detailsHtml += '<div class="st-detail-section">' +
                '<h4>Match info</h4>' +
                '<div style="color:#aaa;font-size:0.9em;">' + escapeHtml(matchHint) + '</div>' +
                '</div>';
        }

        if (show.missingSeasons.length > 0) {
            detailsHtml += '<div class="st-detail-section">' +
                '<h4>Missing Seasons</h4>' +
                '<div class="st-episode-grid">' +
                show.missingSeasons.map(function (s) {
                    return '<span class="st-episode-tag">Season ' + s.seasonNumber +
                        ' (' + s.expectedEpisodeCount + ' eps)</span>';
                }).join('') +
                '</div></div>';
        }

        if (show.missingEpisodes.length > 0) {
            var grouped = groupEpisodesBySeason(show.missingEpisodes);
            detailsHtml += '<div class="st-detail-section">' +
                '<h4>Missing Episodes</h4>';

            Object.keys(grouped).sort(function (a, b) { return parseInt(a) - parseInt(b); }).forEach(function (season) {
                detailsHtml += '<div style="margin-bottom:0.5em;">' +
                    '<span style="color:#aaa;font-size:0.82em;">Season ' + season + ':</span> ' +
                    '<div class="st-episode-grid" style="margin-top:0.3em;">' +
                    grouped[season].map(function (ep) {
                        var cls = ep.hasAired ? '' : ' unaired';
                        var title = ep.name + (ep.airdate ? ' (' + ep.airdate + ')' : '') +
                            (ep.hasAired ? '' : ' — not yet aired');
                        return '<span class="st-episode-tag' + cls + '" title="' + escapeHtml(title) + '">' +
                            ep.episodeCode + '</span>';
                    }).join('') +
                    '</div></div>';
            });

            detailsHtml += '</div>';
        }

        detailsHtml += '<div class="st-detail-actions">' +
            '<a class="st-link" href="' + escapeHtml(show.tvMazeUrl) + '" target="_blank" rel="noopener">View on TVMaze ↗</a>' +
            (show.isComplete ? '' :
                '<button class="st-dismiss-btn" data-id="' + escapeHtml(show.jellyfinId) + '">Dismiss</button>') +
            '</div>';

        return '<div class="st-show" data-id="' + escapeHtml(show.jellyfinId) + '">' +
            '<div class="st-show-header">' +
            '<span class="st-indicator ' + indicatorClass + '"></span>' +
            posterHtml +
            '<div class="st-show-info">' +
            '<div class="st-show-name">' + escapeHtml(show.showName) + '</div>' +
            '<div class="st-show-status">' + escapeHtml(statusText) + '</div>' +
            '</div>' +
            '<span class="st-show-badge ' + badgeClass + '">' + escapeHtml(badgeText) + '</span>' +
            '<span class="st-chevron">▶</span>' +
            '</div>' +
            '<div class="st-show-details">' + detailsHtml + '</div>' +
            '</div>';
    }

    function describeMatchMode(mode, confidence) {
        if (!mode || mode === 'BySeasonEpisode') return '';
        var pct = typeof confidence === 'number' ? Math.round(confidence * 100) + '% confident — ' : '';
        switch (mode) {
            case 'WithNameOrAirdateFallback':
                return pct + 'Some episodes matched by title/airdate (season numbering differs from TVMaze).';
            case 'ByAbsoluteNumbering':
                return pct + 'Library uses absolute episode numbering; TVMaze season layout was flattened for comparison.';
            case 'PossiblyWrongShow':
                return 'Local episode count is much larger than TVMaze — the TVMaze match is probably wrong (check the show\'s TheTVDB/IMDB ID).';
            case 'NoMatch':
                return 'No episodes lined up with TVMaze — probable wrong-show match or completely renumbered library.';
            default:
                return pct + 'Match mode: ' + mode;
        }
    }

    function groupEpisodesBySeason(episodes) {
        var grouped = {};
        episodes.forEach(function (ep) {
            var key = ep.seasonNumber.toString();
            if (!grouped[key]) grouped[key] = [];
            grouped[key].push(ep);
        });
        return grouped;
    }

    // Scan
    async function triggerScan() {
        scanBtn.disabled = true;
        scanBtn.textContent = '⏳ Scanning...';

        try {
            await apiPost('ShowTracker/Scan');

            // Poll for completion
            var attempts = 0;
            var maxAttempts = 120; // 10 minutes max
            var pollInterval = 5000;

            var pollTimer = setInterval(async function () {
                attempts++;
                if (attempts > maxAttempts) {
                    clearInterval(pollTimer);
                    scanBtn.disabled = false;
                    scanBtn.textContent = '🔄 Scan Now';
                    return;
                }

                try {
                    var result = await apiFetch('ShowTracker/Results');
                    if (result && result.scanCompletedUtc) {
                        var scanTime = new Date(result.scanCompletedUtc).getTime();
                        var now = Date.now();
                        // If the result is from the last 2 minutes, consider it fresh
                        if (now - scanTime < 120000) {
                            clearInterval(pollTimer);
                            scanResult = result;
                            renderAll();
                            scanBtn.disabled = false;
                            scanBtn.textContent = '🔄 Scan Now';
                        }
                    }
                } catch (e) {
                    // Keep polling
                }
            }, pollInterval);

        } catch (err) {
            console.error('ShowTracker: Failed to trigger scan', err);
            scanBtn.disabled = false;
            scanBtn.textContent = '🔄 Scan Now';
        }
    }

    // Dismiss
    async function dismissShow(jellyfinId, btn) {
        try {
            await apiPost('ShowTracker/Dismiss/' + encodeURIComponent(jellyfinId));
            // Remove from local state
            if (scanResult && scanResult.shows) {
                scanResult.shows = scanResult.shows.filter(function (s) {
                    return s.jellyfinId !== jellyfinId;
                });
                renderAll();
            }
        } catch (err) {
            console.error('ShowTracker: Failed to dismiss show', err);
        }
    }

    // Escape HTML
    function escapeHtml(str) {
        if (!str) return '';
        var div = document.createElement('div');
        div.appendChild(document.createTextNode(str));
        return div.innerHTML;
    }

    // Event Listeners
    searchEl.addEventListener('input', function () {
        searchQuery = searchEl.value;
        clearBtn.style.display = searchQuery ? 'inline-block' : 'none';
        renderShowList();
    });

    filterEl.addEventListener('change', function () {
        currentFilter = filterEl.value;
        renderShowList();
    });

    sortEl.addEventListener('change', function () {
        currentSort = sortEl.value;
        renderShowList();
    });

    scanBtn.addEventListener('click', triggerScan);

    clearBtn.addEventListener('click', function () {
        searchEl.value = '';
        searchQuery = '';
        filterEl.value = 'all';
        currentFilter = 'all';
        sortEl.value = 'most-missing';
        currentSort = 'most-missing';
        clearBtn.style.display = 'none';
        renderShowList();
    });

    // Start
    init();
})();
