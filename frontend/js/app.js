const API = API_BASE;
const EMOJIS = ["🛏️", "📚", "🧹", "🏃", "🎨", "🎵", "🌱", "🍳", "🧩", "📝"];

if ("serviceWorker" in navigator) {
  navigator.serviceWorker.register("sw.js");
}

let currentChallengeId = null;
let currentMemberId = null;
let isRegisterMode = false;
let cachedChallenges = [];
let cachedProgressMap = {};
let cachedFamilies = [];
let currentUserId = null;
let currentUserEmail = null;

function authHeaders() {
  const token = localStorage.getItem("token");
  return token ? { "Authorization": "Bearer " + token } : {};
}

const CACHE_PREFIX = "apiCache_";
const OFFLINE_QUEUE_KEY = "offlineQueue";

function getCacheKey(path) { return CACHE_PREFIX + path; }

function getCachedResponse(path) {
  try {
    const stored = localStorage.getItem(getCacheKey(path));
    return stored ? JSON.parse(stored) : null;
  } catch { return null; }
}

function setCachedResponse(path, data) {
  try {
    localStorage.setItem(getCacheKey(path), JSON.stringify(data));
  } catch { /* storage full, ignore */ }
}

function clearCache() {
  const keys = Object.keys(localStorage).filter(k => k.startsWith(CACHE_PREFIX));
  keys.forEach(k => localStorage.removeItem(k));
}

function getOfflineQueue() {
  try {
    const stored = localStorage.getItem(OFFLINE_QUEUE_KEY);
    return stored ? JSON.parse(stored) : [];
  } catch { return []; }
}

function setOfflineQueue(queue) {
  try {
    localStorage.setItem(OFFLINE_QUEUE_KEY, JSON.stringify(queue));
  } catch { /* storage full, ignore */ }
}

function addToOfflineQueue(path, options, body) {
  const queue = getOfflineQueue();
  queue.push({ path, options, body, timestamp: new Date().toISOString() });
  setOfflineQueue(queue);
}

async function replayOfflineQueue() {
  const queue = getOfflineQueue();
  if (queue.length === 0) return;
  const remaining = [];
  for (const item of queue) {
    try {
      const res = await fetch(API + item.path, { ...item.options, body: item.body });
      if (!res.ok) { remaining.push(item); continue; }
    } catch {
      remaining.push(item);
    }
  }
  setOfflineQueue(remaining);
  if (remaining.length < queue.length) {
    showToast("Sync Complete", `${queue.length - remaining.length} offline activities synced!`, "success");
  }
}

async function apiFetch(path, options = {}) {
  const headers = { ...authHeaders(), ...options.headers };
  if (options.body && !(options.body instanceof FormData) && !headers["Content-Type"]) {
    headers["Content-Type"] = "application/x-www-form-urlencoded";
  }

  const method = (options.method || "GET").toUpperCase();

  if (method === "GET") {
    const cached = getCachedResponse(path);
    try {
      const res = await fetch(API + path, { ...options, headers });
      if (res.status === 401) {
        localStorage.removeItem("token");
        window.location.reload();
      }
      if (res.ok) {
        const data = await res.json();
        setCachedResponse(path, data);
        return { ok: true, json: async () => data, status: res.status, headers: res.headers };
      }
      if (cached && !res.ok) {
        return { ok: true, json: async () => cached, status: 200, headers: res.headers, _fromCache: true };
      }
      return { ok: false, status: res.status, json: async () => ({ error: "Request failed" }) };
    } catch {
      if (cached) {
        showToast("Offline Mode", "Showing cached data", "info");
        return { ok: true, json: async () => cached, status: 200, headers: new Headers(), _fromCache: true };
      }
      throw new Error("Network error and no cached data available");
    }
  }

  try {
    const res = await fetch(API + path, { ...options, headers });
    if (res.status === 401) {
      localStorage.removeItem("token");
      window.location.reload();
    }
    return res;
  } catch {
    addToOfflineQueue(path, options, options.body);
    showToast("Offline", "Activity queued — will sync when online", "info");
    return { ok: true, json: async () => ({}), _queued: true };
  }
}

function escapeHtml(str) {
  const div = document.createElement("div");
  div.textContent = str;
  return div.innerHTML;
}

function timeAgo(dateStr) {
  const now = new Date();
  const date = new Date(dateStr);
  const ms = now - date;
  const mins = Math.floor(ms / 60000);
  if (mins < 1) return "just now";
  if (mins < 60) return mins + "m ago";
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return hrs + "h ago";
  const days = Math.floor(hrs / 24);
  if (days < 7) return days + "d ago";
  return date.toLocaleDateString();
}

function getDayGreeting() {
  const days = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
  const day = days[new Date().getDay()];
  const times = ["Morning", "Afternoon", "Evening"];
  const hour = new Date().getHours();
  const time = hour < 12 ? times[0] : hour < 17 ? times[1] : times[2];
  return `Happy ${day} ${time}`;
}

function base64UrlDecode(str) {
  str = str.replace(/-/g, "+").replace(/_/g, "/");
  str = str.padEnd(str.length + (4 - str.length % 4) % 4, "=");
  return atob(str);
}

function decodeUserId() {
  const token = localStorage.getItem("token");
  if (!token) return null;
  try {
    const payload = JSON.parse(base64UrlDecode(token.split(".")[1]));
    return payload["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"];
  } catch { return null; }
}

// ========== TOAST & CONFETTI ==========

function spawnConfetti() {
  const colors = ["#4f46e5", "#f59e0b", "#10b981", "#ef4444", "#8b5cf6", "#ec4899"];
  for (let i = 0; i < 40; i++) {
    const piece = document.createElement("div");
    piece.className = "confetti-piece";
    piece.style.left = Math.random() * 100 + "vw";
    piece.style.top = "-10px";
    piece.style.background = colors[Math.floor(Math.random() * colors.length)];
    piece.style.width = (Math.random() * 6 + 4) + "px";
    piece.style.height = (Math.random() * 6 + 4) + "px";
    piece.style.borderRadius = Math.random() > 0.5 ? "50%" : "2px";
    piece.style.animationDuration = (Math.random() * 1.5 + 1.5) + "s";
    piece.style.animationDelay = (Math.random() * 0.5) + "s";
    document.body.appendChild(piece);
    setTimeout(() => piece.remove(), 3000);
  }
}

function showToast(title, description, type) {
  const container = document.getElementById("toast-container");
  if (!container) return;

  const bg = type === "surprise" ? "bg-gradient-to-r from-amber-400 to-orange-500"
    : type === "success" ? "bg-green-500"
    : type === "error" ? "bg-red-500"
    : "bg-indigo-600";

  const toast = document.createElement("div");
  toast.className = `${bg} text-white rounded-2xl p-4 shadow-lg pointer-events-auto animate-slide-down flex items-start gap-3`;
  toast.innerHTML = `
    <span class="text-xl shrink-0">${type === "surprise" ? "🎉" : type === "success" ? "✅" : type === "error" ? "❌" : "ℹ️"}</span>
    <div class="flex-1 min-w-0">
      <div class="font-bold text-sm">${escapeHtml(title)}</div>
      ${description ? `<div class="text-xs opacity-90 mt-0.5">${escapeHtml(description)}</div>` : ""}
    </div>
    <button onclick="this.parentElement.remove()" class="text-white/80 hover:text-white shrink-0 text-lg leading-none">&times;</button>
  `;
  container.appendChild(toast);
  setTimeout(() => { toast.style.opacity = "0"; toast.style.transform = "translateY(-20px)"; toast.style.transition = "all 0.3s"; setTimeout(() => toast.remove(), 300); }, 5000);
}

// ========== AUTH ==========

function showAuth() {
  document.getElementById("auth-section").classList.remove("hidden");
  document.getElementById("dashboard").classList.add("hidden");
  document.getElementById("bottom-nav").classList.add("hidden");
  clearAuthError();
}

function showDashboard(email) {
  document.getElementById("auth-section").classList.add("hidden");
  document.getElementById("dashboard").classList.remove("hidden");
  document.getElementById("bottom-nav").classList.remove("hidden");

  currentUserId = decodeUserId();
  currentUserEmail = email || "";

  const storedUsername = localStorage.getItem("username");
  const displayName = storedUsername || (email ? email.split("@")[0] : "User");
  const capitalized = displayName.charAt(0).toUpperCase() + displayName.slice(1);
  document.getElementById("user-name").textContent = capitalized;
  document.getElementById("profile-name").textContent = capitalized;
  document.getElementById("profile-email").textContent = email || "";
  document.getElementById("greeting-day").textContent = getDayGreeting();

  const initial = displayName[0].toUpperCase();
  document.getElementById("user-avatar").textContent = initial;
  document.getElementById("profile-avatar").textContent = initial;

  loadFamilies();
  loadAllData();
  switchTab("home");

  const params = new URLSearchParams(window.location.search);
  const claimParam = params.get("claim");
  if (claimParam) {
    const parts = claimParam.split(":");
    if (parts.length === 2) {
      processRedemption(parts[0], parts[1]);
    }
    window.history.replaceState({}, "", window.location.pathname);
  }
}

function logout() {
  closeScanner();
  localStorage.removeItem("token");
  localStorage.removeItem("username");
  currentChallengeId = null;
  currentMemberId = null;
  isRegisterMode = false;
  cachedChallenges = [];
  cachedProgressMap = {};
  cachedFamilies = [];
  currentUserId = null;
  currentUserEmail = null;
  showAuth();
}

function clearAuthError() {
  const el = document.getElementById("auth-error");
  el.classList.add("hidden");
  el.textContent = "";
}

function toggleAuthForm() {
  isRegisterMode = !isRegisterMode;
  const title = document.getElementById("auth-form-title");
  const btn = document.getElementById("auth-submit-btn");
  const toggle = document.getElementById("auth-toggle");
  const usernameGroup = document.getElementById("auth-username-group");
  if (isRegisterMode) {
    title.textContent = "Create Account";
    btn.textContent = "REGISTER";
    usernameGroup.classList.remove("hidden");
    toggle.innerHTML = '<span class="text-slate-500">Already have an account?</span> <button type="button" onclick="toggleAuthForm()" class="font-bold text-indigo-600 ml-1">LOG IN</button>';
  } else {
    title.textContent = "Log In";
    btn.textContent = "LOG IN";
    usernameGroup.classList.add("hidden");
    toggle.innerHTML = '<span class="text-slate-500">No account yet?</span> <button type="button" onclick="toggleAuthForm()" class="font-bold text-indigo-600 ml-1">CREATE AN ACCOUNT</button>';
  }
  clearAuthError();
}

async function authFetch(endpoint, email, password) {
  const formData = new URLSearchParams({ email, password });
  if (isRegisterMode) {
    const username = document.getElementById("auth-username").value.trim();
    if (username) formData.set("username", username);
  }
  const res = await fetch(API + endpoint, {
    method: "POST",
    body: formData,
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
  });
  if (!res.ok) {
    const data = await res.json().catch(() => ({}));
    throw new Error(data.error || "Request failed");
  }
  return res.json();
}

// ========== TAB SWITCHING ==========

function switchTab(tab) {
  ["home", "quests", "treasury", "profile"].forEach(t => {
    document.getElementById("tab-" + t).classList.toggle("hidden", t !== tab);
  });
  document.querySelectorAll(".tab-btn").forEach(btn => {
    const isActive = btn.dataset.tab === tab;
    btn.classList.toggle("text-indigo-600", isActive);
    btn.classList.toggle("text-slate-400", !isActive);
  });
  if (tab === "treasury") loadTreasury();
  if (tab === "home") refreshPoints();
}

// ========== DOMContentLoaded ==========

document.addEventListener("DOMContentLoaded", () => {
  const token = localStorage.getItem("token");
  if (token) {
    try {
      const payload = JSON.parse(base64UrlDecode(token.split(".")[1]));
      const email = payload["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"];
      const username = payload["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"];
      if (username) localStorage.setItem("username", username);
      if (email) showDashboard(email);
      else showAuth();
    } catch {
      localStorage.removeItem("token");
      showAuth();
    }
  } else {
    showAuth();
  }

  document.getElementById("login-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    const errorDiv = document.getElementById("auth-error");
    const email = document.getElementById("auth-email").value;
    const password = document.getElementById("auth-password").value;
    try {
      const endpoint = isRegisterMode ? "/api/auth/register" : "/api/auth/login";
      const data = await authFetch(endpoint, email, password);
      localStorage.setItem("token", data.token);
      if (data.userName) localStorage.setItem("username", data.userName);
      showDashboard(data.email);
      clearAuthError();
    } catch (err) {
      errorDiv.textContent = err.message;
      errorDiv.classList.remove("hidden");
    }
  });

  document.getElementById("auth-email").addEventListener("input", () => {
    const val = document.getElementById("auth-email").value;
    const el = document.getElementById("email-validation");
    if (val.includes("@") && val.includes(".")) {
      el.classList.remove("hidden");
    } else {
      el.classList.add("hidden");
    }
  });

  document.getElementById("create-family-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    const name = document.getElementById("family-name").value;
    const formData = new URLSearchParams({ name });
    const res = await apiFetch("/api/families", { method: "POST", body: formData });
    if (res.ok) {
      document.getElementById("family-name").value = "";
      loadFamilies();
    } else {
      const data = await res.json().catch(() => ({}));
      alert(data.error || "Failed to create family");
    }
  });

  document.getElementById("join-family-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    const inviteCode = document.getElementById("invite-code").value;
    const formData = new URLSearchParams({ inviteCode });
    const res = await apiFetch("/api/families/join", { method: "POST", body: formData });
    if (res.ok) {
      document.getElementById("invite-code").value = "";
      loadFamilies();
    } else {
      const data = await res.json().catch(() => ({}));
      alert(data.error || "Failed to join family");
    }
  });

  // Online/offline handling
  window.addEventListener("online", () => {
    document.getElementById("offline-banner")?.classList.add("hidden");
    replayOfflineQueue().then(() => loadAllData());
  });
  window.addEventListener("offline", () => {
    document.getElementById("offline-banner")?.classList.remove("hidden");
  });

  // Replay any pending queue on page load
  replayOfflineQueue();
});

// ========== DATA LOADING ==========

async function loadAllData() {
  await Promise.all([loadChallenges(), loadAchievements()]).catch(() => {});
  renderChronicleFeed();
}

async function loadChallenges() {
  const res = await apiFetch("/api/challenges");
  if (!res.ok) return;
  cachedChallenges = await res.json();

  const progressPromises = cachedChallenges.map(async c => {
    try {
      const r = await apiFetch(`/api/challenges/${c.id}/progress`);
      if (!r.ok) return null;
      return r.json();
    } catch { return null; }
  });
  const progressData = await Promise.all(progressPromises);
  cachedProgressMap = {};
  cachedChallenges.forEach((c, i) => { cachedProgressMap[c.id] = progressData[i]; });

  renderUpNext();
  renderQuickLog();
  renderQuestList();
  refreshPoints();
}

async function loadFamilies() {
  const res = await apiFetch("/api/families");
  if (!res.ok) return;
  cachedFamilies = await res.json();
  const list = document.getElementById("family-list");
  if (cachedFamilies.length === 0) {
    list.innerHTML = "<p class='text-sm text-slate-500'>No families yet. Create or join one above.</p>";
    return;
  }
  list.innerHTML = cachedFamilies.map(f => `
    <div class="bg-white rounded-2xl p-4 shadow-sm border border-slate-100 cursor-pointer" onclick="loadFamilyDetail('${f.id}')">
      <div class="font-bold text-slate-800">${escapeHtml(f.name)}</div>
      <div class="text-sm text-slate-500">Code: ${escapeHtml(f.inviteCode)} &middot; ${f.members.length} members</div>
    </div>
  `).join("");

  const familySelect = document.getElementById("challenge-family");
  if (familySelect) {
    const currentValue = familySelect.value;
    familySelect.innerHTML = '<option value="">Select a family...</option>' +
      cachedFamilies.map(f => `<option value="${f.id}">${escapeHtml(f.name)}</option>`).join("");
    if (currentValue) familySelect.value = currentValue;
  }
}

async function loadAchievements() {
  const res = await apiFetch("/api/achievements");
  if (!res.ok) return;
  const achievements = await res.json();
  const list = document.getElementById("achievement-list");
  const count = document.getElementById("achievement-count");
  if (achievements.length === 0) {
    list.innerHTML = "<p class='text-sm text-slate-500'>No achievements yet. Complete goals to unlock them!</p>";
    count.textContent = "0";
    return;
  }
  count.textContent = achievements.length;
  list.innerHTML = achievements.map(a => `
    <div class="bg-amber-50 rounded-2xl p-4 shadow-sm border border-amber-100 flex items-center gap-3">
      <span class="text-2xl">🏆</span>
      <div>
        <div class="font-bold text-slate-800">${escapeHtml(a.title)}</div>
        <div class="text-xs text-slate-500">unlocked ${new Date(a.unlockedAt).toLocaleDateString()}</div>
      </div>
    </div>
  `).join("");
}

// ========== QUICK LOG ==========

function collectAllActivities() {
  const activities = [];
  const seen = new Set();
  for (const c of cachedChallenges) {
    if (c.activities) {
      for (const a of c.activities) {
        const key = c.id + ":" + a.id;
        if (!seen.has(key)) {
          seen.add(key);
          activities.push({ ...a, challengeId: c.id, challengeTitle: c.title });
        }
      }
    }
    if (c.goals) {
      for (const g of c.goals) {
        if (g.activities) {
          for (const a of g.activities) {
            const key = c.id + ":" + a.id;
            if (!seen.has(key)) {
              seen.add(key);
              activities.push({ ...a, challengeId: c.id, challengeTitle: c.title, goalId: g.id });
            }
          }
        }
      }
    }
  }
  return activities;
}

function getRecentQuickLogIds() {
  try {
    const stored = localStorage.getItem("quickLogIds");
    return stored ? JSON.parse(stored) : [];
  } catch { return []; }
}

function recordQuickLog(activityId) {
  const ids = getRecentQuickLogIds().filter(id => id !== activityId);
  ids.unshift(activityId);
  if (ids.length > 5) ids.length = 5;
  localStorage.setItem("quickLogIds", JSON.stringify(ids));
}

function renderQuickLog() {
  const container = document.getElementById("quick-log-list");
  const section = document.getElementById("quick-log-section");
  const allActivities = collectAllActivities();
  if (allActivities.length === 0) {
    section.classList.add("hidden");
    return;
  }

  const recentIds = getRecentQuickLogIds();
  const sorted = [...allActivities].sort((a, b) => {
    const aIdx = recentIds.indexOf(a.id);
    const bIdx = recentIds.indexOf(b.id);
    if (aIdx !== -1 && bIdx !== -1) return aIdx - bIdx;
    if (aIdx !== -1) return -1;
    if (bIdx !== -1) return 1;
    return 0;
  });
  const top = sorted.slice(0, 5);

  container.innerHTML = top.map(a => {
    const isDistTime = a.activityType === "DistanceAndTime";
    return `
      <form onsubmit="quickLogSubmit('${a.challengeId}', '${a.id}', event)" class="bg-white rounded-2xl p-3 shadow-sm border border-slate-100 flex flex-col gap-2">
        <div class="flex items-center justify-between">
          <span class="text-sm font-bold text-slate-700 truncate">${escapeHtml(a.name)}</span>
          <span class="text-xs text-slate-400 shrink-0 ml-2">${escapeHtml(a.challengeTitle)}</span>
        </div>
        <div class="flex items-center gap-2">
          ${isDistTime ? `
            <input type="number" inputmode="decimal" step="any" placeholder="Dist (${escapeHtml(a.unit)})" required
              class="ql-dist w-20 py-1.5 px-2 bg-slate-100 rounded-lg border border-slate-200 focus:ring-2 focus:ring-indigo-300 outline-none text-xs">
            <input type="number" inputmode="decimal" step="any" placeholder="Time" required
              class="ql-time w-20 py-1.5 px-2 bg-slate-100 rounded-lg border border-slate-200 focus:ring-2 focus:ring-indigo-300 outline-none text-xs">
          ` : `
            <input type="number" inputmode="decimal" step="any" placeholder="Amount" required
              class="ql-amount w-20 py-1.5 px-2 bg-slate-100 rounded-lg border border-slate-200 focus:ring-2 focus:ring-indigo-300 outline-none text-xs">
          `}
          <button type="submit" class="py-1.5 px-4 bg-indigo-600 text-white font-bold rounded-lg hover:bg-indigo-700 transition-colors text-xs whitespace-nowrap">+ Log</button>
        </div>
      </form>
    `;
  }).join("");
  section.classList.remove("hidden");
}

async function quickLogSubmit(challengeId, activityId, event) {
  event.preventDefault();
  const form = event.target;
  const distInput = form.querySelector(".ql-dist");
  const timeInput = form.querySelector(".ql-time");
  const amountInput = form.querySelector(".ql-amount");

  let amount, timeAmount;
  if (distInput && timeInput) {
    amount = parseFloat(distInput.value);
    timeAmount = parseFloat(timeInput.value);
    if (isNaN(amount) || isNaN(timeAmount)) return;
  } else {
    amount = parseFloat(amountInput.value);
    if (isNaN(amount)) return;
  }

  const body = { amount };
  if (timeAmount != null) body.timeAmount = timeAmount;

  const res = await apiFetch(`/api/challenges/${challengeId}/activities/${activityId}/log`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });

  if (res.ok) {
    const data = await res.json();
    recordQuickLog(activityId);
    if (data.surprise) {
      showToast(data.surprise.title, data.surprise.description, "surprise");
    }
    if (data.currencyEarned) {
      showToast("Points Earned", `You earned ${data.currencyEarned} points!`, "success");
      spawnConfetti();
    }
    loadAllData();
  } else {
    const data = await res.json().catch(() => ({}));
    alert(data.error || "Failed to log activity");
  }
}

// ========== HOME TAB ==========

function renderUpNext() {
  const container = document.getElementById("up-next-list");
  if (cachedChallenges.length === 0) {
    container.innerHTML = "<p class='text-slate-500 text-sm'>No challenges yet. Head to Quests to create one!</p>";
    return;
  }
  container.innerHTML = cachedChallenges.map((c, i) => {
    const p = cachedProgressMap[c.id];
    const completed = p ? p.progress.filter(g => g.isCompleted).length : 0;
    const total = p ? p.progress.length : c.goals.length;
    const activityCount = c.activities?.length ?? 0;
    const summary = total > 0 ? `${completed}/${total} goals done` : `${activityCount} activities`;
    const emoji = EMOJIS[i % EMOJIS.length];
    const badgeColor = c.type === "SelfOnly" ? "text-orange-500 bg-orange-50" : "text-blue-500 bg-blue-50";
    const balance = p ? (p.currencyBalance || 0) : 0;
    const streak = p ? (p.currentStreak || 0) : 0;
    const currencyName = p ? p.currencyName : (c.currencyName || null);
    return `
      <div class="snap-start shrink-0 w-64 bg-white rounded-2xl p-4 shadow-sm border border-slate-100 flex flex-col justify-between cursor-pointer" onclick="showProgress('${c.id}')">
        <div>
          <div class="flex items-start justify-between mb-3">
            <div class="h-10 w-10 rounded-xl bg-indigo-100 flex items-center justify-center text-xl">${emoji}</div>
            <span class="font-bold ${badgeColor} px-2 py-1 rounded-lg text-sm">${c.type === "SelfOnly" ? "Personal" : "Family"}</span>
          </div>
          <h3 class="font-bold text-lg text-slate-700 leading-tight">${escapeHtml(c.title)}</h3>
          <p class="text-xs text-slate-500 mt-1">${summary}</p>
        </div>
        ${currencyName ? `<div class="mt-2 flex items-center gap-2 text-sm font-semibold text-amber-600">
          <span>💰 ${balance} ${escapeHtml(currencyName)}</span>
          ${streak > 0 ? `<span>🔥 ${streak} day streak</span>` : ""}
        </div>` : streak > 0 ? `<div class="mt-2 flex items-center gap-2 text-sm font-semibold text-amber-600">
          <span>🔥 ${streak} day streak</span>
        </div>` : ""}
      </div>
    `;
  }).join("");
}

let chronicleOffset = 0;
let chronicleLoading = false;
let chronicleDone = false;
let chronicleObserver = null;

async function renderChronicleFeed() {
  const container = document.getElementById("chronicle-feed");
  chronicleOffset = 0;
  chronicleDone = false;
  chronicleLoading = false;
  container.innerHTML = "";
  await loadMoreChronicle();
  setupChronicleInfiniteScroll();
}

async function loadMoreChronicle() {
  if (chronicleLoading || chronicleDone) return;
  chronicleLoading = true;
  const container = document.getElementById("chronicle-feed");
  try {
    const res = await apiFetch(`/api/chronicle?offset=${chronicleOffset}&limit=20`);
    if (!res.ok) { chronicleLoading = false; return; }
    const entries = await res.json();
    if (entries.length === 0) {
      chronicleDone = true;
      if (chronicleOffset === 0) {
        container.innerHTML = "<p class='text-sm text-slate-500'>No activity yet. Log some progress in Quests!</p>";
      }
      chronicleLoading = false;
      return;
    }
    chronicleOffset += entries.length;
    const userIcons = {};
    container.insertAdjacentHTML("beforeend", entries.map(e => {
      const email = e.userEmail || "";
      if (email && !userIcons[email]) userIcons[email] = email[0].toUpperCase();
      return e.type === "redemption"
        ? `<div class="bg-white rounded-2xl p-4 shadow-sm border border-slate-100 flex items-center gap-4">
            <div class="h-12 w-12 rounded-full bg-amber-100 flex items-center justify-center text-xl shrink-0 font-bold text-amber-600">${userIcons[e.userEmail]}</div>
            <div class="flex-1 min-w-0">
              <p class="text-sm text-slate-600"><span class="font-bold text-slate-800">${escapeHtml(e.userEmail ? e.userEmail.split('@')[0] : "someone")}</span> redeemed</p>
              <p class="font-bold text-slate-800 truncate">🏅 ${escapeHtml(e.prizeDescription)}</p>
            </div>
            <div class="text-right shrink-0">
              ${e.cost != null ? `<span class="font-bold text-amber-500 bg-amber-50 px-2 py-1 rounded-lg text-sm">${e.cost} pts</span>` : '<span class="font-bold text-green-500 bg-green-50 px-2 py-1 rounded-lg text-sm">Free</span>'}
              <p class="text-xs text-slate-400 mt-1">${timeAgo(e.recordedAt)}</p>
            </div>
          </div>`
        : `<div class="bg-white rounded-2xl p-4 shadow-sm border border-slate-100 flex items-center gap-4">
            <div class="h-12 w-12 rounded-full bg-indigo-100 flex items-center justify-center text-xl shrink-0 font-bold text-indigo-600">${userIcons[e.userEmail]}</div>
            <div class="flex-1 min-w-0">
              <p class="text-sm text-slate-600"><span class="font-bold text-slate-800">${escapeHtml(e.userEmail ? e.userEmail.split('@')[0] : "someone")}</span> logged</p>
              <p class="font-bold text-slate-800 truncate">${escapeHtml(e.activityName)}</p>
            </div>
            <div class="text-right shrink-0">
              <span class="font-bold text-green-500 bg-green-50 px-2 py-1 rounded-lg text-sm">+${e.amount} ${escapeHtml(e.unit || "")}</span>
              ${e.currencyEarned ? `<p class="text-xs font-bold text-amber-600 mt-0.5">+${e.currencyEarned} pts</p>` : ""}
              <p class="text-xs text-slate-400 mt-1">${timeAgo(e.recordedAt)}</p>
            </div>
          </div>`;
    }).join(""));
  } catch { }
  chronicleLoading = false;
}

function setupChronicleInfiniteScroll() {
  if (chronicleObserver) chronicleObserver.disconnect();
  const sentinel = document.createElement("div");
  sentinel.id = "chronicle-sentinel";
  sentinel.className = "h-4";
  document.getElementById("chronicle-feed").after(sentinel);
  chronicleObserver = new IntersectionObserver(async (entries) => {
    if (entries[0].isIntersecting) {
      await loadMoreChronicle();
    }
  }, { rootMargin: "200px" });
  chronicleObserver.observe(sentinel);
}

async function refreshPoints() {
  const currencies = {};
  let total = 0;
  for (const c of cachedChallenges) {
    const p = cachedProgressMap[c.id];
    if (p && p.currencyName) {
      const bal = p.currencyBalance || 0;
      currencies[p.currencyName] = (currencies[p.currencyName] || 0) + bal;
      total += bal;
    }
  }
  const entries = Object.entries(currencies);
  const symbol = entries.length === 1 ? ` ${entries[0][0]}` : entries.length > 1 ? " pts" : " 🍦";
  document.getElementById("user-points").textContent = total + symbol;
}

// ========== CHALLENGE MODAL / WIZARD ==========

let editChallengeData = null;
let wizardCurrentStep = 1;

function wizardGoTo(step) {
  wizardCurrentStep = step;
  document.querySelectorAll(".wizard-step").forEach(el => {
    el.classList.add("hidden");
  });
  document.getElementById("wizard-step-" + step).classList.remove("hidden");
  document.querySelectorAll(".step-indicator").forEach(el => {
    const s = parseInt(el.dataset.step);
    const bar = el.querySelector("div");
    const label = el.querySelector("span");
    if (s === step) {
      bar.classList.remove("bg-slate-200", "bg-green-500");
      bar.classList.add("bg-indigo-600");
      label.classList.remove("text-slate-400");
      label.classList.add("text-indigo-600");
    } else if (s < step) {
      bar.classList.remove("bg-slate-200", "bg-indigo-600");
      bar.classList.add("bg-green-500");
      label.classList.remove("text-slate-400", "text-indigo-600");
      label.classList.add("text-green-600");
    } else {
      bar.classList.remove("bg-green-500", "bg-indigo-600");
      bar.classList.add("bg-slate-200");
      label.classList.remove("text-green-600", "text-indigo-600");
      label.classList.add("text-slate-400");
    }
  });
  
  // Rebuild prize goal dropdowns when entering step 3
  if (step === 3) {
    refreshPrizeGoalDropdowns();
  }
}

function wizardNext() {
  if (wizardCurrentStep < 3) wizardGoTo(wizardCurrentStep + 1);
}

function wizardPrev() {
  if (wizardCurrentStep > 1) wizardGoTo(wizardCurrentStep - 1);
}

function toggleChallengeFamilySelect() {
  const type = document.getElementById("challenge-type").value;
  document.getElementById("family-select-group").classList.toggle("hidden", type === "SelfOnly");
}

function togglePrizeCostInputs() {
  const hasCurrency = !!document.getElementById("challenge-currency").value.trim();
  document.querySelectorAll(".prize-cost").forEach(input => {
    input.disabled = !hasCurrency;
    if (!hasCurrency) input.value = "";
  });
}

function toggleGoalFields(selectEl) {
  const type = selectEl.value;
  const container = selectEl.closest('.goal-field');
  const targetGroup = container.querySelector('.goal-target-group');
  if (type === 'Collection') {
    targetGroup.classList.add('hidden');
    targetGroup.querySelectorAll('input').forEach(i => i.removeAttribute('required'));
  } else {
    targetGroup.classList.remove('hidden');
  }
}

function toggleActivityFields(selectEl) {
  const type = selectEl.value;
  const container = selectEl.closest('.activity-field, .challenge-activity-field');
  const unit = container.querySelector('.act-unit');
  const timeunit = container.querySelector('.act-timeunit');
  
  unit.classList.remove('hidden');
  timeunit.classList.add('hidden');
  
  if (type === 'Occurrence') {
    unit.classList.add('hidden');
    unit.removeAttribute('required');
    timeunit.classList.add('hidden');
    timeunit.removeAttribute('required');
  } else if (type === 'Distance') {
    unit.placeholder = 'Unit (km, mi)';
  } else if (type === 'Time') {
    unit.placeholder = 'Unit (min, hr)';
  } else if (type === 'DistanceAndTime') {
    unit.placeholder = 'Dist Unit (km)';
    timeunit.classList.remove('hidden');
    timeunit.placeholder = 'Time Unit (min)';
  }
}

function showCreateChallengeForm() {
  editChallengeData = null;
  document.getElementById("challenge-modal-title").textContent = "Create Challenge";
  document.getElementById("challenge-submit-btn").textContent = "Create";
  document.getElementById("challenge-edit-id").value = "";
  document.getElementById("challenge-title").value = "";
  document.getElementById("challenge-description").value = "";
  document.getElementById("challenge-type").value = "SelfOnly";
  document.getElementById("challenge-currency").value = "";
  document.getElementById("goals-container").innerHTML = "";
  document.getElementById("challenge-activities-container").innerHTML = "";
  document.getElementById("prizes-container").innerHTML = "";
  toggleChallengeFamilySelect();

  const familySelect = document.getElementById("challenge-family");
  familySelect.innerHTML = '<option value="">Select a family...</option>' +
    cachedFamilies.map(f => `<option value="${f.id}">${escapeHtml(f.name)}</option>`).join("");

  addGoalField();
  addPrizeField();
  document.getElementById("challenge-modal").classList.remove("hidden");
  togglePrizeCostInputs();
  wizardGoTo(1);
}

function showEditChallengeForm(challengeId) {
  const c = cachedChallenges.find(x => x.id === challengeId);
  if (!c) return;

  editChallengeData = c;
  document.getElementById("challenge-modal-title").textContent = "Edit Challenge";
  document.getElementById("challenge-submit-btn").textContent = "Save Changes";
  document.getElementById("challenge-edit-id").value = c.id;
  document.getElementById("challenge-title").value = c.title;
  document.getElementById("challenge-description").value = c.description;
  document.getElementById("challenge-type").value = c.type;
  document.getElementById("challenge-currency").value = c.currencyName || "";
  toggleChallengeFamilySelect();

  const familySelect = document.getElementById("challenge-family");
  familySelect.innerHTML = '<option value="">Select a family...</option>' +
    cachedFamilies.map(f => `<option value="${f.id}">${escapeHtml(f.name)}</option>`).join("");
  if (c.familyId) familySelect.value = c.familyId;

  document.getElementById("goals-container").innerHTML = "";
  if (c.goals && c.goals.length > 0) {
    c.goals.forEach(g => addGoalField(g));
  } else {
    addGoalField();
  }

  document.getElementById("challenge-activities-container").innerHTML = "";
  if (c.activities && c.activities.length > 0) {
    c.activities.forEach(a => addChallengeActivityField(a));
  }

  document.getElementById("prizes-container").innerHTML = "";
  if (c.prizes && c.prizes.length > 0) {
    c.prizes.forEach(p => addPrizeField(p, c.goals));
  } else {
    addPrizeField();
  }

  document.getElementById("challenge-modal").classList.remove("hidden");
  togglePrizeCostInputs();
  wizardGoTo(1);
}

function closeChallengeModal() {
  document.getElementById("challenge-modal").classList.add("hidden");
  editChallengeData = null;
  wizardCurrentStep = 1;
}

function addGoalField(goal) {
  const container = document.getElementById("goals-container");
  const idx = container.children.length;
  const div = document.createElement("div");
  div.className = "goal-field bg-slate-50 rounded-xl p-3 mb-2 relative";
  div.dataset.index = idx;
  if (goal && goal.id) div.dataset.editId = goal.id;

  const activitiesHtml = goal && goal.activities && goal.activities.length > 0
    ? goal.activities.map(a => makeActivityHtml(a)).join("")
    : makeActivityHtml();

  const isHidden = goal && goal.isHidden;
  const isCollection = goal && goal.type === 'Collection';

  div.innerHTML = `
    <button type="button" onclick="(!this.closest('.goal-field').dataset.editId || confirm('Removing this goal will delete all past progress logs when saved. Are you sure?')) && this.closest('.goal-field').remove()" class="absolute top-2 right-2 text-slate-400 hover:text-red-500 text-lg leading-none">&times;</button>
    <div class="grid grid-cols-2 gap-2 mb-2">
      <input type="text" placeholder="Goal description" value="${goal ? escapeHtml(goal.description) : ""}" required
        class="goal-desc w-full py-2 px-2 bg-white rounded-lg border border-slate-200 focus:ring-2 focus:ring-indigo-300 outline-none text-sm">
      <select onchange="toggleGoalFields(this)" class="goal-type w-full py-2 px-2 bg-white rounded-lg border border-slate-200 focus:ring-2 focus:ring-indigo-300 outline-none text-sm">
        <option value="Achievement" ${(!goal || goal.type === "Achievement") ? "selected" : ""}>Achievement</option>
        <option value="Collection" ${(goal && goal.type === "Collection") ? "selected" : ""}>Collection</option>
      </select>
    </div>
    <div class="goal-target-group grid grid-cols-2 gap-2 mb-2 ${isCollection ? 'hidden' : ''}">
      <input type="number" inputmode="decimal" step="any" placeholder="Target" value="${goal && goal.targetValue != null ? goal.targetValue : ""}"
        class="goal-target w-full py-2 px-2 bg-white rounded-lg border border-slate-200 focus:ring-2 focus:ring-indigo-300 outline-none text-sm">
      <input type="text" placeholder="Unit" value="${goal && goal.unit ? escapeHtml(goal.unit) : ""}"
        class="goal-unit w-full py-2 px-2 bg-white rounded-lg border border-slate-200 focus:ring-2 focus:ring-indigo-300 outline-none text-sm">
    </div>
    <div class="mb-2">
      <button type="button" onclick="toggleGoalAdvanced(this)" class="text-xs font-medium text-slate-500 hover:text-indigo-600 flex items-center gap-1">
        <span>⚙️ Advanced</span>
        <span class="advanced-arrow text-xs">▸</span>
      </button>
      <div class="goal-advanced hidden mt-2 pl-2 border-l-2 border-slate-200">
        <label class="flex items-center gap-2 text-sm text-slate-600">
          <input type="checkbox" class="goal-hidden" ${isHidden ? "checked" : ""}>
          Hidden goal — surprises user on completion
        </label>
      </div>
    </div>
    <div class="activities-container">
      ${activitiesHtml}
    </div>
    <button type="button" onclick="addActivityToGoal(this)" class="text-xs font-medium text-indigo-600 hover:text-indigo-800 mt-1">+ Add Activity</button>
  `;
  container.appendChild(div);

  reindexGoals();
}

function toggleGoalAdvanced(btn) {
  const advanced = btn.closest(".goal-field").querySelector(".goal-advanced");
  const arrow = btn.querySelector(".advanced-arrow");
  advanced.classList.toggle("hidden");
  arrow.textContent = advanced.classList.contains("hidden") ? "▸" : "▾";
}

function makeActivityHtml(activity) {
  const actType = activity ? activity.activityType : "Occurrence";
  const showUnit = actType !== "Occurrence";
  const showTimeUnit = actType === "DistanceAndTime";
  return `
    <div class="activity-field p-2 mb-1.5 bg-white rounded-lg border border-slate-100">
      ${activity && activity.id ? `<input type="hidden" class="act-id" value="${activity.id}">` : ""}
      <div class="grid grid-cols-[1fr_auto_auto] gap-1.5 items-center mb-1.5">
        <input type="text" placeholder="Name" value="${activity ? escapeHtml(activity.name) : ""}" required
          class="act-name w-full py-1.5 px-2 bg-white rounded-lg border border-slate-200 focus:ring-2 focus:ring-indigo-300 outline-none text-xs">
        <select onchange="toggleActivityFields(this)" class="act-type py-1.5 px-1 bg-white rounded-lg border border-slate-200 text-xs">
          <option value="Occurrence" ${actType === "Occurrence" ? "selected" : ""}>Occurrence</option>
          <option value="Distance" ${actType === "Distance" ? "selected" : ""}>Distance</option>
          <option value="Time" ${actType === "Time" ? "selected" : ""}>Time</option>
          <option value="DistanceAndTime" ${actType === "DistanceAndTime" ? "selected" : ""}>Dist+Time</option>
        </select>
        <button type="button" onclick="this.closest('.activity-field').remove()" class="text-red-400 hover:text-red-600 text-sm leading-none">&times;</button>
      </div>
      <div class="grid grid-cols-3 gap-1.5 items-center">
        <input type="text" placeholder="Unit" value="${activity ? escapeHtml(activity.unit) : ""}" 
          class="act-unit w-full py-1.5 px-1 bg-white rounded-lg border border-slate-200 text-xs ${showUnit ? '' : 'hidden'}">
        <input type="text" placeholder="Time Unit (min)" value="${activity && activity.timeUnit ? escapeHtml(activity.timeUnit) : ""}" 
          class="act-timeunit w-full py-1.5 px-1 bg-white rounded-lg border border-slate-200 text-xs ${showTimeUnit ? '' : 'hidden'}">
        <input type="number" inputmode="decimal" step="any" placeholder="Pts" value="${activity ? activity.pointValue : "1"}" required
          class="act-points w-full py-1.5 px-1 bg-white rounded-lg border border-slate-200 text-xs">
      </div>
    </div>
  `;
}

function addActivityToGoal(btn) {
  const container = btn.closest(".goal-field").querySelector(".activities-container");
  container.insertAdjacentHTML("beforeend", makeActivityHtml());
}

function reindexGoals() {
  // not strictly needed but keeps things tidy
}

function addChallengeActivityField(activity) {
  const container = document.getElementById("challenge-activities-container");
  const div = document.createElement("div");
  div.className = "challenge-activity-field mb-1.5 p-2 bg-slate-50 rounded-lg border border-slate-100";
  if (activity && activity.id) div.dataset.editId = activity.id;
  const actType = activity ? activity.activityType : "Occurrence";
  const showUnit = actType !== "Occurrence";
  const showTimeUnit = actType === "DistanceAndTime";
  div.innerHTML = `
    ${activity && activity.id ? `<input type="hidden" class="act-id" value="${activity.id}">` : ""}
    <div class="grid grid-cols-[1fr_auto_auto] gap-1.5 items-center mb-1.5">
      <input type="text" placeholder="Name" value="${activity ? escapeHtml(activity.name) : ""}" required
        class="act-name w-full py-1.5 px-2 bg-white rounded-lg border border-slate-200 focus:ring-2 focus:ring-indigo-300 outline-none text-xs">
      <select onchange="toggleActivityFields(this)" class="act-type py-1.5 px-1 bg-white rounded-lg border border-slate-200 text-xs">
        <option value="Occurrence" ${actType === "Occurrence" ? "selected" : ""}>Occurrence</option>
        <option value="Distance" ${actType === "Distance" ? "selected" : ""}>Distance</option>
        <option value="Time" ${actType === "Time" ? "selected" : ""}>Time</option>
        <option value="DistanceAndTime" ${actType === "DistanceAndTime" ? "selected" : ""}>Dist+Time</option>
      </select>
      <button type="button" onclick="this.closest('.challenge-activity-field').remove()" class="text-red-400 hover:text-red-600 text-sm leading-none">&times;</button>
    </div>
    <div class="grid grid-cols-3 gap-1.5 items-center">
      <input type="text" placeholder="Unit" value="${activity ? escapeHtml(activity.unit) : ""}" 
        class="act-unit w-full py-1.5 px-1 bg-white rounded-lg border border-slate-200 text-xs ${showUnit ? '' : 'hidden'}">
      <input type="text" placeholder="Time Unit (min)" value="${activity && activity.timeUnit ? escapeHtml(activity.timeUnit) : ""}" 
        class="act-timeunit w-full py-1.5 px-1 bg-white rounded-lg border border-slate-200 text-xs ${showTimeUnit ? '' : 'hidden'}">
      <input type="number" inputmode="decimal" step="any" placeholder="Pts" value="${activity ? activity.pointValue : "1"}" required
        class="act-points w-full py-1.5 px-1 bg-white rounded-lg border border-slate-200 text-xs">
    </div>
  `;
  container.appendChild(div);
}

function addPrizeField(prize, allGoals) {
  const container = document.getElementById("prizes-container");
  const div = document.createElement("div");
  div.className = "prize-field bg-slate-50 rounded-xl p-3 mb-2 relative";
  if (prize && prize.id) div.dataset.editId = prize.id;

  const goalOptions = (allGoals || [...document.querySelectorAll(".goal-field")].map((g, i) => ({
    id: "",
    index: i,
    desc: g.querySelector(".goal-desc")?.value || `Goal ${i + 1}`
  })));

  // Build goal dropdown from currently visible goals
  const goalSelectHtml = buildPrizeGoalDropdown(prize);

  div.innerHTML = `
    <button type="button" onclick="this.closest('.prize-field').remove()" class="absolute top-2 right-2 text-slate-400 hover:text-red-500 text-lg leading-none">&times;</button>
    <div class="grid grid-cols-2 gap-2 mb-2">
      <input type="text" placeholder="Prize description" value="${prize ? escapeHtml(prize.description) : ""}" required
        class="prize-desc w-full py-2 px-2 bg-white rounded-lg border border-slate-200 focus:ring-2 focus:ring-indigo-300 outline-none text-sm">
      <input type="number" inputmode="decimal" step="any" placeholder="Cost (pts, leave empty for free)" value="${prize && prize.cost != null ? prize.cost : ""}"
        class="prize-cost w-full py-2 px-2 bg-white rounded-lg border border-slate-200 focus:ring-2 focus:ring-indigo-300 outline-none text-sm">
    </div>
    <div class="flex items-center gap-3 text-sm text-slate-600">
      <label class="flex items-center gap-1">
        <input type="checkbox" class="prize-hasqr" ${(!prize || prize.hasQR) ? "checked" : ""}>
        QR
      </label>
      <label class="flex items-center gap-1 flex-1">
        <span class="text-xs whitespace-nowrap">Linked goal:</span>
        <select class="prize-goal py-1 px-1 bg-white rounded-lg border border-slate-200 text-xs flex-1">
          ${goalSelectHtml}
        </select>
      </label>
    </div>
  `;
  container.appendChild(div);
  togglePrizeCostInputs();
}

function buildPrizeGoalDropdown(prize) {
  const goalFields = document.querySelectorAll(".goal-field");
  let html = '<option value="">None</option>';
  if (goalFields.length > 0) {
    goalFields.forEach((g, i) => {
      const desc = g.querySelector(".goal-desc")?.value || `Goal ${i + 1}`;
      const editId = g.dataset.editId;
      const selected = prize && prize.challengeGoalId && editId === prize.challengeGoalId ? "selected" : "";
      html += `<option value="goal-${i}" ${selected}>${escapeHtml(desc)}</option>`;
    });
  }
  return html;
}

function refreshPrizeGoalDropdowns() {
  document.querySelectorAll(".prize-field").forEach(pf => {
    const select = pf.querySelector(".prize-goal");
    if (select) {
      const currentVal = select.value;
      select.innerHTML = buildPrizeGoalDropdown();
      if (currentVal) select.value = currentVal;
    }
  });
}

// Override addGoalField to refresh prize dropdowns
const _origAddGoalField = addGoalField;
addGoalField = function(goal) {
  _origAddGoalField(goal);
  refreshPrizeGoalDropdowns();
};

async function submitChallenge(event) {
  event.preventDefault();
  const editId = document.getElementById("challenge-edit-id").value;
  const title = document.getElementById("challenge-title").value;
  const description = document.getElementById("challenge-description").value;
  const type = document.getElementById("challenge-type").value;
  const familyId = type !== "SelfOnly" ? document.getElementById("challenge-family").value : null;
  const currencyName = document.getElementById("challenge-currency").value.trim();

  const goals = [];
  document.querySelectorAll(".goal-field").forEach(g => {
    const goalEditId = g.dataset.editId;
    const description = g.querySelector(".goal-desc").value;
    const goalType = g.querySelector(".goal-type").value;
    const targetValue = g.querySelector(".goal-target").value;
    const unit = g.querySelector(".goal-unit").value;
    const isHidden = g.querySelector(".goal-hidden").checked;
    const activities = [];
    g.querySelectorAll(".activity-field").forEach(a => {
      const actIdInput = a.querySelector(".act-id");
      const name = a.querySelector(".act-name").value;
      const activityType = a.querySelector(".act-type").value;
      const actUnit = a.querySelector(".act-unit").value;
      const actTimeUnit = a.querySelector(".act-timeunit").value;
      const pointValue = a.querySelector(".act-points").value;
      if (name && pointValue) {
        const act = { name, activityType, unit: actUnit || "times", timeUnit: actTimeUnit || null, pointValue: parseFloat(pointValue) };
        if (actIdInput && actIdInput.value) act.id = actIdInput.value;
        activities.push(act);
      }
    });
    if (description) {
      const gd = {
        id: goalEditId || undefined,
        description,
        type: goalType,
        targetValue: targetValue ? parseFloat(targetValue) : null,
        unit: unit || null,
        isHidden,
        activities: activities.length > 0 ? activities : null,
      };
      goals.push(gd);
    }
  });

  const prizes = [];
    document.querySelectorAll(".prize-field").forEach(p => {
    const description = p.querySelector(".prize-desc").value;
    const cost = p.querySelector(".prize-cost").value;
    const hasQR = p.querySelector(".prize-hasqr").checked;
    const goalIdx = p.querySelector(".prize-goal").value;
    if (description) {
      let linkedGoalId = null;
      if (goalIdx) {
        const goalIndex = parseInt(goalIdx.replace("goal-", ""));
        linkedGoalId = goals[goalIndex]?.id || null;
      }
      const pd = {
        id: (editChallengeData && editChallengeData.prizes && p.dataset.editId) ? p.dataset.editId : undefined,
        description,
        cost: cost ? parseFloat(cost) : null,
        hasQR,
        challengeGoalId: linkedGoalId,
      };
      prizes.push(pd);
    }
  });

  const challengeActivities = [];
  document.querySelectorAll("#challenge-activities-container .challenge-activity-field").forEach(a => {
    const actIdInput = a.querySelector(".act-id");
    const name = a.querySelector(".act-name").value;
    const activityType = a.querySelector(".act-type").value;
    const actUnit = a.querySelector(".act-unit").value;
    const actTimeUnit = a.querySelector(".act-timeunit").value;
    const pointValue = a.querySelector(".act-points").value;
    if (name && pointValue) {
      const act = { name, activityType, unit: actUnit || "times", timeUnit: actTimeUnit || null, pointValue: parseFloat(pointValue) };
      if (actIdInput && actIdInput.value) act.id = actIdInput.value;
      challengeActivities.push(act);
    }
  });

  const body = {
    title, description, type, familyId: familyId || null,
    goals, prizes,
    activities: challengeActivities.length > 0 ? challengeActivities : null,
  };
  if (currencyName) body.currencyName = currencyName;

  const method = editId ? "PUT" : "POST";
  const url = editId ? `/api/challenges/${editId}` : "/api/challenges";

  const res = await apiFetch(url, {
    method,
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });

  if (res.ok) {
    closeChallengeModal();
    loadAllData();
  } else {
    const data = await res.json().catch(() => ({}));
    alert(data.error || "Failed to save challenge");
  }
}

// ========== DELETE CHALLENGE ==========

function showDeleteChallengeConfirm(challengeId, challengeTitle) {
  const existing = document.getElementById("delete-modal");
  if (existing) existing.remove();

  const modal = document.createElement("div");
  modal.id = "delete-modal";
  modal.className = "fixed inset-0 bg-black/50 z-[3000] flex items-center justify-center p-4";
  modal.innerHTML = `
    <div class="bg-white rounded-3xl p-6 shadow-lg max-w-sm w-full">
      <div class="text-center mb-4">
        <span class="text-5xl block mb-3">⚠️</span>
        <h3 class="text-xl font-bold text-slate-800 mb-2">Delete Challenge?</h3>
        <p class="text-sm text-slate-600">This will permanently delete <strong>"${escapeHtml(challengeTitle)}"</strong> and all its progress, achievements, and prize claims. This cannot be undone.</p>
      </div>
      <div class="flex gap-3">
        <button onclick="this.closest('#delete-modal').remove()" class="flex-1 py-3 bg-slate-100 text-slate-700 font-bold rounded-xl hover:bg-slate-200 transition-colors">Cancel</button>
        <button onclick="deleteChallenge('${challengeId}')" class="flex-1 py-3 bg-red-500 text-white font-bold rounded-xl hover:bg-red-600 transition-colors">Delete</button>
      </div>
    </div>
  `;
  document.body.appendChild(modal);
}

async function deleteChallenge(challengeId) {
  const modal = document.getElementById("delete-modal");
  const btn = modal?.querySelector("button:last-child");
  if (btn) { btn.disabled = true; btn.textContent = "Deleting..."; }

  const res = await apiFetch(`/api/challenges/${challengeId}`, { method: "DELETE" });
  if (res.ok) {
    if (modal) modal.remove();
    showToast("Challenge Deleted", "The challenge and all its data have been removed.", "success");
    loadAllData();
  } else {
    let error = "Failed to delete challenge";
    try { const d = await res.json(); error = d.error || error; } catch {}
    if (modal) modal.remove();
    showToast("Error", error, "error");
  }
}

// ========== QUESTS TAB ==========

function renderQuestList() {
  const list = document.getElementById("challenge-list");
  if (cachedChallenges.length === 0) {
    list.innerHTML = "<p class='text-sm text-slate-500'>No challenges yet. Tap + to create one!</p>";
    return;
  }
  list.innerHTML = cachedChallenges.map((c, i) => {
    const p = cachedProgressMap[c.id];
    const completed = p ? p.progress.filter(g => g.isCompleted).length : 0;
    const total = p ? p.progress.length : c.goals.length;
    const activityCount = c.activities?.length ?? 0;
    const summary = total > 0 ? `${completed}/${total} goals` : `${activityCount} activities`;
    const emoji = EMOJIS[i % EMOJIS.length];
    const isCreator = c.createdById && currentUserId && c.createdById === currentUserId;
    const balance = p ? (p.currencyBalance || 0) : 0;
    const currencyName = p ? p.currencyName : (c.currencyName || null);
    return `
      <div class="bg-white rounded-2xl p-4 shadow-sm border border-slate-100 cursor-pointer" onclick="showProgress('${c.id}')">
        <div class="flex items-center gap-3">
          <div class="h-10 w-10 rounded-xl bg-indigo-100 flex items-center justify-center text-xl shrink-0">${emoji}</div>
          <div class="flex-1 min-w-0">
            <div class="flex items-center gap-2">
              <span class="font-bold text-slate-800 truncate">${escapeHtml(c.title)}</span>
              <span class="text-xs font-bold ${c.type === 'SelfOnly' ? 'text-orange-500 bg-orange-50' : 'text-blue-500 bg-blue-50'} px-2 py-0.5 rounded-full shrink-0">${c.type === "SelfOnly" ? "Personal" : "Family"}</span>
            </div>
            <p class="text-xs text-slate-500 mt-0.5">${summary} &middot; ${escapeHtml(c.description)}</p>
            ${currencyName ? `<p class="text-xs font-semibold text-amber-600 mt-0.5">💰 ${balance} ${escapeHtml(currencyName)}</p>` : ""}
          </div>
          <div class="flex items-center gap-1">
            ${isCreator ? `<button onclick="event.stopPropagation(); showEditChallengeForm('${c.id}')" class="text-slate-400 hover:text-indigo-600 p-1" title="Edit">✏️</button>` : ""}
            ${isCreator ? `<button onclick="event.stopPropagation(); showDeleteChallengeConfirm('${c.id}', '${escapeHtml(c.title)}')" class="text-slate-400 hover:text-red-500 p-1" title="Delete">🗑️</button>` : ""}
            <span class="text-slate-400">›</span>
          </div>
        </div>
      </div>
    `;
  }).join("");
}

async function showProgress(challengeId) {
  currentChallengeId = challengeId;
  currentMemberId = null;
  const container = document.getElementById("challenge-progress");

  const challengeRes = await apiFetch("/api/challenges/" + challengeId);
  if (!challengeRes.ok) return;
  const challenge = await challengeRes.json();

  const isFamily = challenge.type !== "SelfOnly";
  let html = `<div class="bg-white rounded-2xl p-5 shadow-sm border border-slate-100 mt-4">`;
  html += `<div class="flex items-center justify-between mb-3">
    <h3 class="text-xl font-bold text-slate-800">${escapeHtml(challenge.title)}</h3>
    <button onclick="closeProgress()" class="text-slate-500 text-sm font-bold">Close</button>
  </div>`;
  html += `<p class="text-sm text-slate-600 mb-4">${escapeHtml(challenge.description)}</p>`;

  if (isFamily) {
    await renderFamilyProgress(challenge, html, container);
  } else {
    await renderSelfProgress(challenge, html, container);
  }

  container.scrollIntoView({ behavior: "smooth" });
}

function closeProgress() {
  currentChallengeId = null;
  currentMemberId = null;
  document.getElementById("challenge-progress").innerHTML = "";
}

async function renderSelfProgress(challenge, panelHtml, container) {
  const res = await apiFetch("/api/challenges/" + challenge.id + "/progress");
  if (!res.ok) return;
  const data = await res.json();

  let html = panelHtml;

  if (data.currencyName) {
    html += `<div class="flex items-center gap-3 mb-3 p-3 bg-amber-50 rounded-xl border border-amber-200">
      <span class="font-bold text-amber-700">💰 Balance: ${data.currencyBalance} ${escapeHtml(data.currencyName)}</span>
      <span class="text-sm text-amber-600">🔥 ${data.currentStreak} day streak</span>
    </div>`;
  } else if (data.currentStreak > 0) {
    html += `<div class="flex items-center gap-3 mb-3 p-3 bg-amber-50 rounded-xl border border-amber-200">
      <span class="font-bold text-amber-600">🔥 ${data.currentStreak} day streak</span>
    </div>`;
  }

  for (const g of data.progress) {
    html += makeGoalCard(g, challenge.id);
  }

  // Challenge-level activities (not tied to a goal)
  if (challenge.activities && challenge.activities.length > 0) {
    html += `<div class="mt-3">
      <div id="challenge-activities-log">${makeChallengeActivityForms(challenge.id, challenge.activities)}</div>
    </div>`;
  }

  html += await renderPrizes(challenge);
  html += await renderAchievements(data.achievements);
  html += await renderActivityLog(challenge.id);
  html += `</div>`;
  container.innerHTML = html;
  renderGoalActions(challenge);
}

async function renderFamilyProgress(challenge, panelHtml, container) {
  const membersRes = await apiFetch("/api/challenges/" + challenge.id + "/progress/members");
  if (!membersRes.ok) return;
  const data = await membersRes.json();

  let html = panelHtml;
      html += `<div class="flex gap-2 mb-4 flex-wrap">`;
      html += data.members.map((m, i) => {
        const prefix = m.email ? m.email.split('@')[0] : "Unknown";
        return `<button class="member-tab py-2 px-4 rounded-xl text-sm font-bold transition-colors ${i === 0 ? 'bg-indigo-600 text-white' : 'bg-slate-100 text-slate-600'}" data-user-id="${m.userId}" onclick="switchMember(this)">${escapeHtml(prefix)}</button>`;
      }).join("");
  html += `</div>`;

  html += `<div id="member-progress-container">`;
  for (const m of data.members) {
    const isVisible = m === data.members[0];
      html += `<div class="member-progress" id="member-progress-${m.userId}" style="display:${isVisible ? 'block' : 'none'}">`;
      html += `<h4 class="font-bold text-indigo-600 mb-2">${escapeHtml(m.email || "Unknown")}</h4>`;
    if (m.currencyName) {
      html += `<div class="flex items-center gap-3 mb-2 p-2 bg-amber-50 rounded-lg border border-amber-200 text-sm">
        <span class="font-bold text-amber-700">💰 ${m.currencyBalance} ${escapeHtml(m.currencyName)}</span>
        <span class="text-amber-600">🔥 ${m.currentStreak} day streak</span>
      </div>`;
    } else if (m.currentStreak > 0) {
      html += `<div class="flex items-center gap-3 mb-2 p-2 bg-amber-50 rounded-lg border border-amber-200 text-sm">
        <span class="font-bold text-amber-600">🔥 ${m.currentStreak} day streak</span>
      </div>`;
    }
    for (const g of m.goals) {
      html += makeGoalCard(g, challenge.id, m.userId);
    }
    html += `</div>`;
  }
  html += `</div>`;

  // Challenge-level activities (not tied to a goal)
  if (challenge.activities && challenge.activities.length > 0) {
    html += `<div class="mt-3">
      <div id="challenge-activities-log">${makeChallengeActivityForms(challenge.id, challenge.activities)}</div>
    </div>`;
  }

  html += await renderPrizes(challenge);
  html += await renderAchievements(data.achievements);
  html += await renderActivityLog(challenge.id);
  html += `</div>`;
  container.innerHTML = html;

  const firstMember = data.members[0];
  if (firstMember) {
    currentMemberId = firstMember.userId;
    renderGoalActions(challenge);
  }
}

function makeChallengeActivityForms(challengeId, activities) {
  return activities.map(a => {
    const isDistTime = a.activityType === "DistanceAndTime";
    return `
      <form onsubmit="logActivity('${challengeId}', '${a.id}', event)" class="flex flex-col gap-2 mt-3 p-3 bg-slate-50 rounded-xl border border-slate-100">
        <span class="text-sm font-semibold text-slate-700">${escapeHtml(a.name)} <span class="text-xs font-normal text-slate-500">(${a.pointValue} pts/${escapeHtml(a.unit)})</span></span>
        ${isDistTime ? `
          <div class="grid grid-cols-2 gap-2">
            <input type="number" inputmode="decimal" step="any" placeholder="Distance (${escapeHtml(a.unit)})" required
              class="dist-input w-full py-2 px-3 bg-white rounded-lg border border-slate-200 focus:ring-2 focus:ring-indigo-300 outline-none text-sm">
            <input type="number" inputmode="decimal" step="any" placeholder="Time (${a.timeUnit ? escapeHtml(a.timeUnit) : 'min'})" required
              class="time-input w-full py-2 px-3 bg-white rounded-lg border border-slate-200 focus:ring-2 focus:ring-indigo-300 outline-none text-sm">
          </div>
        ` : `
          <input type="number" inputmode="decimal" step="any" placeholder="Amount (${escapeHtml(a.unit)})" required
            class="amount-input w-full py-2 px-3 bg-white rounded-lg border border-slate-200 focus:ring-2 focus:ring-indigo-300 outline-none text-sm">
        `}
        <div class="flex gap-2">
          <input type="text" placeholder="Notes (optional)" class="notes-input flex-1 py-2 px-3 bg-white rounded-lg border border-slate-200 focus:ring-2 focus:ring-indigo-300 outline-none text-sm">
          <button type="submit" class="py-2 px-5 bg-indigo-600 text-white font-bold rounded-lg hover:bg-indigo-700 transition-colors text-sm whitespace-nowrap shadow-sm">Log Activity</button>
        </div>
      </form>
    `;
  }).join("");
}

function renderGoalActions(challenge) {
  for (const goal of challenge.goals) {
    const actionsDiv = document.getElementById("goal-actions-" + goal.id);
    if (!actionsDiv || !goal.activities || goal.activities.length === 0) continue;
    actionsDiv.innerHTML = goal.activities.map(a => {
      const isDistTime = a.activityType === "DistanceAndTime";
      return `
        <form onsubmit="logActivity('${challenge.id}', '${a.id}', event)" class="flex flex-col gap-2 mt-3 p-3 bg-slate-50 rounded-xl border border-slate-100">
          <span class="text-sm font-semibold text-slate-700">${escapeHtml(a.name)} <span class="text-xs font-normal text-slate-500">(${a.pointValue} pts/${escapeHtml(a.unit)})</span></span>
          ${isDistTime ? `
            <div class="grid grid-cols-2 gap-2">
              <input type="number" inputmode="decimal" step="any" placeholder="Distance (${escapeHtml(a.unit)})" required
                class="dist-input w-full py-2 px-3 bg-white rounded-lg border border-slate-200 focus:ring-2 focus:ring-indigo-300 outline-none text-sm">
              <input type="number" inputmode="decimal" step="any" placeholder="Time (${a.timeUnit ? escapeHtml(a.timeUnit) : 'min'})" required
                class="time-input w-full py-2 px-3 bg-white rounded-lg border border-slate-200 focus:ring-2 focus:ring-indigo-300 outline-none text-sm">
            </div>
          ` : `
            <input type="number" inputmode="decimal" step="any" placeholder="Amount (${escapeHtml(a.unit)})" required
              class="amount-input w-full py-2 px-3 bg-white rounded-lg border border-slate-200 focus:ring-2 focus:ring-indigo-300 outline-none text-sm">
          `}
          <div class="flex gap-2">
            <input type="text" placeholder="Notes (optional)" class="notes-input flex-1 py-2 px-3 bg-white rounded-lg border border-slate-200 focus:ring-2 focus:ring-indigo-300 outline-none text-sm">
            <button type="submit" class="py-2 px-5 bg-indigo-600 text-white font-bold rounded-lg hover:bg-indigo-700 transition-colors text-sm whitespace-nowrap shadow-sm">Log Activity</button>
          </div>
        </form>
      `;
    }).join("");
  }
}

function makeGoalCard(g, challengeId, memberId) {
  const pct = g.targetValue ? Math.round((g.currentValue / g.targetValue) * 100) : 0;
  return `
    <div class="bg-slate-50 rounded-xl p-3 mb-3">
      <div class="flex items-center justify-between mb-1">
        <strong class="text-sm text-slate-800">${escapeHtml(g.goalDescription)}</strong>
        <span class="text-xs font-medium text-slate-500 bg-slate-200 px-2 py-0.5 rounded-full">${escapeHtml(g.goalType)}</span>
      </div>
      <div class="text-sm text-slate-600">
        ${g.targetValue != null
          ? `${g.currentValue} / ${g.targetValue} ${escapeHtml(g.unit || "")}`
          : `${g.currentValue} pts`}
      </div>
      ${g.isCompleted ? '<div class="text-sm font-bold text-green-600 mt-1">✅ Complete!</div>' : ""}
      ${g.targetValue ? `<div class="goal-bar mt-2"><div class="goal-bar-fill" style="width:${Math.min(pct, 100)}%"></div></div>` : ""}
      ${!g.isCompleted ? `<div class="goal-actions mt-2" id="goal-actions-${g.goalId}"><em class="text-xs text-slate-400">Loading activities...</em></div>` : ""}
    </div>
  `;
}

function switchMember(el) {
  const userId = typeof el === "string" ? el : el.dataset.userId;
  currentMemberId = userId;
  document.querySelectorAll(".member-tab").forEach(t => {
    const isActive = t.dataset.userId === userId;
    t.classList.toggle("bg-indigo-600", isActive);
    t.classList.toggle("text-white", isActive);
    t.classList.toggle("bg-slate-100", !isActive);
    t.classList.toggle("text-slate-600", !isActive);
  });
  document.querySelectorAll(".member-progress").forEach(d => d.style.display = "none");
  const mp = document.getElementById("member-progress-" + userId);
  if (mp) mp.style.display = "block";
}

async function logActivity(challengeId, activityId, event) {
  event.preventDefault();
  const form = event.target;

  const distInput = form.querySelector(".dist-input");
  const timeInput = form.querySelector(".time-input");
  const amountInput = form.querySelector(".amount-input");
  const notesInput = form.querySelector(".notes-input");

  const isDistTime = distInput && timeInput;
  let amount, timeAmount;

  if (isDistTime) {
    amount = parseFloat(distInput.value);
    timeAmount = parseFloat(timeInput.value);
    if (isNaN(amount) || isNaN(timeAmount)) return;
  } else {
    amount = parseFloat(amountInput.value);
    if (isNaN(amount)) return;
  }

  const body = { amount };
  if (timeAmount != null) body.timeAmount = timeAmount;
  if (notesInput && notesInput.value.trim()) {
    body.notes = notesInput.value.trim();
  }

  const res = await apiFetch(`/api/challenges/${challengeId}/activities/${activityId}/log`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (res.ok) {
    const data = await res.json();
    if (data.surprise) {
      showToast(data.surprise.title, data.surprise.description, "surprise");
    }
    if (data.currencyEarned) {
      showToast("Points Earned", `You earned ${data.currencyEarned} points!`, "success");
      spawnConfetti();
    }
    showProgress(challengeId);
    loadAllData();
  } else {
    const data = await res.json().catch(() => ({}));
    alert(data.error || "Failed to log activity");
  }
}

async function renderActivityLog(challengeId) {
  const res = await apiFetch(`/api/challenges/${challengeId}/activity-log?count=50`);
  if (!res.ok) return "";
  const entries = await res.json();
  if (!entries.length) return "";

  const userEmojis = {};
  let html = `<div class="mt-4 pt-4 border-t border-slate-200"><h4 class="font-bold text-slate-800 mb-2">Activity Log</h4>`;
  for (const e of entries) {
    const entryUserEmail = e.userEmail || "unknown";
    if (!userEmojis[entryUserEmail]) userEmojis[entryUserEmail] = entryUserEmail[0].toUpperCase();
    const isOwn = currentUserEmail && e.userEmail && e.userEmail === currentUserEmail;
    html += `<div class="flex items-center gap-2 py-2 text-sm" data-entry-id="${e.id}" data-challenge-id="${challengeId}" data-activity-id="${e.activityId || ""}">
      <span class="h-6 w-6 rounded-full bg-indigo-100 flex items-center justify-center text-xs font-bold text-indigo-600 shrink-0">${userEmojis[entryUserEmail]}</span>
      <span class="font-semibold text-slate-700">${escapeHtml(entryUserEmail.split('@')[0])}</span>
      <span class="text-slate-500">${escapeHtml(e.activityName)}</span>
      <span class="text-green-600 font-medium">+${e.amount} ${escapeHtml(e.unit || "")}</span>
      ${e.currencyEarned ? `<span class="text-amber-600 font-medium text-xs">+${e.currencyEarned} pts</span>` : ""}
      ${e.timeAmount ? `<span class="text-slate-400 text-xs">time: ${e.timeAmount} min</span>` : ""}
      ${e.notes ? `<span class="text-slate-400 italic text-xs">— ${escapeHtml(e.notes)}</span>` : ""}
      <span class="text-slate-400 text-xs ml-auto">${timeAgo(e.recordedAt)}</span>
      ${isOwn ? `<button onclick="editLogEntry(this)" class="text-slate-300 hover:text-indigo-500 ml-1 text-xs" title="Edit">✏️</button>` : ""}
    </div>`;
  }
  html += `</div>`;
  return html;
}

function editLogEntry(btn) {
  const row = btn.closest("[data-entry-id]");
  if (!row) return;
  const challengeId = row.dataset.challengeId;
  const entryId = row.dataset.entryId;
  const activityId = row.dataset.activityId;

  const amountSpan = row.querySelector(".text-green-600");
  const notesSpan = row.querySelector(".italic");
  const timeSpan = row.querySelector(".text-slate-400.text-xs");

  const currentAmount = amountSpan ? parseFloat(amountSpan.textContent.replace(/[+\s]/g, "").split(" ")[0]) || 0 : 0;
  const currentNotes = notesSpan ? notesSpan.textContent.replace(/^—\s*/, "").trim() : "";
  const currentTime = timeSpan && timeSpan.textContent.startsWith("time:") ? parseFloat(timeSpan.textContent.replace("time:", "")) || null : null;

  row.innerHTML = `
    <form onsubmit="saveLogEntryEdit('${challengeId}', '${activityId}', '${entryId}', event)" class="flex items-center gap-2 py-1 w-full">
      <input type="number" inputmode="decimal" step="any" value="${currentAmount}" required
        class="edit-amount w-16 py-1 px-1.5 bg-white rounded-lg border border-slate-200 focus:ring-2 focus:ring-indigo-300 outline-none text-xs">
      ${currentTime !== null ? `<input type="number" inputmode="decimal" step="any" value="${currentTime}" 
        class="edit-time w-16 py-1 px-1.5 bg-white rounded-lg border border-slate-200 focus:ring-2 focus:ring-indigo-300 outline-none text-xs" placeholder="Time">` : ""}
      <input type="text" value="${escapeHtml(currentNotes)}" placeholder="Notes"
        class="edit-notes flex-1 py-1 px-1.5 bg-white rounded-lg border border-slate-200 focus:ring-2 focus:ring-indigo-300 outline-none text-xs">
      <button type="submit" class="py-1 px-2 bg-indigo-600 text-white font-bold rounded-lg hover:bg-indigo-700 transition-colors text-xs">Save</button>
      <button type="button" onclick="cancelLogEntryEdit(this)" class="py-1 px-2 bg-slate-100 text-slate-600 font-bold rounded-lg hover:bg-slate-200 transition-colors text-xs">Cancel</button>
    </form>
  `;
}

function cancelLogEntryEdit(btn) {
  const row = btn.closest("[data-entry-id]");
  if (row) row.remove();
}

async function saveLogEntryEdit(challengeId, activityId, entryId, event) {
  event.preventDefault();
  const form = event.target;
  const amount = parseFloat(form.querySelector(".edit-amount").value);
  if (isNaN(amount)) return;

  const timeInput = form.querySelector(".edit-time");
  const notesInput = form.querySelector(".edit-notes");

  const body = { amount };
  if (timeInput && timeInput.value) body.timeAmount = parseFloat(timeInput.value);
  if (notesInput && notesInput.value.trim()) body.notes = notesInput.value.trim();

  const res = await apiFetch(`/api/challenges/${challengeId}/activities/${activityId}/log/${entryId}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });

  if (res.ok) {
    const data = await res.json();
    if (data.surprise) {
      showToast(data.surprise.title, data.surprise.description, "surprise");
    }
    if (currentChallengeId) showProgress(currentChallengeId);
    loadAllData();
  } else {
    const data = await res.json().catch(() => ({}));
    alert(data.error || "Failed to update entry");
  }
}

async function renderAchievements(achievements) {
  if (!achievements || !achievements.length) return "";
  let html = `<div class="mt-4 pt-4 border-t border-slate-200"><h4 class="font-bold text-slate-800 mb-2">Achievements</h4>`;
  for (const a of achievements) {
    html += `<div class="flex items-center gap-2 py-1 text-sm ${a.unlockedAt ? '' : 'opacity-50'}">
      ${a.unlockedAt ? "🏆" : "🔒"} ${escapeHtml(a.title)}
      ${a.unlockedAt ? `<span class="text-slate-400 text-xs">— ${new Date(a.unlockedAt).toLocaleDateString()}</span>` : ""}
    </div>`;
  }
  html += `</div>`;
  return html;
}

// ========== TREASURY TAB ==========

async function loadTreasury() {
  const container = document.getElementById("treasury-prizes");
  if (cachedChallenges.length === 0) {
    container.innerHTML = "<p class='text-sm text-slate-500'>No challenges yet, so no prizes to show!</p>";
    return;
  }
  let html = "";
  for (const c of cachedChallenges) {
    if (!c.prizes || c.prizes.length === 0) continue;
    html += `<div class="bg-white rounded-2xl p-4 shadow-sm border border-slate-100">
      <h4 class="font-bold text-slate-800 mb-2">${escapeHtml(c.title)}</h4>`;
    for (const p of c.prizes) {
      const costStr = p.cost != null ? ` (Cost: ${p.cost} ${c.currencyName ? escapeHtml(c.currencyName) : "pts"})` : "";
      html += `<div class="flex items-center justify-between py-2 border-t border-slate-100">
        <span>🏅 ${escapeHtml(p.description)}${costStr}</span>
        ${p.hasQR ? `<button onclick="generateRedemption('${c.id}', '${p.id}')" class="text-xs font-bold bg-indigo-100 text-indigo-700 px-3 py-1.5 rounded-xl hover:bg-indigo-200 transition-colors">QR</button>` : ""}
      </div>`;
    }
    html += `</div>`;
  }
  if (!html) {
    html = "<p class='text-sm text-slate-500'>No prizes found in any challenge.</p>";
  }
  container.innerHTML = html;
  loadClaims();
}

async function loadClaims() {
  const container = document.getElementById("claims-list");
  let allClaims = [];
  for (const c of cachedChallenges) {
    try {
      const res = await apiFetch(`/api/challenges/${c.id}/claims`);
      if (res.ok) {
        const claims = await res.json();
        allClaims = allClaims.concat(claims);
      }
    } catch { }
  }
  if (allClaims.length === 0) {
    container.innerHTML = "";
    return;
  }
  allClaims.sort((a, b) => new Date(b.claimedAt) - new Date(a.claimedAt));
  container.innerHTML = `<h4 class="font-bold text-slate-800 mb-3">Claim History</h4>` +
    allClaims.slice(0, 10).map(cl => `
      <div class="bg-white rounded-2xl p-3 shadow-sm border border-slate-100 flex items-center gap-3">
        <span class="text-lg">🏅</span>
        <div class="flex-1">
          <div class="font-bold text-sm text-slate-800">${escapeHtml(cl.prizeDescription)}</div>
          <div class="text-xs text-slate-500">${escapeHtml(cl.userEmail || "unknown")} &middot; ${timeAgo(cl.claimedAt)}</div>
        </div>
      </div>
    `).join("");
}

// ========== QR / REDEMPTION ==========

async function renderPrizes(challenge) {
  if (!challenge.prizes || challenge.prizes.length === 0) return "";
  let html = `<div class="mt-4 pt-4 border-t border-slate-200"><h4 class="font-bold text-slate-800 mb-2">Prizes</h4>`;
  for (const p of challenge.prizes) {
    const costStr = p.cost != null ? ` (Cost: ${p.cost} ${challenge.currencyName ? escapeHtml(challenge.currencyName) : "pts"})` : "";
    html += `<div class="flex items-center justify-between py-2 border-t border-slate-100">
      <span>🏅 ${escapeHtml(p.description)}${costStr}</span>
      ${p.hasQR ? `<button onclick="generateRedemption('${challenge.id}', '${p.id}')" class="text-xs font-bold bg-indigo-100 text-indigo-700 px-3 py-1.5 rounded-xl hover:bg-indigo-200 transition-colors">QR</button>` : ""}
    </div>`;
  }
  html += `</div>`;
  return html;
}

async function generateRedemption(challengeId, prizeId) {
  closeQrModal();

  const challengeRes = await apiFetch("/api/challenges/" + challengeId);
  if (!challengeRes.ok) return;
  const challenge = await challengeRes.json();
  const prize = challenge.prizes.find(p => p.id === prizeId);
  if (!prize) return;

  try {
    const headers = authHeaders();
    const qrRes = await fetch(API + `/api/challenges/${challengeId}/prizes/${prizeId}/qr`, { headers });
    if (!qrRes.ok) { showToast("QR Error", "Could not load QR code", "error"); return; }
    const blob = await qrRes.blob();
    const blobUrl = URL.createObjectURL(blob);
    showQrModal(prize.description, prize.cost, challenge.currencyName, blobUrl);
  } catch (e) {
    showToast("QR Error", "Could not load QR code", "error");
  }
}

function showQrModal(prizeDescription, cost, currencyName, qrUrl) {
  const modal = document.createElement("div");
  modal.id = "qr-modal";
  modal.className = "qr-modal-overlay";
  const costStr = cost != null ? `${cost} ${currencyName ? escapeHtml(currencyName) : "pts"}` : "";
  modal.innerHTML = `
    <div class="qr-modal-content">
      <div class="flex items-center justify-between mb-4">
        <h3 class="text-lg font-bold text-slate-800">${escapeHtml(prizeDescription)}</h3>
        <button onclick="closeQrModal()" class="text-slate-500 text-2xl leading-none">&times;</button>
      </div>
      <div class="qr-print-area text-center p-6 border-2 border-dashed border-indigo-400 rounded-xl bg-slate-50">
        <div class="text-xs uppercase tracking-widest text-indigo-500 mb-2">REWARD COUPON</div>
        <div class="text-xl font-bold text-slate-800 mb-1">${escapeHtml(prizeDescription)}</div>
        ${costStr ? `<div class="text-sm text-slate-500 mb-3">${costStr}</div>` : ""}
        <img src="${qrUrl}" alt="QR Code" class="mx-auto w-48 h-48 image-rendering-pixelated">
      </div>
      <div class="flex gap-3 justify-center mt-4">
        <button onclick="printQr()" class="py-3 px-6 bg-indigo-600 text-white font-bold rounded-xl hover:bg-indigo-700 transition-colors">🖨️ Print</button>
        <button onclick="closeQrModal()" class="py-3 px-6 bg-slate-100 text-slate-700 font-bold rounded-xl hover:bg-slate-200 transition-colors">Close</button>
      </div>
    </div>
  `;
  modal.dataset.qrUrl = qrUrl;
  document.body.appendChild(modal);
}

function closeQrModal() {
  const modal = document.getElementById("qr-modal");
  if (modal) {
    const qrUrl = modal.dataset.qrUrl;
    if (qrUrl && qrUrl.startsWith("blob:")) URL.revokeObjectURL(qrUrl);
    modal.remove();
  }
  const redeemModal = document.getElementById("redeem-modal");
  if (redeemModal) redeemModal.remove();
}

function printQr() {
  const printArea = document.querySelector(".qr-print-area");
  if (!printArea) return;
  const win = window.open("", "_blank");
  if (!win) { alert("Please allow popups for printing"); return; }
  win.document.write(`
    <!DOCTYPE html>
    <html>
    <head><title>Print Reward Coupon</title>
    <style>
      body { font-family: system-ui, sans-serif; display: flex; justify-content: center; align-items: center; min-height: 100vh; margin: 0; background: #fff; }
      .qr-card { text-align: center; padding: 2rem; border: 3px dashed #4f46e5; border-radius: 12px; max-width: 400px; }
      .qr-reward-label { font-size: 0.75rem; text-transform: uppercase; letter-spacing: 2px; color: #4f46e5; margin-bottom: 0.5rem; }
      .qr-prize-name { font-size: 1.5rem; font-weight: 700; margin-bottom: 0.25rem; }
      .qr-cost { font-size: 1rem; color: #6b7280; margin-bottom: 1rem; }
      .qr-image { width: 256px; height: 256px; image-rendering: pixelated; }
      @media print { body { padding: 1in; } }
    </style>
    </head>
    <body>${printArea.innerHTML}</body>
    </html>
  `);
  win.document.close();
  setTimeout(() => { win.print(); }, 500);
}

async function processRedemption(challengeId, prizeId) {
  const modal = document.createElement("div");
  modal.id = "redeem-modal";
  modal.className = "qr-modal-overlay";
  modal.innerHTML = `
    <div class="qr-modal-content">
      <div class="flex items-center justify-between mb-4">
        <h3 class="text-lg font-bold text-slate-800">Redeem Prize</h3>
        <button onclick="closeQrModal()" class="text-slate-500 text-2xl leading-none">&times;</button>
      </div>
      <div id="redeem-status" class="text-sm text-slate-500 mb-3">Looking up prize...</div>
      <div id="redeem-prize-info" class="hidden"></div>
      <form id="redeem-form" class="hidden" onsubmit="confirmRedemption('${challengeId}', '${prizeId}', event)">
        <input type="text" id="redeem-notes" placeholder="Notes (optional)" class="w-full py-3 px-3 bg-slate-100 rounded-xl border border-slate-200 focus:ring-2 focus:ring-indigo-300 outline-none mb-3">
        <div class="flex gap-3">
          <button type="submit" class="flex-1 py-3 bg-green-500 text-white font-bold rounded-xl hover:bg-green-600 transition-colors">Confirm Redemption</button>
          <button type="button" onclick="closeQrModal()" class="py-3 px-6 bg-slate-100 text-slate-700 font-bold rounded-xl hover:bg-slate-200 transition-colors">Cancel</button>
        </div>
      </form>
    </div>
  `;
  document.body.appendChild(modal);

  try {
    const res = await apiFetch("/api/challenges/" + challengeId);
    if (!res.ok) throw new Error("Challenge not found");
    const challenge = await res.json();
    const prize = challenge.prizes.find(p => p.id === prizeId);
    if (!prize) throw new Error("Prize not found");

    const costStr = prize.cost != null ? `${prize.cost} ${escapeHtml(challenge.currencyName || "pts")}` : "";
    document.getElementById("redeem-status").textContent = "";
    document.getElementById("redeem-prize-info").innerHTML = `
      <div class="text-center p-4 bg-slate-50 rounded-xl mb-3">
        <div class="font-bold text-lg text-slate-800">🏅 ${escapeHtml(prize.description)}</div>
        ${costStr ? `<div class="text-sm text-slate-500 mt-1">${costStr}</div>` : ""}
      </div>
    `;
    document.getElementById("redeem-prize-info").classList.remove("hidden");
    document.getElementById("redeem-form").classList.remove("hidden");
  } catch (err) {
    document.getElementById("redeem-status").textContent = "❌ " + err.message;
  }
}

async function confirmRedemption(challengeId, prizeId, event) {
  event.preventDefault();
  const btn = event.target.querySelector("button[type='submit']");
  btn.disabled = true;
  btn.textContent = "Redeeming...";

  const res = await apiFetch(`/api/challenges/${challengeId}/prizes/${prizeId}/redeem`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({}),
  });

  if (res.ok) {
    const data = await res.json();
    const infoDiv = document.getElementById("redeem-prize-info");
    infoDiv.innerHTML = `<div class="text-center p-4 bg-green-50 rounded-xl font-bold text-green-700">✅ Redeemed! ${escapeHtml(data.prizeDescription)} for ${data.cost} pts.</div>`;
    document.getElementById("redeem-form").classList.add("hidden");
    showToast("Prize Redeemed", data.prizeDescription, "success");
    loadAllData();
  } else {
    let errorMsg = "Redemption failed";
    try {
      const data = await res.json();
      errorMsg = data.error || errorMsg;
    } catch { /* use default */ }
    document.getElementById("redeem-status").textContent = "❌ " + errorMsg;
    btn.disabled = false;
    btn.textContent = "Confirm Redemption";
  }
}

// ========== QR SCANNER ==========

let scannerStream = null;
let scannerAnimationId = null;

async function openScanner() {
  const existing = document.getElementById("scanner-modal");
  if (existing) existing.remove();

  const modal = document.createElement("div");
  modal.id = "scanner-modal";
  modal.className = "qr-modal-overlay";
  modal.innerHTML = `
    <div class="qr-modal-content max-w-[500px]">
      <div class="flex items-center justify-between mb-4">
        <h3 class="text-lg font-bold text-slate-800">Scan QR Code</h3>
        <button onclick="closeScanner()" class="text-slate-500 text-2xl leading-none">&times;</button>
      </div>
      <div class="scanner-view">
        <video id="scanner-video" autoplay playsinline muted></video>
        <canvas id="scanner-canvas" class="hidden"></canvas>
        <div id="scanner-status" class="text-sm text-white">Position the QR code in the camera view...</div>
      </div>
    </div>
  `;
  document.body.appendChild(modal);
  document.getElementById("scanner-status").textContent = "Requesting camera...";

  try {
    scannerStream = await navigator.mediaDevices.getUserMedia({
      video: { facingMode: "environment", width: 640, height: 480 }
    });
    const video = document.getElementById("scanner-video");
    video.srcObject = scannerStream;
    await video.play();
    document.getElementById("scanner-status").textContent = "Position the QR code in the camera view...";
    scanFrame();
  } catch {
    document.getElementById("scanner-status").textContent = "❌ Camera not available. Allow camera access and try again.";
  }
}

function closeScanner() {
  if (scannerAnimationId) { cancelAnimationFrame(scannerAnimationId); scannerAnimationId = null; }
  if (scannerStream) {
    scannerStream.getTracks().forEach(t => t.stop());
    scannerStream = null;
  }
  const modal = document.getElementById("scanner-modal");
  if (modal) modal.remove();
}

function scanFrame() {
  const video = document.getElementById("scanner-video");
  if (!video || !video.videoWidth) { scannerAnimationId = requestAnimationFrame(scanFrame); return; }

  const canvas = document.getElementById("scanner-canvas");
  canvas.width = video.videoWidth;
  canvas.height = video.videoHeight;
  const ctx = canvas.getContext("2d");
  ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
  const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);

  const code = jsQR(imageData.data, imageData.width, imageData.height, { inversionAttempts: "dontInvert" });
  if (code) {
    try {
      const url = new URL(code.data);
      const claim = url.searchParams.get("claim");
      if (claim) {
        const parts = claim.split(":");
        if (parts.length === 2) {
          closeScanner();
          processRedemption(parts[0], parts[1]);
          return;
        }
      }
    } catch { }
  }

  scannerAnimationId = requestAnimationFrame(scanFrame);
}

// ========== FAMILY DETAIL ==========

async function loadFamilyDetail(familyId) {
  const res = await apiFetch("/api/families/" + familyId);
  if (!res.ok) return;
  const family = await res.json();
  const detail = document.getElementById("family-detail");
  detail.innerHTML = `
    <div class="bg-white rounded-2xl p-4 shadow-sm border border-slate-100 mt-3">
      <div class="flex items-center justify-between mb-2">
        <h3 class="font-bold text-lg text-slate-800">${escapeHtml(family.name)}</h3>
        <button onclick="this.closest('#family-detail').innerHTML = ''" class="text-sm text-slate-500 font-bold">Close</button>
      </div>
      <p class="text-sm text-slate-500">Code: <strong>${escapeHtml(family.inviteCode)}</strong></p>
      <p class="text-xs text-slate-400 mb-3">Created: ${new Date(family.createdAt).toLocaleDateString()}</p>
      <h4 class="font-bold text-sm text-slate-700 mb-2">Members</h4>
      ${family.members.map(m => `
        <div class="flex items-center gap-2 py-1.5 border-t border-slate-100 text-sm">
          <span class="h-6 w-6 rounded-full bg-indigo-100 flex items-center justify-center text-xs font-bold text-indigo-600">${m.email[0].toUpperCase()}</span>
          <span>${escapeHtml(m.email)}</span>
          <span class="text-xs text-slate-400 ml-auto">${escapeHtml(m.role)}</span>
        </div>
      `).join("")}
    </div>
  `;
  detail.scrollIntoView({ behavior: "smooth" });
}
