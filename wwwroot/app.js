const form = document.querySelector("#analysis-form");
const imageInput = document.querySelector("#image");
const preview = document.querySelector("#preview");
const emptyPreview = document.querySelector("#empty-preview");
const output = document.querySelector("#output");
const statusText = document.querySelector("#status");
const submit = document.querySelector("#submit");
const downloadProgress = document.querySelector("#download-progress");
const downloadTitle = document.querySelector("#download-title");
const downloadPercent = document.querySelector("#download-percent");
const downloadBar = document.querySelector("#download-bar");
const downloadStatus = document.querySelector("#download-status");

imageInput.addEventListener("change", () => {
  const file = imageInput.files?.[0];
  if (!file) {
    preview.removeAttribute("src");
    preview.style.display = "none";
    emptyPreview.style.display = "grid";
    return;
  }

  preview.src = URL.createObjectURL(file);
  preview.style.display = "block";
  emptyPreview.style.display = "none";
});

form.addEventListener("submit", async (event) => {
  event.preventDefault();

  const formData = new FormData(form);
  const selectedModel = formData.get("model");

  submit.disabled = true;
  statusText.textContent = `Checking ${selectedModel}...`;
  resetModelProgress(selectedModel);
  output.textContent = "{}";

  try {
    await ensureModelAvailable(selectedModel);
    statusText.textContent = "Analyzing...";

    const response = await fetch("/api/vision/analyze", {
      method: "POST",
      body: formData
    });

    const contentType = response.headers.get("content-type") ?? "";
    const payload = contentType.includes("application/json")
      ? await response.json()
      : { error: await response.text() };

    if (!response.ok) {
      throw new Error(payload.error ?? "Analysis failed.");
    }

    output.textContent = JSON.stringify(payload, null, 2);
    statusText.textContent = "Done";
    hideModelProgress();
  } catch (error) {
    output.textContent = JSON.stringify({ error: error.message }, null, 2);
    statusText.textContent = "Error";
  } finally {
    submit.disabled = false;
  }
});

function ensureModelAvailable(model) {
  return new Promise((resolve, reject) => {
    const source = new EventSource(`/api/vision/models/ensure?model=${encodeURIComponent(model)}`);

    source.onmessage = (event) => {
      const progress = JSON.parse(event.data);
      const status = progress.status ?? progress.Status ?? "Preparing model";
      const percent = progress.percent ?? progress.Percent;
      const done = progress.done ?? progress.Done;
      const hasError = progress.error ?? progress.Error;

      updateModelProgress(model, status, percent);

      statusText.textContent = percent === null || percent === undefined
        ? `${model}: ${status}`
        : `${model}: ${status} (${percent}%)`;

      if (hasError) {
        source.close();
        reject(new Error(status));
        return;
      }

      if (done) {
        updateModelProgress(model, status, 100);
        source.close();
        resolve();
      }
    };

    source.onerror = () => {
      source.close();
      reject(new Error("Model download progress connection failed."));
    };
  });
}

function resetModelProgress(model) {
  downloadProgress.hidden = false;
  downloadTitle.textContent = model;
  downloadPercent.textContent = "0%";
  downloadBar.style.width = "0%";
  downloadStatus.textContent = "Preparing model check...";
}

function updateModelProgress(model, status, percent) {
  downloadProgress.hidden = false;
  downloadTitle.textContent = model;
  downloadStatus.textContent = status;

  if (percent === null || percent === undefined) {
    downloadPercent.textContent = "...";
    return;
  }

  const normalizedPercent = Math.max(0, Math.min(100, Number(percent)));
  downloadPercent.textContent = `${normalizedPercent}%`;
  downloadBar.style.width = `${normalizedPercent}%`;
}

function hideModelProgress() {
  window.setTimeout(() => {
    downloadProgress.hidden = true;
  }, 900);
}
