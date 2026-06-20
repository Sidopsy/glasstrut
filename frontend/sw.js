const CACHE_NAME = "glasstrut-v4";
const CDN_CACHE = "glasstrut-cdn-v2";
const API_CACHE = "glasstrut-api-v1";

const STATIC_FILES = [
  "./",
  "./index.html",
  "./css/style.css",
  "./js/config.js",
  "./js/app.js",
  "./manifest.json",
  "./icon.svg",
];

const CDN_URLS = [
  "https://cdn.tailwindcss.com",
  "https://cdn.jsdelivr.net/npm/jsqr@1.4.0/dist/jsQR.js",
];

self.addEventListener("install", (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => cache.addAll(STATIC_FILES))
  );
  self.skipWaiting();
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(
        keys
          .filter((k) => k !== CACHE_NAME && k !== CDN_CACHE && k !== API_CACHE)
          .map((k) => caches.delete(k))
      )
    )
  );
});

self.addEventListener("fetch", (event) => {
  const url = new URL(event.request.url);

  // CDN assets: stale-while-revalidate (serve cached, update in background)
  if (CDN_URLS.some((cdn) => url.href.startsWith(cdn))) {
    event.respondWith(
      caches.open(CDN_CACHE).then((cache) =>
        cache.match(event.request).then((cached) => {
          const fetchPromise = fetch(event.request)
            .then((response) => {
              cache.put(event.request, response.clone());
              return response;
            })
            .catch(() => cached);
          return cached || fetchPromise;
        })
      )
    );
    return;
  }

  // API requests: network-first with cache fallback
  if (url.pathname.startsWith("/api/")) {
    event.respondWith(
      caches.open(API_CACHE).then((cache) =>
        fetch(event.request)
          .then((response) => {
            if (response.ok && event.request.method === "GET") {
              cache.put(event.request, response.clone());
            }
            return response;
          })
          .catch(() =>
            cache.match(event.request).then((cached) => {
              if (cached) return cached;
              // For non-GET API requests, return a simple offline response
              if (event.request.method !== "GET") {
                return new Response(JSON.stringify({ ok: true, _queued: true }), {
                  status: 200,
                  headers: { "Content-Type": "application/json" },
                });
              }
              return new Response(
                JSON.stringify({ error: "You are offline" }),
                { status: 503, headers: { "Content-Type": "application/json" } }
              );
            })
          )
      )
    );
    return;
  }

  // Static files: cache-first with network fallback
  event.respondWith(
    caches.match(event.request).then((cached) => cached || fetch(event.request))
  );
});
