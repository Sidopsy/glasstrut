const API = API_BASE;
const EMOJIS = ["🛏️", "📚", "🧹", "🏃", "🎨", "🎵", "🌱", "🍳", "🧩", "📝"];

if ("serviceWorker" in navigator) {
  navigator.serviceWorker.register("/sw.js");
}

let currentChallengeId = null;
let currentMemberId = null;
let isRegisterMode = false;
let cachedChallenges = [];
let cachedProgressMap = {};
let cachedFamilies = [];

function authHeaders() {
  const token = localStorage.getItem("token");
  return token ? { "Authorization": "Bearer " + token } : {};
}

async function apiFetch(path, options = {}) {
  const res = await fetch(API + path, {
    ...options,
    headers: { ...authHeaders(), "Content-Type": "application/x-www-form-urlencoded", ...options.headers },
  });
  if (res.status === 401) {
    localStorage.removeItem("token");
    window.location.reload();
  }
  return res;
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

  const name = email ? email.split("@")[0] : "User";
  document.getElementById("user-name").textContent = name.charAt(0).toUpperCase() + name.slice(1);
  document.getElementById("profile-name").textContent = name.charAt(0).toUpperCase() + name.slice(1);
  document.getElementById("profile-email").textContent = email || "";
  document.getElementById("greeting-day").textContent = getDayGreeting();

  const initial = email ? email[0].toUpperCase() : "?";
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
  currentChallengeId = null;
  currentMemberId = null;
  isRegisterMode = false;
  cachedChallenges = [];
  cachedProgressMap = {};
  cachedFamilies = [];
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
  if (isRegisterMode) {
    title.textContent = "Create Account";
    btn.textContent = "REGISTER";
    toggle.innerHTML = '<span class="text-slate-500">Already have an account?</span> <button type="button" onclick="toggleAuthForm()" class="font-bold text-indigo-600 ml-1">LOG IN</button>';
  } else {
    title.textContent = "Log In";
    btn.textContent = "LOG IN";
    toggle.innerHTML = '<span class="text-slate-500">No account yet?</span> <button type="button" onclick="toggleAuthForm()" class="font-bold text-indigo-600 ml-1">CREATE AN ACCOUNT</button>';
  }
  clearAuthError();
}

async function authFetch(endpoint, email, password) {
  const formData = new URLSearchParams({ email, password });
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
      const payload = JSON.parse(atob(token.split(".")[1]));
      const email = payload["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"];
      if (email) showDashboard(email);
      else showAuth();
    } catch {
      localStorage.removeItem("token");
      showAuth();
    }
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
      const data = await res.json();
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
      const data = await res.json();
      alert(data.error || "Failed to join family");
    }
  });

  document.getElementById("challenge-type").addEventListener("change", () => {
    const type = document.getElementById("challenge-type").value;
    document.getElementById("family-select-group").classList.toggle("hidden", type === "SelfOnly");
  });

  document.getElementById("create-challenge-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    const title = document.getElementById("challenge-title").value;
    const description = document.getElementById("challenge-description").value;
    const type = document.getElementById("challenge-type").value;
    const familyId = type !== "SelfOnly" ? document.getElementById("challenge-family").value : null;
    if (type !== "SelfOnly" && !familyId) { alert("Please select a family."); return; }
    const goals = document.getElementById("challenge-goals").value.split(",").map(s => s.trim()).filter(Boolean);
    const prizes = document.getElementById("challenge-prizes").value.split(",").map(s => s.trim()).filter(Boolean);
    const currencyName = document.getElementById("challenge-currency").value.trim();

    const body = {
      title, description, type, familyId,
      goals: goals.map(g => ({
        description: g,
        type: "Achievement",
        activities: [{ name: g, unit: "times", pointValue: 1 }]
      })),
      prizes: prizes.map(p => ({ description: p })),
    };
    if (currencyName) body.currencyName = currencyName;
    const res = await fetch(API + "/api/challenges", {
      method: "POST",
      headers: { ...authHeaders(), "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    if (res.ok) {
      closeCreateChallengeForm();
      loadAllData();
    } else {
      const data = await res.json();
      alert(data.error || "Failed to create challenge");
    }
  });
});

// ========== DATA LOADING ==========

async function loadAllData() {
  await Promise.all([loadChallenges(), loadAchievements()]);
  renderChronicleFeed();
}

async function loadChallenges() {
  const res = await fetch(API + "/api/challenges", { headers: { ...authHeaders() } });
  if (!res.ok) return;
  cachedChallenges = await res.json();

  const progressPromises = cachedChallenges.map(async c => {
    try {
      const r = await fetch(API + `/api/challenges/${c.id}/progress`, { headers: { ...authHeaders() } });
      if (!r.ok) return null;
      return r.json();
    } catch { return null; }
  });
  const progressData = await Promise.all(progressPromises);
  cachedProgressMap = {};
  cachedChallenges.forEach((c, i) => { cachedProgressMap[c.id] = progressData[i]; });

  renderUpNext();
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
  const res = await fetch(API + "/api/achievements", { headers: { ...authHeaders() } });
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
    const emoji = EMOJIS[i % EMOJIS.length];
    const badgeColor = c.type === "SelfOnly" ? "text-orange-500 bg-orange-50" : "text-blue-500 bg-blue-50";
    return `
      <div class="snap-start shrink-0 w-64 bg-white rounded-2xl p-4 shadow-sm border border-slate-100 flex flex-col justify-between cursor-pointer" onclick="showProgress('${c.id}')">
        <div>
          <div class="flex items-start justify-between mb-3">
            <div class="h-10 w-10 rounded-xl bg-indigo-100 flex items-center justify-center text-xl">${emoji}</div>
            <span class="font-bold ${badgeColor} px-2 py-1 rounded-lg text-sm">${c.type === "SelfOnly" ? "Personal" : "Family"}</span>
          </div>
          <h3 class="font-bold text-lg text-slate-700 leading-tight">${escapeHtml(c.title)}</h3>
          <p class="text-xs text-slate-500 mt-1">${completed}/${total} goals done</p>
        </div>
        ${c.currencyName && total > 0 ? `<div class="mt-3 text-sm font-semibold text-indigo-600">${completed}/${total} &middot; ${escapeHtml(c.currencyName)}</div>` : ""}
      </div>
    `;
  }).join("");
}

async function renderChronicleFeed() {
  const container = document.getElementById("chronicle-feed");
  const allEntries = [];
  for (const c of cachedChallenges) {
    try {
      const res = await fetch(API + `/api/challenges/${c.id}/activity-log?count=5`, {
        headers: { ...authHeaders() },
      });
      if (res.ok) {
        const entries = await res.json();
        allEntries.push(...entries);
      }
    } catch { }
  }
  if (allEntries.length === 0) {
    container.innerHTML = "<p class='text-sm text-slate-500'>No activity yet. Log some progress in Quests!</p>";
    return;
  }
  allEntries.sort((a, b) => new Date(b.recordedAt) - new Date(a.recordedAt));
  const top = allEntries.slice(0, 10);
  const userIcons = {};
  container.innerHTML = top.map(e => {
    if (!userIcons[e.userEmail]) userIcons[e.userEmail] = e.userEmail[0].toUpperCase();
    return `
      <div class="bg-white rounded-2xl p-4 shadow-sm border border-slate-100 flex items-center gap-4">
        <div class="h-12 w-12 rounded-full bg-indigo-100 flex items-center justify-center text-xl shrink-0 font-bold text-indigo-600">${userIcons[e.userEmail]}</div>
        <div class="flex-1 min-w-0">
          <p class="text-sm text-slate-600"><span class="font-bold text-slate-800">${escapeHtml(e.userEmail.split('@')[0])}</span> logged</p>
          <p class="font-bold text-slate-800 truncate">${escapeHtml(e.activityName)}</p>
        </div>
        <div class="text-right shrink-0">
          <span class="font-bold text-green-500 bg-green-50 px-2 py-1 rounded-lg text-sm">+${e.amount} ${escapeHtml(e.unit || "")}</span>
          <p class="text-xs text-slate-400 mt-1">${timeAgo(e.recordedAt)}</p>
        </div>
      </div>
    `;
  }).join("");
}

async function refreshPoints() {
  let total = 0;
  for (const c of cachedChallenges) {
    const p = cachedProgressMap[c.id];
    if (p) {
      for (const g of p.progress) {
        if (g.goalType === "Currency") total += g.currentValue;
      }
    }
  }
  document.getElementById("user-points").textContent = total + " 🍦";
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
    const emoji = EMOJIS[i % EMOJIS.length];
    return `
      <div class="bg-white rounded-2xl p-4 shadow-sm border border-slate-100 cursor-pointer" onclick="showProgress('${c.id}')">
        <div class="flex items-center gap-3">
          <div class="h-10 w-10 rounded-xl bg-indigo-100 flex items-center justify-center text-xl shrink-0">${emoji}</div>
          <div class="flex-1 min-w-0">
            <div class="flex items-center gap-2">
              <span class="font-bold text-slate-800 truncate">${escapeHtml(c.title)}</span>
              <span class="text-xs font-bold ${c.type === 'SelfOnly' ? 'text-orange-500 bg-orange-50' : 'text-blue-500 bg-blue-50'} px-2 py-0.5 rounded-full shrink-0">${c.type === "SelfOnly" ? "Personal" : "Family"}</span>
            </div>
            <p class="text-xs text-slate-500 mt-0.5">${completed}/${total} goals &middot; ${escapeHtml(c.description)}</p>
          </div>
          <span class="text-slate-400">›</span>
        </div>
      </div>
    `;
  }).join("");
}

function showCreateChallengeForm() {
  // Populate family selector
  const familySelect = document.getElementById("challenge-family");
  familySelect.innerHTML = '<option value="">Select a family...</option>' +
    cachedFamilies.map(f => `<option value="${f.id}">${escapeHtml(f.name)}</option>`).join("");
  document.getElementById("create-challenge-modal").classList.remove("hidden");
}

function closeCreateChallengeForm() {
  document.getElementById("create-challenge-modal").classList.add("hidden");
}

async function showProgress(challengeId) {
  currentChallengeId = challengeId;
  currentMemberId = null;
  const container = document.getElementById("challenge-progress");

  const challengeRes = await fetch(API + "/api/challenges/" + challengeId, {
    headers: { ...authHeaders() },
  });
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
  const res = await fetch(API + "/api/challenges/" + challenge.id + "/progress", {
    headers: { ...authHeaders() },
  });
  if (!res.ok) return;
  const data = await res.json();

  let html = panelHtml;
  for (const g of data.progress) {
    html += makeGoalCard(g, challenge.id);
  }
  html += await renderPrizes(challenge);
  html += await renderAchievements(data.achievements);
  html += await renderActivityLog(challenge.id);
  html += `</div>`;
  container.innerHTML = html;

  for (const goal of challenge.goals) {
    const actionsDiv = document.getElementById("goal-actions-" + goal.id);
    if (!actionsDiv || !goal.activities || goal.activities.length === 0) continue;
    actionsDiv.innerHTML = goal.activities.map(a => `
      <form onsubmit="logActivity('${challenge.id}', '${a.id}', event)" class="flex items-center gap-2 mt-2 flex-wrap">
        <span class="text-sm font-medium text-slate-600">${escapeHtml(a.name)} (${a.pointValue}/${escapeHtml(a.unit)}):</span>
        <input type="number" step="any" placeholder="${escapeHtml(a.unit)}" required
          class="flex-1 min-w-[80px] py-2 px-3 bg-slate-100 rounded-xl border border-slate-200 focus:ring-2 focus:ring-indigo-300 outline-none text-sm">
        <input type="text" placeholder="Notes" class="notes-input py-2 px-3 bg-slate-100 rounded-xl border border-slate-200 focus:ring-2 focus:ring-indigo-300 outline-none text-sm flex-1 min-w-[100px]">
        <button type="submit" class="py-2 px-4 bg-indigo-600 text-white font-bold rounded-xl hover:bg-indigo-700 transition-colors text-sm">Log</button>
      </form>
    `).join("");
  }
}

async function renderFamilyProgress(challenge, panelHtml, container) {
  const membersRes = await fetch(API + "/api/challenges/" + challenge.id + "/progress/members", {
    headers: { ...authHeaders() },
  });
  if (!membersRes.ok) return;
  const data = await membersRes.json();

  let html = panelHtml;
  html += `<div class="flex gap-2 mb-4 flex-wrap">`;
  html += data.members.map((m, i) =>
    `<button class="member-tab py-2 px-4 rounded-xl text-sm font-bold transition-colors ${i === 0 ? 'bg-indigo-600 text-white' : 'bg-slate-100 text-slate-600'}" onclick="switchMember('${m.userId}')">${escapeHtml(m.email.split('@')[0])}</button>`
  ).join("");
  html += `</div>`;

  html += `<div id="member-progress-container">`;
  for (const m of data.members) {
    const isVisible = m === data.members[0];
    html += `<div class="member-progress" id="member-progress-${m.userId}" style="display:${isVisible ? 'block' : 'none'}">`;
    html += `<h4 class="font-bold text-indigo-600 mb-2">${escapeHtml(m.email)}</h4>`;
    for (const g of m.goals) {
      html += makeGoalCard(g, challenge.id, m.userId);
    }
    html += `</div>`;
  }
  html += `</div>`;

  html += await renderPrizes(challenge);
  html += await renderAchievements(data.achievements);
  html += await renderActivityLog(challenge.id);
  html += `</div>`;
  container.innerHTML = html;

  const firstMember = data.members[0];
  if (firstMember) {
    currentMemberId = firstMember.userId;
    for (const goal of challenge.goals) {
      const actionsDiv = document.getElementById("goal-actions-" + goal.id);
      if (!actionsDiv || !goal.activities || goal.activities.length === 0) continue;
      actionsDiv.innerHTML = goal.activities.map(a => `
        <form onsubmit="logActivity('${challenge.id}', '${a.id}', event)" class="flex items-center gap-2 mt-2 flex-wrap">
          <span class="text-sm font-medium text-slate-600">${escapeHtml(a.name)} (${a.pointValue}/${escapeHtml(a.unit)}):</span>
          <input type="number" step="any" placeholder="${escapeHtml(a.unit)}" required
            class="flex-1 min-w-[80px] py-2 px-3 bg-slate-100 rounded-xl border border-slate-200 focus:ring-2 focus:ring-indigo-300 outline-none text-sm">
          <input type="text" placeholder="Notes" class="notes-input py-2 px-3 bg-slate-100 rounded-xl border border-slate-200 focus:ring-2 focus:ring-indigo-300 outline-none text-sm flex-1 min-w-[100px]">
          <button type="submit" class="py-2 px-4 bg-indigo-600 text-white font-bold rounded-xl hover:bg-indigo-700 transition-colors text-sm">Log</button>
        </form>
      `).join("");
    }
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
          ? `${g.currentValue} / ${g.targetValue} ${g.unit || ""}`
          : `${g.currentValue} pts`}
      </div>
      ${g.isCompleted ? '<div class="text-sm font-bold text-green-600 mt-1">✅ Complete!</div>' : ""}
      ${g.targetValue ? `<div class="goal-bar mt-2"><div class="goal-bar-fill" style="width:${Math.min(pct, 100)}%"></div></div>` : ""}
      ${!g.isCompleted ? `<div class="goal-actions mt-2" id="goal-actions-${g.goalId}"><em class="text-xs text-slate-400">Loading activities...</em></div>` : ""}
    </div>
  `;
}

function switchMember(userId) {
  currentMemberId = userId;
  document.querySelectorAll(".member-tab").forEach(t => {
    t.classList.toggle("bg-indigo-600", t.textContent.trim() === [...document.querySelectorAll(".member-tab")].find(b => b.onclick.toString().includes(userId))?.textContent.trim());
    t.classList.toggle("text-white", t.textContent.trim() === [...document.querySelectorAll(".member-tab")].find(b => b.onclick.toString().includes(userId))?.textContent.trim());
    t.classList.toggle("bg-slate-100", t.textContent.trim() !== [...document.querySelectorAll(".member-tab")].find(b => b.onclick.toString().includes(userId))?.textContent.trim());
    t.classList.toggle("text-slate-600", t.textContent.trim() !== [...document.querySelectorAll(".member-tab")].find(b => b.onclick.toString().includes(userId))?.textContent.trim());
  });
  document.querySelectorAll(".member-progress").forEach(d => d.style.display = "none");
  const mp = document.getElementById("member-progress-" + userId);
  if (mp) mp.style.display = "block";
}

async function logActivity(challengeId, activityId, event) {
  event.preventDefault();
  const form = event.target;
  const amountInput = form.querySelector("input[type='number']");
  const notesInput = form.querySelector("input.notes-input");
  const amount = parseFloat(amountInput.value);
  if (isNaN(amount)) return;

  const body = { amount };
  if (notesInput && notesInput.value.trim()) {
    body.notes = notesInput.value.trim();
  }

  const res = await fetch(API + `/api/challenges/${challengeId}/activities/${activityId}/log`, {
    method: "POST",
    headers: { ...authHeaders(), "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (res.ok) {
    showProgress(challengeId);
    loadAllData();
  } else {
    const data = await res.json();
    alert(data.error || "Failed to log activity");
  }
}

async function renderActivityLog(challengeId) {
  const res = await fetch(API + `/api/challenges/${challengeId}/activity-log?count=5`, {
    headers: { ...authHeaders() },
  });
  if (!res.ok) return "";
  const entries = await res.json();
  if (!entries.length) return "";

  const userEmojis = {};
  let html = `<div class="mt-4 pt-4 border-t border-slate-200"><h4 class="font-bold text-slate-800 mb-2">Recent Activity</h4>`;
  for (const e of entries) {
    if (!userEmojis[e.userEmail]) userEmojis[e.userEmail] = e.userEmail[0].toUpperCase();
    html += `<div class="flex items-center gap-2 py-2 text-sm">
      <span class="h-6 w-6 rounded-full bg-indigo-100 flex items-center justify-center text-xs font-bold text-indigo-600 shrink-0">${userEmojis[e.userEmail]}</span>
      <span class="font-semibold text-slate-700">${escapeHtml(e.userEmail.split('@')[0])}</span>
      <span class="text-slate-500">${escapeHtml(e.activityName)}</span>
      <span class="text-green-600 font-medium">+${e.amount} ${escapeHtml(e.unit || "")}</span>
      ${e.notes ? `<span class="text-slate-400 italic text-xs">— ${escapeHtml(e.notes)}</span>` : ""}
      <span class="text-slate-400 text-xs ml-auto">${timeAgo(e.recordedAt)}</span>
    </div>`;
  }
  html += `</div>`;
  return html;
}

async function renderAchievements(achievements) {
  if (!achievements.length) return "";
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
        <button onclick="generateRedemption('${c.id}', '${p.id}')" class="text-xs font-bold bg-indigo-100 text-indigo-700 px-3 py-1.5 rounded-xl hover:bg-indigo-200 transition-colors">QR</button>
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
      const res = await fetch(API + `/api/challenges/${c.id}/claims`, { headers: { ...authHeaders() } });
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
      <button onclick="generateRedemption('${challenge.id}', '${p.id}')" class="text-xs font-bold bg-indigo-100 text-indigo-700 px-3 py-1.5 rounded-xl hover:bg-indigo-200 transition-colors">QR</button>
    </div>`;
  }
  html += `</div>`;
  return html;
}

async function generateRedemption(challengeId, prizeId) {
  const existing = document.getElementById("qr-modal");
  if (existing) existing.remove();

  const challengeRes = await fetch(API + "/api/challenges/" + challengeId, {
    headers: { ...authHeaders() },
  });
  if (!challengeRes.ok) return;
  const challenge = await challengeRes.json();
  const prize = challenge.prizes.find(p => p.id === prizeId);
  if (!prize) return;

  const qrUrl = `${API}/api/challenges/${challengeId}/prizes/${prizeId}/qr`;
  showQrModal(prize.description, prize.cost, challenge.currencyName, qrUrl);
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
  document.body.appendChild(modal);
}

function closeQrModal() {
  const modal = document.getElementById("qr-modal");
  if (modal) modal.remove();
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
    const res = await fetch(API + "/api/challenges/" + challengeId, {
      headers: { ...authHeaders() },
    });
    if (!res.ok) throw new Error("Challenge not found");
    const challenge = await res.json();
    const prize = challenge.prizes.find(p => p.id === prizeId);
    if (!prize) throw new Error("Prize not found");

    const costStr = prize.cost != null ? `${prize.cost} ${challenge.currencyName || "pts"}` : "";
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

  const res = await fetch(API + `/api/challenges/${challengeId}/prizes/${prizeId}/redeem`, {
    method: "POST",
    headers: { ...authHeaders(), "Content-Type": "application/json" },
    body: JSON.stringify({}),
  });

  if (res.ok) {
    const data = await res.json();
    const infoDiv = document.getElementById("redeem-prize-info");
    infoDiv.innerHTML = `<div class="text-center p-4 bg-green-50 rounded-xl font-bold text-green-700">✅ Redeemed! ${escapeHtml(data.prizeDescription)} for ${data.cost} pts.</div>`;
    document.getElementById("redeem-form").classList.add("hidden");
    loadAllData();
  } else {
    const data = await res.json();
    document.getElementById("redeem-status").textContent = "❌ " + (data.error || "Redemption failed");
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
