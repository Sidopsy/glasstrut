var API_BASE = (() => {
  // 1. Check for local override (file is gitignored)
  try {
    if (typeof LOCAL_API_BASE !== 'undefined') return LOCAL_API_BASE;
  } catch (_) {}

  // 2. Auto-detect local dev vs deployed
  var host = window.location.hostname;
  if (!host || host === 'localhost' || host === '127.0.0.1') {
    return 'http://localhost:5088';
  }

  // 3. Production — replace with your backend's DuckDNS, ngrok, or custom domain
  return 'https://glasstrut.duckdns.org';
})();
