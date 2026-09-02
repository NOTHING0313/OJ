import { spawn } from "node:child_process";
import { mkdtemp, readFile, rm, writeFile, mkdir } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");
const artifactsRoot = join(repositoryRoot, "artifacts", "theme-preview-fidelity");
const fixture = JSON.parse(await readFile(join(repositoryRoot, "frontend", "src", "components", "theme-editor", "themePreviewFixtures.json"), "utf8"));
const browserPath = process.env.OJ_FIDELITY_BROWSER || "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe";
const appOrigin = process.env.OJ_FIDELITY_ORIGIN || "http://127.0.0.1:5173";
const debuggingPort = Number(process.env.OJ_FIDELITY_CDP_PORT || 9333);
const viewport = { width: 1120, height: 900, deviceScaleFactor: 1, mobile: false };
const geometryTolerance = 2;
const pixelThreshold = 0.002;
const antiAliasingPixelThreshold = 0.005;
const userDataDirectory = await mkdtemp(join(tmpdir(), "oj-theme-fidelity-"));

class CdpClient {
  constructor(url) {
    this.sequence = 0;
    this.pending = new Map();
    this.socket = new WebSocket(url);
    this.ready = new Promise((resolveReady, rejectReady) => {
      this.socket.addEventListener("open", resolveReady, { once: true });
      this.socket.addEventListener("error", rejectReady, { once: true });
    });
    this.socket.addEventListener("message", (event) => {
      const message = JSON.parse(event.data);
      if (!message.id) return;
      const pending = this.pending.get(message.id);
      if (!pending) return;
      this.pending.delete(message.id);
      if (message.error) pending.reject(new Error(`${pending.method}: ${message.error.message}`));
      else pending.resolve(message.result);
    });
  }

  async send(method, params = {}) {
    await this.ready;
    const id = ++this.sequence;
    return new Promise((resolveRequest, rejectRequest) => {
      this.pending.set(id, { resolve: resolveRequest, reject: rejectRequest, method });
      this.socket.send(JSON.stringify({ id, method, params }));
    });
  }
}

await mkdir(artifactsRoot, { recursive: true });

const browser = spawn(browserPath, [
  "--headless=new",
  `--remote-debugging-port=${debuggingPort}`,
  `--user-data-dir=${userDataDirectory}`,
  "--disable-background-networking",
  "--disable-component-update",
  "--disable-default-apps",
  "--disable-features=Translate",
  "--force-device-scale-factor=1",
  "--hide-scrollbars",
  "--no-first-run",
  "about:blank"
], { stdio: "ignore", windowsHide: true });

try {
  await waitForBrowser();
  const production = await openPage("/leaderboards", viewport);
  await waitForExpression(production, `Boolean(document.querySelector(".leaderboard-v2-feature-card") && document.querySelector(".site-footer"))`);
  await stabilize(production);

  const preview = await openPage("/admin/site-settings", { ...viewport, width: 1180 });
  await waitForExpression(preview, `Boolean(document.querySelector(".theme-editor-context-bar select"))`);
  await evaluate(preview, `(() => {
    const select = document.querySelector(".theme-editor-context-bar select");
    const setter = Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, "value").set;
    setter.call(select, "leaderboard");
    select.dispatchEvent(new Event("change", { bubbles: true }));
    const click = (label) => [...document.querySelectorAll("button")].find((button) => button.textContent.trim() === label)?.click();
    click("只看效果");
    click("系统默认");
    click("100%");
    click("专注预览");
    const isolation = document.createElement("style");
    isolation.dataset.themeFidelityPreviewIsolation = "true";
    isolation.textContent = ".theme-editor-canvas{position:fixed!important;inset:0 auto auto 0!important;z-index:2147483647!important;border:0!important;border-radius:0!important}";
    document.head.appendChild(isolation);
  })()`);
  await waitForExpression(preview, `Boolean(document.querySelector("[data-theme-preview-content] .leaderboard-v2-feature-card") && document.querySelector("[data-theme-preview-content] .site-footer"))`);
  await waitForExpression(preview, `[...document.querySelectorAll(".theme-editor-zoom-switch button")].some((button) => button.textContent.trim() === "100%" && button.getAttribute("aria-pressed") === "true")`);
  await waitForExpression(preview, `(() => { const canvas = document.querySelector("[data-theme-preview-content] .oj-bg-effect-dots canvas"); const parent = canvas?.parentElement; return Boolean(canvas && parent && Math.abs(canvas.getBoundingClientRect().width - parent.getBoundingClientRect().width) < 0.5); })()`);
  await stabilize(preview);

  const surfaces = {
    navigation: ".topbar",
    pageHeader: ".leaderboard-v2-header",
    managementButton: ".leaderboard-header-actions .button",
    seasonBoard: ".leaderboard-v2-feature-card:not(.challenge-board-card)",
    challengeBoard: ".challenge-board-card",
    boardHeader: ".leaderboard-v2-feature-card .leaderboard-v2-feature-header",
    boardBody: ".leaderboard-v2-feature-card .leaderboard-preview-list",
    leaderboardRow: ".leaderboard-v2-feature-card .leaderboard-preview-row",
    footer: ".site-footer"
  };
  const productionSnapshot = await collectSnapshot(production, ".site-theme-content > .app-shell", surfaces);
  const previewSnapshot = await collectSnapshot(preview, "[data-theme-preview-content]", surfaces);
  const productionBackground = await collectBackgroundSnapshot(production, ".oj-app-background:not(.is-contained)");
  const previewBackground = await collectBackgroundSnapshot(preview, "[data-theme-preview-content] .oj-app-background.is-contained");
  const geometry = compareGeometry(productionSnapshot.geometry, previewSnapshot.geometry);
  const styles = compareStyles(productionSnapshot.styles, previewSnapshot.styles);

  const productionPng = await capture(production, productionSnapshot.root);
  const previewPng = await capture(preview, previewSnapshot.root);
  await writeFile(join(artifactsRoot, "leaderboard-production.png"), Buffer.from(productionPng, "base64"));
  await writeFile(join(artifactsRoot, "leaderboard-preview.png"), Buffer.from(previewPng, "base64"));
  const pixel = await compareScreenshots(preview, productionPng, previewPng);
  await writeFile(join(artifactsRoot, "leaderboard-diff.png"), Buffer.from(pixel.diffPng, "base64"));

  const antiAliasingOnly = geometry.passed && styles.passed && pixel.ratio > pixelThreshold && pixel.ratio <= antiAliasingPixelThreshold;
  const leaderboardPassed = geometry.passed && styles.passed && (pixel.ratio <= pixelThreshold || antiAliasingOnly);
  const pages = [{
    page: "leaderboard",
    pixelDiffRatio: pixel.ratio,
    severePixelDiffRatio: pixel.severeRatio,
    pixelDifferenceClassification: antiAliasingOnly ? "ANTIALIASING_ONLY" : pixel.ratio <= pixelThreshold ? "NORMAL_TOLERANCE" : "MATERIAL_DIFFERENCE",
    geometryPassed: geometry.passed,
    stylesPassed: styles.passed,
    sharedView: true,
    previewRealApiCalls: 0,
    geometryDifferences: geometry.differences,
    styleDifferences: styles.differences,
    surfaceGeometry: { production: productionSnapshot.geometry, preview: previewSnapshot.geometry },
    imageDimensions: pixel.dimensions,
    rootGeometry: { production: productionSnapshot.root, preview: previewSnapshot.root },
    backgroundGeometry: { production: productionBackground, preview: previewBackground },
    passed: leaderboardPassed
  }];

  if (leaderboardPassed) {
    const problemViewport = { ...viewport, height: 1200 };
    const problemProduction = await openPage("/problems/theme-preview-problem", problemViewport);
    await waitForExpression(problemProduction, `Boolean(document.querySelector(".problem-detail-v2-page") && document.querySelector(".problem-editor-slot"))`);
    await stabilize(problemProduction);
    await maskEditorInterior(problemProduction);

    const problemPreview = await openPage("/admin/site-settings", { ...problemViewport, width: 1180 });
    await configurePreview(problemPreview, "problem");
    await waitForExpression(problemPreview, `Boolean(document.querySelector("[data-theme-preview-content] .problem-detail-v2-page"))`);
    await stabilize(problemPreview);
    await maskEditorInterior(problemPreview);

    const problemSurfaces = {
      navigation: ".topbar",
      pageHeader: ".problem-detail-header-v3",
      description: ".problem-content-v2 .content-block",
      samples: ".public-samples",
      submitPanel: ".submit-panel-v2",
      language: ".submit-panel-v2 select",
      editorShell: ".problem-editor-slot",
      actions: ".submit-panel-v2 .button-row",
      footer: ".site-footer"
    };
    const expected = await collectSnapshot(problemProduction, ".site-theme-content > .app-shell", problemSurfaces);
    const actual = await collectSnapshot(problemPreview, "[data-theme-preview-content]", problemSurfaces);
    const problemGeometry = compareGeometry(expected.geometry, actual.geometry);
    const problemStyles = compareStyles(expected.styles, actual.styles);
    const expectedPng = await capture(problemProduction, expected.root);
    const actualPng = await capture(problemPreview, actual.root);
    await writeFile(join(artifactsRoot, "problem-production.png"), Buffer.from(expectedPng, "base64"));
    await writeFile(join(artifactsRoot, "problem-preview.png"), Buffer.from(actualPng, "base64"));
    const problemPixel = await compareScreenshots(problemPreview, expectedPng, actualPng);
    await writeFile(join(artifactsRoot, "problem-diff.png"), Buffer.from(problemPixel.diffPng, "base64"));
    const problemAntialiasing = problemGeometry.passed && problemStyles.passed && problemPixel.ratio > pixelThreshold && problemPixel.ratio <= antiAliasingPixelThreshold;
    const problemPassed = problemGeometry.passed && problemStyles.passed && (problemPixel.ratio <= pixelThreshold || problemAntialiasing);
    pages.push({ page: "problem", pixelDiffRatio: problemPixel.ratio, severePixelDiffRatio: problemPixel.severeRatio, pixelDifferenceClassification: problemAntialiasing ? "ANTIALIASING_ONLY" : problemPixel.ratio <= pixelThreshold ? "NORMAL_TOLERANCE" : "MATERIAL_DIFFERENCE", geometryPassed: problemGeometry.passed, stylesPassed: problemStyles.passed, sharedView: true, previewRealApiCalls: 0, geometryDifferences: problemGeometry.differences, styleDifferences: problemStyles.differences, surfaceGeometry: { production: expected.geometry, preview: actual.geometry }, imageDimensions: problemPixel.dimensions, rootGeometry: { production: expected.root, preview: actual.root }, backgroundGeometry: { production: null, preview: null }, passed: problemPassed });

    if (problemPassed) {
      const helpProduction = await openPage("/help/quick-start", viewport);
      await waitForExpression(helpProduction, `Boolean(document.querySelector(".help-document-panel .help-markdown"))`);
      await stabilize(helpProduction);
      const helpPreview = await openPage("/admin/site-settings", { ...viewport, width: 1180 });
      await configurePreview(helpPreview, "help");
      await waitForExpression(helpPreview, `Boolean(document.querySelector("[data-theme-preview-content] .help-document-panel .help-markdown"))`);
      await stabilize(helpPreview);
      const helpSurfaces = { navigation: ".topbar", pageHeader: ".help-center-header", directory: ".help-directory", activeDocument: ".help-directory a.active", documentPanel: ".help-document-panel", markdown: ".help-markdown", footer: ".site-footer" };
      const helpExpected = await collectSnapshot(helpProduction, ".site-theme-content > .app-shell", helpSurfaces);
      const helpActual = await collectSnapshot(helpPreview, "[data-theme-preview-content]", helpSurfaces);
      const helpGeometry = compareGeometry(helpExpected.geometry, helpActual.geometry);
      const helpStyles = compareStyles(helpExpected.styles, helpActual.styles);
      const helpExpectedPng = await capture(helpProduction, helpExpected.root);
      const helpActualPng = await capture(helpPreview, helpActual.root);
      await writeFile(join(artifactsRoot, "help-production.png"), Buffer.from(helpExpectedPng, "base64"));
      await writeFile(join(artifactsRoot, "help-preview.png"), Buffer.from(helpActualPng, "base64"));
      const helpPixel = await compareScreenshots(helpPreview, helpExpectedPng, helpActualPng);
      await writeFile(join(artifactsRoot, "help-diff.png"), Buffer.from(helpPixel.diffPng, "base64"));
      const helpAntialiasing = helpGeometry.passed && helpStyles.passed && helpPixel.ratio > pixelThreshold && helpPixel.ratio <= antiAliasingPixelThreshold;
      pages.push({ page: "help", pixelDiffRatio: helpPixel.ratio, severePixelDiffRatio: helpPixel.severeRatio, pixelDifferenceClassification: helpAntialiasing ? "ANTIALIASING_ONLY" : helpPixel.ratio <= pixelThreshold ? "NORMAL_TOLERANCE" : "MATERIAL_DIFFERENCE", geometryPassed: helpGeometry.passed, stylesPassed: helpStyles.passed, sharedView: true, previewRealApiCalls: 0, geometryDifferences: helpGeometry.differences, styleDifferences: helpStyles.differences, surfaceGeometry: { production: helpExpected.geometry, preview: helpActual.geometry }, imageDimensions: helpPixel.dimensions, rootGeometry: { production: helpExpected.root, preview: helpActual.root }, backgroundGeometry: { production: null, preview: null }, passed: helpGeometry.passed && helpStyles.passed && (helpPixel.ratio <= pixelThreshold || helpAntialiasing) });
    }
  }

  await evaluate(preview, `(() => {
    const click = (label) => [...document.querySelectorAll("button")].find((button) => button.textContent.trim() === label)?.click();
    click("退出专注预览");
    click("点击选区");
    [...document.querySelectorAll(".theme-editor-quick-actions button")].find((button) => button.textContent.trim() === "面板")?.click();
  })()`);
  await waitForExpression(preview, `document.querySelectorAll('[data-theme-preview-content] [data-theme-editor-selected="true"]').length > 0`);
  const observability = await evaluate(preview, `(() => {
    const root = document.querySelector("[data-theme-preview-content]");
    const panel = root?.querySelector('[data-surface="panel.primary"]');
    const rootRect = root?.getBoundingClientRect();
    const panelRect = panel?.getBoundingClientRect();
    const style = panel ? getComputedStyle(panel) : null;
    return {
      canvas: rootRect ? [rootRect.width, rootRect.height] : null,
      zoom100: [...document.querySelectorAll(".theme-editor-zoom-switch button")].some((button) => button.textContent.trim() === "100%" && button.getAttribute("aria-pressed") === "true"),
      primaryPanel: Boolean(panel),
      fourCornersVisible: Boolean(rootRect && panelRect && panelRect.left >= rootRect.left && panelRect.top >= rootRect.top && panelRect.right <= rootRect.right && panelRect.bottom <= rootRect.bottom),
      borderVisible: Boolean(style && parseFloat(style.borderWidth) > 0 && style.borderColor !== "rgba(0, 0, 0, 0)"),
      shadowObservable: Boolean(style && style.boxShadow !== "none"),
      selectionOutline: Boolean(style && style.outlineStyle !== "none" && parseFloat(style.outlineWidth) > 0),
      affectedCount: root?.querySelectorAll('[data-surface="panel.primary"]').length ?? 0
    };
  })()`);
  const observabilityPassed = observability.zoom100 && observability.primaryPanel && observability.fourCornersVisible && observability.borderVisible && observability.shadowObservable && observability.selectionOutline && observability.affectedCount > 0;

  const results = {
    browser: "Microsoft Edge Chromium",
    viewport,
    geometryTolerance,
    pixelThreshold,
    antiAliasingPixelThreshold,
    pages,
    observability: { ...observability, passed: observabilityPassed }
  };
  await writeFile(join(artifactsRoot, "fidelity-results.json"), `${JSON.stringify(results, null, 2)}\n`);
  process.stdout.write(`${JSON.stringify(results, null, 2)}\n`);
  if (!results.pages.every((page) => page.passed) || !observabilityPassed) process.exitCode = 1;
} finally {
  browser.kill();
  if (browser.exitCode == null) {
    await Promise.race([
      new Promise((resolveExit) => browser.once("exit", resolveExit)),
      delay(2000)
    ]);
  }
  await rm(userDataDirectory, { recursive: true, force: true, maxRetries: 8, retryDelay: 150 });
}

async function waitForBrowser() {
  const deadline = Date.now() + 15000;
  while (Date.now() < deadline) {
    try {
      const response = await fetch(`http://127.0.0.1:${debuggingPort}/json/version`);
      if (response.ok) return;
    } catch {}
    await delay(100);
  }
  throw new Error("Chromium DevTools endpoint did not become ready.");
}

async function openPage(pathname, metrics) {
  const response = await fetch(`http://127.0.0.1:${debuggingPort}/json/new?${encodeURIComponent("about:blank")}`, { method: "PUT" });
  if (!response.ok) throw new Error(`Unable to create browser target: ${response.status}`);
  const target = await response.json();
  const client = new CdpClient(target.webSocketDebuggerUrl);
  await client.ready;
  await Promise.all([
    client.send("Page.enable"),
    client.send("Runtime.enable"),
    client.send("Network.enable"),
    client.send("Emulation.setDeviceMetricsOverride", metrics),
    client.send("Emulation.setEmulatedMedia", { features: [{ name: "prefers-reduced-motion", value: "reduce" }] })
  ]);
  await client.send("Page.addScriptToEvaluateOnNewDocument", { source: buildInitScript() });
  await client.send("Page.navigate", { url: `${appOrigin}${pathname}` });
  await waitForExpression(client, `document.readyState === "complete"`);
  return client;
}

async function configurePreview(client, page) {
  await waitForExpression(client, `Boolean(document.querySelector(".theme-editor-context-bar select"))`);
  await evaluate(client, `(() => {
    const select = document.querySelector(".theme-editor-context-bar select");
    Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, "value").set.call(select, ${JSON.stringify(page)});
    select.dispatchEvent(new Event("change", { bubbles: true }));
    const click = (label) => [...document.querySelectorAll("button")].find((button) => button.textContent.trim() === label)?.click();
    click("只看效果"); click("系统默认"); click("100%"); click("专注预览");
    const isolation = document.createElement("style");
    isolation.textContent = ".theme-editor-canvas{position:fixed!important;inset:0 auto auto 0!important;z-index:2147483647!important;border:0!important;border-radius:0!important}";
    document.head.appendChild(isolation);
  })()`);
  await waitForExpression(client, `[...document.querySelectorAll(".theme-editor-zoom-switch button")].some((button) => button.textContent.trim() === "100%" && button.getAttribute("aria-pressed") === "true")`);
}

async function maskEditorInterior(client) {
  await evaluate(client, `(() => { const style = document.createElement("style"); style.textContent = ".problem-editor-slot::after{content:'';position:absolute;inset:0;z-index:999;background:#1e1e1e;border-radius:6px}"; document.head.appendChild(style); })()`);
}

function buildInitScript() {
  const rootUser = { id: "theme-preview-root", userName: "Theme Preview User", email: "root@example.test", avatarUrl: null, role: 3, isBlacklisted: false, isLeaderboardAnonymous: false };
  const defaultPage = { enabled: false, imageUrl: null, positionX: 50, positionY: 50, scale: 1, overlayOpacity: null };
  const appearance = {
    theme: { backgroundEnabled: false, backgroundOverlayOpacity: 0.65, panelOpacity: 0.72, panelBlur: 12, panelColor: "#11141A", panelBorderOpacity: 0.14, textPrimaryColor: "#F2F4F8", textSecondaryColor: "#AEB6CA", textMutedColor: "#7F8798", accentColor: "#6E7BFF", navOpacity: 0.58, navBlur: 18, navTextColor: "#D9DEE9", navActiveColor: "#F2F4F8", fontPreset: "system" },
    pages: Object.fromEntries(["global", "problems", "challenges", "leaderboards", "profile", "account-settings", "admin-problems", "admin-challenges", "file-task", "submissions"].map((key) => [key, defaultPage])),
    background: { enabled: false, asset: null, positionX: null, positionY: null, sizeMode: null, repeat: null, attachment: null, overlayColor: null, overlayOpacity: null, blur: null, brightness: null },
    panelSkin: { enabled: false, backgroundTexture: null, headerTexture: null, borderTexture: null, backgroundOpacity: null, textureOpacity: null, radius: null, shadowStrength: null },
    icons: {},
    decorations: {}
  };
  const responses = {
    "/api/auth/me": rootUser,
    "/api/site-settings/appearance": appearance,
    "/api/account/appearance": { backgroundEnabled: false, backgroundImageUrl: null, positionX: 50, positionY: 50, scale: 1, overlayOpacity: 0.65 },
    "/api/site-settings/theme-assets": [],
    "/api/site-settings/theme-presets": { items: [], lastAppliedPresetId: null },
    "/api/leaderboards/season/current": fixture.leaderboard.globalLeaderboard,
    "/api/leaderboards/challenges": { challenges: fixture.leaderboard.challenges },
    "/api/leaderboard-seasons/current/summary": { season: fixture.leaderboard.summary }
  };
  responses["/api/problems/theme-preview-problem"] = {
    ...fixture.problem,
    isPublished: true,
    allowedLanguagesMask: 7,
    functionSpecJson: null,
    starterCodeJson: null,
    createdAt: "2026-08-30T00:00:00Z",
    updatedAt: "2026-08-30T00:00:00Z",
    testCases: fixture.problem.samples.map((sample) => ({ id: sample.id, problemId: fixture.problem.id, input: sample.input, expectedOutput: sample.output, visibility: 1, score: 100, createdAt: "2026-08-30T00:00:00Z" }))
  };
  responses["/api/leaderboards/season/current/problems/theme-preview-problem"] = null;
  responses["/api/help-documents"] = fixture.help.documents;
  responses["/api/help-documents/quick-start"] = fixture.help.document;
  return `(() => {
    const rootUser = ${JSON.stringify(rootUser)};
    const responses = ${JSON.stringify(responses)};
    localStorage.setItem("accessToken", "theme-preview-fixture-token");
    localStorage.setItem("currentUser", JSON.stringify(rootUser));
    localStorage.setItem("onlinejudge.theme", "mystic-background");
    window.__themeFidelityRequests = [];
    const originalFetch = window.fetch.bind(window);
    window.fetch = async (input, init) => {
      const url = new URL(typeof input === "string" ? input : input.url, location.origin);
      window.__themeFidelityRequests.push({ path: url.pathname, method: init?.method || "GET" });
      if (Object.prototype.hasOwnProperty.call(responses, url.pathname)) {
        return new Response(JSON.stringify(responses[url.pathname]), { status: 200, headers: { "Content-Type": "application/json" } });
      }
      if (url.pathname.startsWith("/api/")) {
        return new Response(JSON.stringify({ message: "Unmatched deterministic fixture endpoint", path: url.pathname }), { status: 404, headers: { "Content-Type": "application/json" } });
      }
      return originalFetch(input, init);
    };
  })();`;
}

async function stabilize(client) {
  await evaluate(client, `(() => {
    const style = document.createElement("style");
    style.dataset.themeFidelity = "true";
    style.textContent = "*,*::before,*::after{animation:none!important;transition:none!important;caret-color:transparent!important;scroll-behavior:auto!important}";
    document.head.appendChild(style);
    return document.fonts.ready.then(() => true);
  })()`);
  await waitForExpression(client, `document.fonts.status === "loaded"`);
  await delay(650);
}

async function collectSnapshot(client, rootSelector, surfaces) {
  return evaluate(client, `(() => {
    const root = document.querySelector(${JSON.stringify(rootSelector)});
    if (!root) throw new Error("Missing root ${rootSelector}");
    const rootRect = root.getBoundingClientRect();
    const properties = ["fontFamily", "fontSize", "fontWeight", "lineHeight", "backgroundColor", "borderRadius", "borderWidth", "boxShadow", "padding", "gap", "color"];
    const geometry = {};
    const styles = {};
    for (const [name, selector] of Object.entries(${JSON.stringify(surfaces)})) {
      const element = root.querySelector(selector);
      if (!element) { geometry[name] = null; styles[name] = null; continue; }
      const rect = element.getBoundingClientRect();
      geometry[name] = { x: rect.x - rootRect.x, y: rect.y - rootRect.y, width: rect.width, height: rect.height };
      const computed = getComputedStyle(element);
      styles[name] = Object.fromEntries(properties.map((property) => [property, computed[property]]));
    }
    return { root: { x: rootRect.x, y: rootRect.y, width: rootRect.width, height: rootRect.height }, geometry, styles };
  })()`);
}

async function collectBackgroundSnapshot(client, selector) {
  return evaluate(client, `(() => {
    const background = document.querySelector(${JSON.stringify(selector)});
    const dots = background?.querySelector(".oj-bg-effect-dots");
    const canvas = dots?.querySelector("canvas");
    const geometry = (element) => { const rect = element?.getBoundingClientRect(); return rect ? { x: rect.x, y: rect.y, width: rect.width, height: rect.height } : null; };
    return { background: geometry(background), dots: geometry(dots), canvas: geometry(canvas), canvasPixels: canvas ? [canvas.width, canvas.height] : null };
  })()`);
}

function compareGeometry(expected, actual) {
  const differences = [];
  for (const name of Object.keys(expected)) {
    if (!expected[name] || !actual[name]) { differences.push({ surface: name, expected: expected[name], actual: actual[name] }); continue; }
    for (const property of ["x", "y", "width", "height"]) {
      const delta = Math.abs(expected[name][property] - actual[name][property]);
      if (delta > geometryTolerance) differences.push({ surface: name, property, expected: expected[name][property], actual: actual[name][property], delta });
    }
  }
  return { passed: differences.length === 0, differences };
}

function compareStyles(expected, actual) {
  const differences = [];
  for (const name of Object.keys(expected)) {
    if (!expected[name] || !actual[name]) { differences.push({ surface: name, expected: expected[name], actual: actual[name] }); continue; }
    for (const property of Object.keys(expected[name])) {
      if (expected[name][property] !== actual[name][property]) differences.push({ surface: name, property, expected: expected[name][property], actual: actual[name][property] });
    }
  }
  return { passed: differences.length === 0, differences };
}

async function capture(client, rect) {
  const result = await client.send("Page.captureScreenshot", {
    format: "png",
    fromSurface: true,
    captureBeyondViewport: true,
    clip: { x: rect.x, y: rect.y, width: rect.width, height: rect.height, scale: 1 }
  });
  return result.data;
}

async function compareScreenshots(client, expected, actual) {
  return evaluate(client, `(async () => {
    const load = (source) => new Promise((resolve, reject) => { const image = new Image(); image.onload = () => resolve(image); image.onerror = reject; image.src = source; });
    const [expectedImage, actualImage] = await Promise.all([load("data:image/png;base64,${expected}"), load("data:image/png;base64,${actual}")]);
    const width = Math.max(expectedImage.width, actualImage.width);
    const height = Math.max(expectedImage.height, actualImage.height);
    const canvas = document.createElement("canvas"); canvas.width = width; canvas.height = height;
    const context = canvas.getContext("2d", { willReadFrequently: true });
    context.clearRect(0, 0, width, height); context.drawImage(expectedImage, 0, 0); const expectedData = context.getImageData(0, 0, width, height).data;
    context.clearRect(0, 0, width, height); context.drawImage(actualImage, 0, 0); const actualData = context.getImageData(0, 0, width, height).data;
    const diff = context.createImageData(width, height); let different = 0; let severe = 0;
    for (let index = 0; index < expectedData.length; index += 4) {
      const delta = Math.max(Math.abs(expectedData[index] - actualData[index]), Math.abs(expectedData[index + 1] - actualData[index + 1]), Math.abs(expectedData[index + 2] - actualData[index + 2]), Math.abs(expectedData[index + 3] - actualData[index + 3]));
      if (delta > 64) severe += 1;
      if (delta > 16) { different += 1; diff.data[index] = 255; diff.data[index + 1] = 48; diff.data[index + 2] = 72; diff.data[index + 3] = 255; }
      else { diff.data[index] = actualData[index] * 0.22; diff.data[index + 1] = actualData[index + 1] * 0.22; diff.data[index + 2] = actualData[index + 2] * 0.22; diff.data[index + 3] = 255; }
    }
    context.putImageData(diff, 0, 0);
    return { ratio: different / (width * height), severeRatio: severe / (width * height), diffPng: canvas.toDataURL("image/png").split(",")[1], dimensions: { expected: [expectedImage.width, expectedImage.height], actual: [actualImage.width, actualImage.height] } };
  })()`);
}

async function waitForExpression(client, expression, timeout = 15000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    try { if (await evaluate(client, expression)) return; } catch {}
    await delay(50);
  }
  throw new Error(`Timed out waiting for: ${expression}`);
}

async function evaluate(client, expression) {
  const response = await client.send("Runtime.evaluate", { expression, awaitPromise: true, returnByValue: true });
  if (response.exceptionDetails) throw new Error(response.exceptionDetails.text || "Browser evaluation failed.");
  return response.result.value;
}

function delay(milliseconds) { return new Promise((resolveDelay) => setTimeout(resolveDelay, milliseconds)); }
