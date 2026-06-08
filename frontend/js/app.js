document.body.addEventListener("htmx:configRequest", (e) => {
  e.detail.path = API_BASE + e.detail.path;
});

if ("serviceWorker" in navigator) {
  navigator.serviceWorker.register("/sw.js");
}
