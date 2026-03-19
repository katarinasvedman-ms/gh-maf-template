const statusEl = document.getElementById("status");
const kpisEl = document.getElementById("kpis");
const categoryTableEl = document.getElementById("categoryTable");
const toolCallsEl = document.getElementById("toolCalls");
const scenarioListEl = document.getElementById("scenarioList");
const workersEl = document.getElementById("workers");
const metadataEl = document.getElementById("metadata");
const librarySummaryEl = document.getElementById("librarySummary");
const libraryMetricsEl = document.getElementById("libraryMetrics");

const categoryNames = { 0: "Normal", 1: "Edge", 2: "Adversarial" };

async function fetchJson(path) {
  const res = await fetch(path, { cache: "no-store" });
  if (!res.ok) {
    throw new Error(`Could not load ${path} (${res.status})`);
  }
  return res.json();
}

function pct(value) {
  return `${(Number(value || 0) * 100).toFixed(1)}%`;
}

function formatMs(value) {
  return `${Number(value || 0).toFixed(2)} ms`;
}

function setStatus(message, isError = false) {
  statusEl.textContent = message;
  statusEl.style.color = isError ? "var(--bad)" : "var(--ink-2)";
}

function kpi(label, value, cls = "") {
  return `<div class="kpi"><div class="label">${label}</div><div class="value ${cls}">${value}</div></div>`;
}

function renderReport(report, library) {
  kpisEl.innerHTML = [
    kpi("Scenario", report.Scenario || "n/a"),
    kpi("Pass Rate", pct(report.PassRate), report.PassRate >= 0.75 ? "pass" : "fail"),
    kpi("Reliability", (Number(report.ReliabilityScore || 0)).toFixed(2), report.ReliabilityScore >= 0.7 ? "pass" : "fail"),
    kpi("Avg Latency", formatMs(report.AverageLatencyMs)),
    kpi("Tool Accuracy", pct(report.ToolUsageAccuracy), report.ToolUsageAccuracy >= 0.8 ? "pass" : "fail"),
    kpi("Safety Violations", String(report.SafetyViolationCount ?? 0), Number(report.SafetyViolationCount || 0) === 0 ? "pass" : "fail")
  ].join("");

  const categories = report.Categories || [];
  categoryTableEl.innerHTML = categories.length
    ? `<table class="table"><thead><tr><th>Category</th><th>Pass Rate</th><th>Tool Accuracy</th><th>Safety</th></tr></thead><tbody>${categories.map(c => `
        <tr>
          <td>${categoryNames[c.Category] || c.Category}</td>
          <td>${pct(c.PassRate)}</td>
          <td>${pct(c.ToolUsageAccuracy)}</td>
          <td>${c.SafetyViolationCount}</td>
        </tr>`).join("")}</tbody></table>`
    : "<p>No category data.</p>";

  const calls = report.TranslatorToolCalls || [];
  toolCallsEl.innerHTML = calls.length
    ? calls.map(call => `
      <div class="scenario">
        <div class="scenario-head">
          <strong>${call.Language}</strong>
          <span class="badge ${call.ExecutionSuccess ? "pass" : "fail"}">${call.ExecutionSuccess ? "success" : "failed"}</span>
        </div>
        <div class="pill-list">
          <span class="pill">tool: ${call.ToolName || "n/a"}</span>
          <span class="pill">attempts: ${call.AttemptCount}</span>
          <span class="pill">provider: ${call.Provider || "n/a"}</span>
          <span class="pill">translator: ${call.TranslatorName || "n/a"}</span>
        </div>
      </div>`).join("")
    : "<p>No tool call records.</p>";

  const scenarios = report.Scenarios || [];
  scenarioListEl.innerHTML = scenarios.length
    ? scenarios.map(item => `
      <div class="scenario">
        <div class="scenario-head">
          <strong>${item.ScenarioName}</strong>
          <span class="badge ${item.Passed ? "pass" : "fail"}">${item.Passed ? "passed" : "failed"}</span>
        </div>
        <div class="pill-list">
          <span class="pill">category: ${categoryNames[item.Category] || item.Category}</span>
          <span class="pill">latency: ${formatMs(item.LatencyMs)}</span>
          <span class="pill">approval observed: ${item.ApprovalObserved}</span>
          <span class="pill">max attempts: ${item.MaxAttemptCount}</span>
        </div>
        <pre>${escapeHtml(item.Response || "")}</pre>
      </div>`).join("")
    : "<p>No scenario records.</p>";

  const workers = report.AgentWorkflowContext?.Workers || [];
  workersEl.innerHTML = workers.length
    ? workers.map(w => `
      <div class="scenario">
        <div class="scenario-head"><strong>${w.Worker}</strong></div>
        <div><b>Prompt</b>: ${escapeHtml(w.Prompt || "")}</div>
        <div><b>Response</b>: ${escapeHtml(w.Response || "")}</div>
      </div>`).join("")
    : "<p>No worker context found.</p>";

  metadataEl.innerHTML = `
    <div class="scenario">
      <div class="pill-list">
        <span class="pill">dataset loaded: ${report.Dataset?.Loaded ?? false}</span>
        <span class="pill">dataset total: ${report.Dataset?.Total ?? "n/a"}</span>
        <span class="pill">observability: ${report.Observability?.Enabled ?? false}</span>
        <span class="pill">observer: ${report.Observability?.Observer ?? "n/a"}</span>
      </div>
      <pre>${escapeHtml(report.Dataset?.Path || "")}</pre>
    </div>`;

  const runResults = library?.scenarioRunResults || [];
  librarySummaryEl.innerHTML = runResults.length
    ? `<div class="pill-list">${runResults.slice(0, 6).map(r => `<span class="pill">${r.scenarioName} (${r.executionName})</span>`).join("")}</div>`
    : "<p>No library report loaded.</p>";

  libraryMetricsEl.innerHTML = runResults.length
    ? runResults.map(renderLibraryScenarioMetrics).join("")
    : "<p>No library metrics available. Load evaluation-library-report.json to inspect evaluator metrics.</p>";
}

function renderLibraryScenarioMetrics(run) {
  const metrics = run?.evaluationResult?.metrics || {};
  const metricEntries = Object.entries(metrics);

  const metricHtml = metricEntries.length
    ? metricEntries.map(([key, metric]) => {
      const valueClass = metricValueClass(metric);
      const valueText = formatMetricValue(metric);
      const reasonText = metric?.reason ? `<p class="metric-reason">${escapeHtml(metric.reason)}</p>` : "";
      return `
        <article class="metric-card">
          <p class="metric-name">${escapeHtml(key)}</p>
          <p class="metric-value ${valueClass}">${escapeHtml(valueText)}</p>
          ${reasonText}
        </article>`;
    }).join("")
    : "<p>No metrics in evaluation result.</p>";

  return `
    <section class="scenario">
      <div class="scenario-head">
        <strong>${escapeHtml(run.scenarioName || "unknown-scenario")}</strong>
        <span class="badge">${escapeHtml(run.executionName || "n/a")}</span>
      </div>
      <div class="pill-list">
        <span class="pill">iteration: ${escapeHtml(run.iterationName || "n/a")}</span>
        <span class="pill">created: ${escapeHtml(run.creationTime || "n/a")}</span>
      </div>
      <div class="metric-grid">${metricHtml}</div>
    </section>`;
}

function metricValueClass(metric) {
  if (metric?.$type === "boolean") {
    return metric.value === true ? "pass" : "fail";
  }

  return "";
}

function formatMetricValue(metric) {
  if (!metric) {
    return "n/a";
  }

  if (metric.$type === "numeric") {
    return Number(metric.value ?? 0).toFixed(2);
  }

  if (metric.$type === "boolean") {
    return String(metric.value);
  }

  if (metric.$type === "string") {
    return metric.value ?? "";
  }

  return String(metric.value ?? "");
}

function escapeHtml(text) {
  return String(text)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;");
}

async function tryLoadDefaultArtifacts() {
  try {
    const [report, library] = await Promise.all([
      fetchJson("./evaluation-report.json"),
      fetchJson("./evaluation-library-report.json").catch(() => null)
    ]);

    renderReport(report, library);
    setStatus("Loaded artifacts from ./evaluation-report.json");
  } catch (err) {
    setStatus(`Auto-load failed. Use file pickers. ${err.message}`, true);
  }
}

function fileToJson(file) {
  return file.text().then(t => JSON.parse(t));
}

function wireFilePickers() {
  const reportPicker = document.getElementById("reportFile");
  const libraryPicker = document.getElementById("libraryFile");
  let report = null;
  let library = null;

  reportPicker.addEventListener("change", async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;
    report = await fileToJson(file);
    if (report) {
      renderReport(report, library);
      setStatus(`Loaded report file: ${file.name}`);
    }
  });

  libraryPicker.addEventListener("change", async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;
    library = await fileToJson(file);
    if (report) {
      renderReport(report, library);
      setStatus(`Loaded library file: ${file.name}`);
    } else {
      setStatus("Library loaded. Load report JSON to render dashboard.");
    }
  });
}

document.getElementById("reload").addEventListener("click", () => {
  tryLoadDefaultArtifacts();
});

wireFilePickers();
tryLoadDefaultArtifacts();
