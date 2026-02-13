namespace HelloWorld.Api;

public static class DashboardHtml
{
    public const string Content = """
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>dotweave Dashboard</title>
<style>
  :root {
    --bg: #0f1117;
    --surface: #181b23;
    --surface2: #1e222d;
    --border: #2a2e3a;
    --text: #e0e0e6;
    --text-dim: #8b8fa3;
    --accent: #6c8cff;
    --accent2: #4ecdc4;
    --green: #4ade80;
    --red: #f87171;
    --orange: #fbbf24;
    --font: 'SF Mono', 'Cascadia Code', 'Fira Code', 'JetBrains Mono', monospace;
  }
  * { margin: 0; padding: 0; box-sizing: border-box; }
  body {
    background: var(--bg);
    color: var(--text);
    font-family: var(--font);
    font-size: 13px;
    line-height: 1.5;
  }
  .header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 16px 24px;
    border-bottom: 1px solid var(--border);
    background: var(--surface);
  }
  .header h1 {
    font-size: 16px;
    font-weight: 600;
    color: var(--accent);
  }
  .header .controls {
    display: flex;
    gap: 12px;
    align-items: center;
  }
  .header .status {
    width: 8px; height: 8px;
    border-radius: 50%;
    background: var(--green);
    animation: pulse 2s infinite;
  }
  @keyframes pulse {
    0%, 100% { opacity: 1; }
    50% { opacity: 0.4; }
  }
  .header button {
    background: var(--surface2);
    border: 1px solid var(--border);
    color: var(--text);
    padding: 6px 14px;
    border-radius: 6px;
    cursor: pointer;
    font-family: var(--font);
    font-size: 12px;
    transition: background 0.15s;
  }
  .header button:hover { background: var(--border); }
  .header button.active { background: var(--accent); color: #fff; border-color: var(--accent); }

  .container {
    max-width: 1400px;
    margin: 0 auto;
    padding: 20px 24px;
  }

  /* Tabs */
  .tabs {
    display: flex;
    gap: 0;
    margin-bottom: 20px;
    border-bottom: 1px solid var(--border);
  }
  .tab {
    padding: 10px 20px;
    cursor: pointer;
    color: var(--text-dim);
    border-bottom: 2px solid transparent;
    transition: all 0.15s;
    font-size: 13px;
  }
  .tab:hover { color: var(--text); }
  .tab.active { color: var(--accent); border-bottom-color: var(--accent); }

  .panel { display: none; }
  .panel.active { display: block; }

  /* Metrics cards */
  .metrics-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(340px, 1fr));
    gap: 16px;
    margin-bottom: 20px;
  }
  .metric-card {
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: 10px;
    padding: 18px;
    transition: border-color 0.15s;
  }
  .metric-card:hover { border-color: var(--accent); }
  .metric-card .name {
    font-size: 12px;
    color: var(--text-dim);
    margin-bottom: 4px;
    text-transform: uppercase;
    letter-spacing: 0.5px;
  }
  .metric-card .description {
    font-size: 11px;
    color: var(--text-dim);
    margin-bottom: 12px;
    opacity: 0.7;
  }
  .metric-card .value {
    font-size: 28px;
    font-weight: 700;
    color: var(--text);
    margin-bottom: 2px;
  }
  .metric-card .unit {
    font-size: 11px;
    color: var(--text-dim);
  }
  .metric-card .data-points {
    margin-top: 12px;
    display: flex;
    flex-direction: column;
    gap: 6px;
  }
  .data-point {
    display: flex;
    justify-content: space-between;
    align-items: center;
    background: var(--surface2);
    padding: 8px 12px;
    border-radius: 6px;
    font-size: 12px;
  }
  .data-point .tag {
    display: inline-block;
    background: var(--border);
    padding: 2px 8px;
    border-radius: 4px;
    font-size: 11px;
    margin-right: 6px;
  }
  .tag-ok { color: var(--green); }
  .tag-error { color: var(--red); }
  .data-point .dp-value {
    font-weight: 600;
    color: var(--accent2);
  }

  /* Histogram bar */
  .hist-bar-container {
    margin-top: 6px;
    display: flex;
    align-items: center;
    gap: 8px;
  }
  .hist-bar {
    flex: 1;
    height: 6px;
    background: var(--surface2);
    border-radius: 3px;
    overflow: hidden;
  }
  .hist-bar-fill {
    height: 100%;
    background: linear-gradient(90deg, var(--accent), var(--accent2));
    border-radius: 3px;
    transition: width 0.3s;
  }

  /* Traces table */
  .traces-table {
    width: 100%;
    border-collapse: collapse;
  }
  .traces-table th {
    text-align: left;
    padding: 10px 12px;
    color: var(--text-dim);
    font-weight: 500;
    font-size: 11px;
    text-transform: uppercase;
    letter-spacing: 0.5px;
    border-bottom: 1px solid var(--border);
    background: var(--surface);
    position: sticky;
    top: 0;
    z-index: 1;
  }
  .traces-table td {
    padding: 10px 12px;
    border-bottom: 1px solid var(--border);
    font-size: 12px;
    vertical-align: top;
  }
  .traces-table tr { transition: background 0.1s; }
  .traces-table tr:hover { background: var(--surface2); }
  .traces-table tr.expandable { cursor: pointer; }

  .trace-id { color: var(--accent); font-size: 11px; opacity: 0.7; }
  .span-name { font-weight: 600; }
  .duration { color: var(--accent2); font-weight: 600; }
  .status-ok { color: var(--green); }
  .status-error { color: var(--red); }
  .status-unset { color: var(--text-dim); }
  .source-badge {
    display: inline-block;
    background: var(--surface2);
    border: 1px solid var(--border);
    padding: 2px 8px;
    border-radius: 4px;
    font-size: 11px;
    color: var(--text-dim);
  }

  /* Trace detail row */
  .trace-detail {
    display: none;
    background: var(--surface);
  }
  .trace-detail.open { display: table-row; }
  .trace-detail td {
    padding: 16px;
  }
  .detail-section { margin-bottom: 12px; }
  .detail-section h4 {
    font-size: 11px;
    text-transform: uppercase;
    color: var(--text-dim);
    margin-bottom: 6px;
    letter-spacing: 0.5px;
  }
  .tag-list {
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
  }
  .tag-item {
    background: var(--surface2);
    border: 1px solid var(--border);
    padding: 4px 10px;
    border-radius: 4px;
    font-size: 11px;
  }
  .tag-key { color: var(--text-dim); }
  .tag-val { color: var(--accent2); }

  .event-item {
    background: var(--surface2);
    border: 1px solid var(--border);
    border-radius: 6px;
    padding: 10px 12px;
    margin-bottom: 6px;
  }
  .event-name { color: var(--red); font-weight: 600; font-size: 12px; }
  .event-time { color: var(--text-dim); font-size: 11px; }
  .event-tags { margin-top: 6px; font-size: 11px; }
  .event-tags pre {
    background: var(--bg);
    padding: 8px;
    border-radius: 4px;
    overflow-x: auto;
    color: var(--text-dim);
    font-size: 11px;
    max-height: 200px;
    overflow-y: auto;
  }

  /* Waterfall visualization */
  .waterfall {
    margin-top: 20px;
    border: 1px solid var(--border);
    border-radius: 10px;
    overflow: hidden;
    background: var(--surface);
  }
  .waterfall-header {
    padding: 12px 16px;
    font-weight: 600;
    font-size: 13px;
    color: var(--accent);
    border-bottom: 1px solid var(--border);
    background: var(--surface2);
  }
  .waterfall-row {
    display: flex;
    align-items: center;
    padding: 8px 16px;
    border-bottom: 1px solid var(--border);
    gap: 12px;
  }
  .waterfall-row:last-child { border-bottom: none; }
  .waterfall-label {
    min-width: 200px;
    font-size: 12px;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }
  .waterfall-bar-area {
    flex: 1;
    height: 20px;
    position: relative;
    background: var(--surface2);
    border-radius: 4px;
  }
  .waterfall-bar {
    position: absolute;
    height: 100%;
    border-radius: 4px;
    min-width: 2px;
    display: flex;
    align-items: center;
    padding: 0 6px;
    font-size: 10px;
    color: #fff;
    white-space: nowrap;
    overflow: hidden;
  }
  .waterfall-bar.span-app {
    background: linear-gradient(90deg, var(--accent), #8ba4ff);
  }
  .waterfall-bar.span-infra {
    background: linear-gradient(90deg, var(--accent2), #7ae0d8);
  }
  .waterfall-duration {
    min-width: 70px;
    text-align: right;
    font-size: 11px;
    color: var(--text-dim);
  }

  .empty-state {
    text-align: center;
    padding: 60px 20px;
    color: var(--text-dim);
  }
  .empty-state h3 { font-size: 16px; margin-bottom: 8px; color: var(--text); }
  .empty-state p { font-size: 13px; }
  .empty-state code {
    display: inline-block;
    background: var(--surface2);
    padding: 2px 8px;
    border-radius: 4px;
    margin-top: 8px;
    color: var(--accent2);
  }

  /* Scrollable area */
  .traces-container {
    max-height: 70vh;
    overflow-y: auto;
    border: 1px solid var(--border);
    border-radius: 10px;
  }
  .traces-container::-webkit-scrollbar { width: 6px; }
  .traces-container::-webkit-scrollbar-track { background: var(--surface); }
  .traces-container::-webkit-scrollbar-thumb { background: var(--border); border-radius: 3px; }

  .refresh-info {
    font-size: 11px;
    color: var(--text-dim);
  }

</style>
</head>
<body>

<div class="header">
  <h1>dotweave Dashboard</h1>
  <div class="controls">
    <span class="refresh-info">Auto-refresh: <span id="interval-label">2s</span></span>
    <div class="status" id="status-dot"></div>
    <button onclick="fetchAll()" title="Refresh now">Refresh</button>
    <button onclick="generateTraffic()" title="Generate sample traffic">Send Requests</button>
  </div>
</div>

<div class="container">
  <div class="tabs">
    <div class="tab active" data-panel="metrics-panel" onclick="switchTab(this)">Metrics</div>
    <div class="tab" data-panel="traces-panel" onclick="switchTab(this)">Traces</div>
    <div class="tab" data-panel="waterfall-panel" onclick="switchTab(this)">Waterfall</div>
  </div>

  <!-- Metrics Panel -->
  <div id="metrics-panel" class="panel active">
    <div id="metrics-grid" class="metrics-grid"></div>
    <div id="metrics-empty" class="empty-state" style="display:none">
      <h3>No metrics yet</h3>
      <p>Send some requests to generate metrics.</p>
      <code>curl http://localhost:5000/hello/world</code>
    </div>
  </div>

  <!-- Traces Panel -->
  <div id="traces-panel" class="panel">
    <div id="traces-content" class="traces-container"></div>
    <div id="traces-empty" class="empty-state" style="display:none">
      <h3>No traces yet</h3>
      <p>Send some requests to generate traces.</p>
      <code>curl http://localhost:5000/hello/world</code>
    </div>
  </div>

  <!-- Waterfall Panel -->
  <div id="waterfall-panel" class="panel">
    <div id="waterfall-content"></div>
    <div id="waterfall-empty" class="empty-state" style="display:none">
      <h3>No traces for waterfall</h3>
      <p>Send some requests to see the waterfall view.</p>
      <code>curl http://localhost:5000/hello/world</code>
    </div>
  </div>
</div>

<script>
let allTraces = [];
let allMetrics = [];

function switchTab(el) {
  document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
  document.querySelectorAll('.panel').forEach(p => p.classList.remove('active'));
  el.classList.add('active');
  document.getElementById(el.dataset.panel).classList.add('active');
}

function formatDuration(ms) {
  if (ms < 1) return '<1 ms';
  if (ms < 1000) return ms.toFixed(1) + ' ms';
  return (ms / 1000).toFixed(2) + ' s';
}

function formatNumber(n) {
  if (n === undefined || n === null) return '0';
  if (Number.isInteger(n)) return n.toLocaleString();
  return n.toFixed(2);
}

function formatTime(iso) {
  if (!iso) return '';
  const d = new Date(iso);
  return d.toLocaleTimeString('en-US', { hour12: false, hour: '2-digit', minute: '2-digit', second: '2-digit' })
    + '.' + String(d.getMilliseconds()).padStart(3, '0');
}

function statusClass(status) {
  if (!status) return 'status-unset';
  const s = status.toLowerCase();
  if (s === 'ok') return 'status-ok';
  if (s === 'error') return 'status-error';
  return 'status-unset';
}

// ---------- Metrics rendering ----------

function renderMetrics(metrics) {
  const grid = document.getElementById('metrics-grid');
  const empty = document.getElementById('metrics-empty');

  if (!metrics || metrics.length === 0) {
    grid.innerHTML = '';
    empty.style.display = 'block';
    return;
  }
  empty.style.display = 'none';

  // Group by base metric name (strip .calls / .duration suffix)
  const grouped = {};
  metrics.forEach(m => {
    const base = m.name.replace(/\.(calls|duration|errors)$/, '');
    if (!grouped[base]) grouped[base] = {};
    if (m.name.endsWith('.calls')) grouped[base].calls = m;
    else if (m.name.endsWith('.duration')) grouped[base].duration = m;
    else grouped[base][m.name] = m;
  });

  let html = '';

  for (const [baseName, group] of Object.entries(grouped)) {
    // Calls card
    if (group.calls) {
      const m = group.calls;
      const totalOk = m.dataPoints.filter(dp => dp.tags?.status === 'ok').reduce((s, dp) => s + dp.value, 0);
      const totalErr = m.dataPoints.filter(dp => dp.tags?.status === 'error').reduce((s, dp) => s + dp.value, 0);
      const total = totalOk + totalErr;

      html += `<div class="metric-card">
        <div class="name">${escHtml(baseName)} &mdash; Calls</div>
        <div class="description">${escHtml(m.description || '')}</div>
        <div class="value">${formatNumber(total)}</div>
        <div class="unit">total invocations</div>
        <div class="data-points">
          <div class="data-point">
            <span><span class="tag tag-ok">ok</span></span>
            <span class="dp-value">${formatNumber(totalOk)}</span>
          </div>
          <div class="data-point">
            <span><span class="tag tag-error">error</span></span>
            <span class="dp-value">${formatNumber(totalErr)}</span>
          </div>
        </div>
      </div>`;
    }

    // Duration card
    if (group.duration) {
      const m = group.duration;
      let maxVal = 0;
      const points = m.dataPoints.map(dp => {
        const avg = dp.count > 0 ? dp.value / dp.count : 0;
        if (avg > maxVal) maxVal = avg;
        return { tags: dp.tags, sum: dp.value, count: dp.count, avg };
      });

      html += `<div class="metric-card">
        <div class="name">${escHtml(baseName)} &mdash; Duration</div>
        <div class="description">${escHtml(m.description || '')}</div>
        <div class="data-points">`;

      for (const pt of points) {
        const pct = maxVal > 0 ? (pt.avg / maxVal * 100) : 0;
        const tags = Object.entries(pt.tags || {}).map(([k,v]) =>
          `<span class="tag ${v === 'ok' ? 'tag-ok' : v === 'error' ? 'tag-error' : ''}">${escHtml(k)}=${escHtml(v)}</span>`
        ).join('');

        html += `<div class="data-point">
          <span>${tags}</span>
          <span class="dp-value">${formatDuration(pt.avg)} avg (${pt.count || 0}x)</span>
        </div>
        <div class="hist-bar-container">
          <div class="hist-bar"><div class="hist-bar-fill" style="width:${pct}%"></div></div>
        </div>`;
      }
      html += `</div></div>`;
    }

    // Other (non-grouped) metrics
    for (const [key, m] of Object.entries(group)) {
      if (key === 'calls' || key === 'duration') continue;
      html += `<div class="metric-card">
        <div class="name">${escHtml(m.name)}</div>
        <div class="description">${escHtml(m.description || '')} (${escHtml(m.metricType)})</div>
        <div class="data-points">`;
      for (const dp of m.dataPoints) {
        const tags = Object.entries(dp.tags || {}).map(([k,v]) =>
          `<span class="tag">${escHtml(k)}=${escHtml(v)}</span>`
        ).join('');
        html += `<div class="data-point">
          <span>${tags || '<span class="tag">no tags</span>'}</span>
          <span class="dp-value">${formatNumber(dp.value)}${dp.count != null ? ' (' + dp.count + 'x)' : ''}</span>
        </div>`;
      }
      html += `</div></div>`;
    }
  }

  grid.innerHTML = html;
}

// ---------- Traces rendering ----------

function renderTraces(traces) {
  const container = document.getElementById('traces-content');
  const empty = document.getElementById('traces-empty');

  if (!traces || traces.length === 0) {
    container.innerHTML = '';
    empty.style.display = 'block';
    return;
  }
  empty.style.display = 'none';

  // Show newest first
  const sorted = [...traces].reverse();

  let html = `<table class="traces-table">
    <thead><tr>
      <th>Time</th>
      <th>Operation</th>
      <th>Source</th>
      <th>Duration</th>
      <th>Status</th>
      <th>Trace ID</th>
    </tr></thead><tbody>`;

  for (let i = 0; i < sorted.length; i++) {
    const t = sorted[i];
    const hasDetail = (Object.keys(t.tags || {}).length > 0) || (t.events && t.events.length > 0);

    html += `<tr class="${hasDetail ? 'expandable' : ''}" onclick="toggleDetail(${i})">
      <td>${formatTime(t.startTime)}</td>
      <td><span class="span-name">${escHtml(t.displayName || t.operationName)}</span></td>
      <td><span class="source-badge">${escHtml(t.source)}</span></td>
      <td><span class="duration">${formatDuration(t.durationMs)}</span></td>
      <td><span class="${statusClass(t.status)}">${escHtml(t.status)}</span></td>
      <td><span class="trace-id">${escHtml((t.traceId || '').substring(0, 12))}...</span></td>
    </tr>`;

    // Detail row
    html += `<tr class="trace-detail" id="detail-${i}"><td colspan="6">`;

    if (Object.keys(t.tags || {}).length > 0) {
      html += `<div class="detail-section"><h4>Tags</h4><div class="tag-list">`;
      for (const [k, v] of Object.entries(t.tags)) {
        html += `<span class="tag-item"><span class="tag-key">${escHtml(k)}</span>=<span class="tag-val">${escHtml(v)}</span></span>`;
      }
      html += `</div></div>`;
    }

    if (t.events && t.events.length > 0) {
      html += `<div class="detail-section"><h4>Events</h4>`;
      for (const ev of t.events) {
        html += `<div class="event-item">
          <span class="event-name">${escHtml(ev.name)}</span>
          <span class="event-time"> at ${formatTime(ev.timestamp)}</span>`;
        if (Object.keys(ev.tags || {}).length > 0) {
          html += `<div class="event-tags"><pre>${escHtml(JSON.stringify(ev.tags, null, 2))}</pre></div>`;
        }
        html += `</div>`;
      }
      html += `</div>`;
    }

    html += `<div class="detail-section"><h4>IDs</h4>
      <div class="tag-list">
        <span class="tag-item"><span class="tag-key">trace</span>=<span class="tag-val">${escHtml(t.traceId)}</span></span>
        <span class="tag-item"><span class="tag-key">span</span>=<span class="tag-val">${escHtml(t.spanId)}</span></span>
        ${t.parentSpanId ? `<span class="tag-item"><span class="tag-key">parent</span>=<span class="tag-val">${escHtml(t.parentSpanId)}</span></span>` : ''}
      </div>
    </div>`;

    html += `</td></tr>`;
  }

  html += '</tbody></table>';
  container.innerHTML = html;
}

function toggleDetail(idx) {
  const el = document.getElementById('detail-' + idx);
  if (el) el.classList.toggle('open');
}

// ---------- Waterfall rendering ----------

function renderWaterfall(traces) {
  const container = document.getElementById('waterfall-content');
  const empty = document.getElementById('waterfall-empty');

  if (!traces || traces.length === 0) {
    container.innerHTML = '';
    empty.style.display = 'block';
    return;
  }
  empty.style.display = 'none';

  // Group by traceId, pick latest N trace groups
  const groups = {};
  traces.forEach(t => {
    if (!groups[t.traceId]) groups[t.traceId] = [];
    groups[t.traceId].push(t);
  });

  const traceIds = Object.keys(groups).slice(-8).reverse();
  let html = '';

  for (const tid of traceIds) {
    const spans = groups[tid].sort((a, b) => new Date(a.startTime) - new Date(b.startTime));
    if (spans.length === 0) continue;

    const minStart = Math.min(...spans.map(s => new Date(s.startTime).getTime()));
    const maxEnd = Math.max(...spans.map(s => new Date(s.startTime).getTime() + s.durationMs));
    const totalDuration = maxEnd - minStart;

    html += `<div class="waterfall">
      <div class="waterfall-header">
        Trace ${tid.substring(0, 16)}... (${formatDuration(totalDuration)}, ${spans.length} span${spans.length > 1 ? 's' : ''})
      </div>`;

    for (const span of spans) {
      const offset = new Date(span.startTime).getTime() - minStart;
      const leftPct = totalDuration > 0 ? (offset / totalDuration * 100) : 0;
      const widthPct = totalDuration > 0 ? Math.max(0.5, span.durationMs / totalDuration * 100) : 100;
      const isApp = span.source === 'dotweave.Traced';

      html += `<div class="waterfall-row">
        <div class="waterfall-label" title="${escAttr(span.displayName || span.operationName)}">
          ${escHtml(span.displayName || span.operationName)}
        </div>
        <div class="waterfall-bar-area">
          <div class="waterfall-bar ${isApp ? 'span-app' : 'span-infra'}"
               style="left:${leftPct}%;width:${widthPct}%">
            ${widthPct > 10 ? formatDuration(span.durationMs) : ''}
          </div>
        </div>
        <div class="waterfall-duration">${formatDuration(span.durationMs)}</div>
      </div>`;
    }

    html += '</div>';
  }

  container.innerHTML = html;
}

// ---------- Fetch + auto-refresh ----------

async function fetchAll() {
  const dot = document.getElementById('status-dot');
  dot.style.background = 'var(--orange)';

  try {
    const [tracesRes, metricsRes] = await Promise.all([
      fetch('/telemetry/traces'),
      fetch('/telemetry/metrics')
    ]);
    allTraces = await tracesRes.json();
    allMetrics = await metricsRes.json();

    renderMetrics(allMetrics);
    renderTraces(allTraces);
    renderWaterfall(allTraces);

    dot.style.background = 'var(--green)';
  } catch (e) {
    console.error('Fetch error:', e);
    dot.style.background = 'var(--red)';
  }
}

async function generateTraffic() {
  const names = ['alice', 'bob', 'charlie', 'diana', 'eve'];
  const endpoints = ['/hello/', '/hello-async/', '/hello-custom/'];
  const promises = [];

  for (let i = 0; i < 6; i++) {
    const ep = endpoints[i % endpoints.length];
    const name = names[i % names.length];
    promises.push(fetch(ep + name).catch(() => {}));
  }

  await Promise.all(promises);
  // Wait a moment for telemetry to be collected
  setTimeout(fetchAll, 500);
}

function escHtml(s) {
  if (!s) return '';
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function escAttr(s) {
  return escHtml(s);
}

// Initial fetch and start auto-refresh
fetchAll();
setInterval(fetchAll, 2000);
</script>
</body>
</html>
""";
}
