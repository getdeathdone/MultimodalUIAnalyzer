const LOCAL_AGENT_URL = "http://localhost:5088";
const STATUS_URL = `${LOCAL_AGENT_URL}/api/status`;

const statusBadge = document.querySelector("#statusBadge");
const readyPanel = document.querySelector("#readyPanel");
const installPanel = document.querySelector("#installPanel");
const checkAgain = document.querySelector("#checkAgain");
const windowsDownload = document.querySelector("#windowsDownload");
const macArmDownload = document.querySelector("#macArmDownload");
const macIntelDownload = document.querySelector("#macIntelDownload");
const windowsCommand = document.querySelector("#windowsCommand");
const macArmCommand = document.querySelector("#macArmCommand");
const macIntelCommand = document.querySelector("#macIntelCommand");
const copyWindows = document.querySelector("#copyWindows");
const copyMacArm = document.querySelector("#copyMacArm");
const copyMacIntel = document.querySelector("#copyMacIntel");

function detectPlatform() {
  const ua = navigator.userAgent.toLowerCase();
  if (ua.includes("mac")) return "mac";
  if (ua.includes("windows")) return "windows";
  return "unknown";
}

function getBaseUrl() {
  const url = new URL(window.location.href);
  url.hash = "";
  url.search = "";
  url.pathname = url.pathname.replace(/\/[^/]*$/, "/");
  return url.toString().replace(/\/$/, "");
}

function buildWindowsCommand() {
  const baseUrl = getBaseUrl();
  const appUrl = `${baseUrl}/downloads/MultimodalUIAnalyzer-0.1.0-win-x64.zip`;
  return `powershell -ExecutionPolicy Bypass -Command "iwr -UseBasicParsing '${baseUrl}/install-windows.ps1' -OutFile $env:TEMP\\mua-install.ps1; & $env:TEMP\\mua-install.ps1 -AppUrl '${appUrl}'"`;
}

function buildMacCommand(runtime, archiveName) {
  const baseUrl = getBaseUrl();
  const archiveUrl = `${baseUrl}/downloads/${archiveName}`;
  return `curl -fsSL "${baseUrl}/install-mac.sh" | bash -s -- "${archiveUrl}" ${runtime}`;
}

async function copyCommand(command, button) {
  await navigator.clipboard.writeText(command);
  const original = button.textContent;
  button.textContent = "Copied";
  window.setTimeout(() => {
    button.textContent = original;
  }, 1200);
}

function markRecommendedDownload() {
  const platform = detectPlatform();
  windowsDownload.classList.toggle("recommended", platform === "windows");
  macArmDownload.classList.toggle("recommended", platform === "mac");
}

function setChecking() {
  statusBadge.className = "status checking";
  statusBadge.textContent = "Checking local app...";
  readyPanel.classList.add("hidden");
  installPanel.classList.add("hidden");
}

function setReady() {
  statusBadge.className = "status ready";
  statusBadge.textContent = "App is running";
  readyPanel.classList.remove("hidden");
  installPanel.classList.add("hidden");
  window.location.href = LOCAL_AGENT_URL;
}

function setMissing() {
  statusBadge.className = "status missing";
  statusBadge.textContent = "Local app is not running";
  readyPanel.classList.add("hidden");
  installPanel.classList.remove("hidden");
}

async function checkAgent() {
  setChecking();

  const controller = new AbortController();
  const timeout = window.setTimeout(() => controller.abort(), 1800);

  try {
    const response = await fetch(STATUS_URL, {
      signal: controller.signal,
      mode: "cors"
    });

    window.clearTimeout(timeout);

    if (!response.ok) {
      setMissing();
      return;
    }

    setReady();
  } catch {
    window.clearTimeout(timeout);
    setMissing();
  }
}

windowsCommand.textContent = buildWindowsCommand();
macArmCommand.textContent = buildMacCommand("osx-arm64", "MultimodalUIAnalyzer-0.1.0-osx-arm64.zip");
macIntelCommand.textContent = buildMacCommand("osx-x64", "MultimodalUIAnalyzer-0.1.0-osx-x64.zip");

copyWindows.addEventListener("click", () => copyCommand(windowsCommand.textContent, copyWindows));
copyMacArm.addEventListener("click", () => copyCommand(macArmCommand.textContent, copyMacArm));
copyMacIntel.addEventListener("click", () => copyCommand(macIntelCommand.textContent, copyMacIntel));
checkAgain.addEventListener("click", checkAgent);

markRecommendedDownload();
checkAgent();
