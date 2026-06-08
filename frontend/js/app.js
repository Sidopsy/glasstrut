document.body.addEventListener("htmx:configRequest", (e) => {
  const original = new URL(e.detail.path, window.location.origin);
  const rewritten = new URL(original.pathname + original.search, API_BASE);
  e.detail.path = rewritten.toString();
  const token = localStorage.getItem("token");
  if (token) {
    e.detail.headers["Authorization"] = "Bearer " + token;
  }
});

document.body.addEventListener("htmx:responseError", (e) => {
  if (e.detail.xhr.status === 401) {
    localStorage.removeItem("token");
    window.location.reload();
  }
});

if ("serviceWorker" in navigator) {
  navigator.serviceWorker.register("/sw.js");
}

function handleAuthResponse(target, responseText) {
  const data = JSON.parse(responseText);
  localStorage.setItem("token", data.token);
  window.location.reload();
}
