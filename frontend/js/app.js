document.body.addEventListener("htmx:configRequest", (e) => {
  e.detail.path = API_BASE + e.detail.path;
  const token = localStorage.getItem("token");
  if (token) {
    e.detail.headers["Authorization"] = "Bearer " + token;
  }
});

document.body.addEventListener("htmx:afterSwap", (e) => {
  if (e.detail.requestConfig.elt.id === "auth-result") return;
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

function handleAuthResponse(target, data) {
  localStorage.setItem("token", data.token);
  window.location.reload();
}
