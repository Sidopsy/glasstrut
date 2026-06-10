const API = API_BASE;

if ("serviceWorker" in navigator) {
  navigator.serviceWorker.register("/sw.js");
}

let currentChallengeId = null;
let currentMemberId = null;

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

function showDashboard(email) {
  document.getElementById("auth-section").style.display = "none";
  document.getElementById("dashboard").style.display = "block";
  document.getElementById("user-email").textContent = email;
  loadFamilies();
  loadChallenges();
  loadAchievements();

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

function showAuth() {
  document.getElementById("auth-section").style.display = "block";
  document.getElementById("dashboard").style.display = "none";
}

function logout() {
  closeScanner();
  localStorage.removeItem("token");
  currentChallengeId = null;
  currentMemberId = null;
  document.getElementById("family-list").innerHTML = "";
  document.getElementById("challenge-list").innerHTML = "";
  document.getElementById("achievement-list").innerHTML = "<p>No achievements yet.</p>";
  document.getElementById("achievement-count").textContent = "0";
  document.getElementById("dashboard-stats").innerHTML = "";
  document.getElementById("challenge-progress").innerHTML = "";
  document.getElementById("family-detail").innerHTML = "";
  showAuth();
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
    try {
      const data = await authFetch("/api/auth/login",
        document.getElementById("login-email").value,
        document.getElementById("login-password").value);
      localStorage.setItem("token", data.token);
      showDashboard(data.email);
      errorDiv.textContent = "";
    } catch (err) {
      errorDiv.textContent = err.message;
    }
  });

  document.getElementById("register-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    const errorDiv = document.getElementById("auth-error");
    try {
      const data = await authFetch("/api/auth/register",
        document.getElementById("register-email").value,
        document.getElementById("register-password").value);
      localStorage.setItem("token", data.token);
      showDashboard(data.email);
      errorDiv.textContent = "";
    } catch (err) {
      errorDiv.textContent = err.message;
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
    document.getElementById("family-select-group").style.display = type === "SelfOnly" ? "none" : "block";
    document.getElementById("challenge-family").required = type !== "SelfOnly";
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
      document.getElementById("challenge-title").value = "";
      document.getElementById("challenge-description").value = "";
      document.getElementById("challenge-goals").value = "";
      document.getElementById("challenge-prizes").value = "";
      document.getElementById("challenge-currency").value = "";
      loadChallenges();
    } else {
      const data = await res.json();
      alert(data.error || "Failed to create challenge");
    }
  });
});

async function loadChallenges() {
  const res = await fetch(API + "/api/challenges", {
    headers: { ...authHeaders() },
  });
  if (!res.ok) return;
  const challenges = await res.json();
  const list = document.getElementById("challenge-list");
  if (challenges.length === 0) {
    list.innerHTML = "<p>No challenges yet.</p>";
    updateDashboardStats([]);
    return;
  }

  // Get progress for each to show summary
  const progressPromises = challenges.map(async c => {
    try {
      const r = await fetch(API + `/api/challenges/${c.id}/progress`, { headers: { ...authHeaders() } });
      if (!r.ok) return null;
      return r.json();
    } catch { return null; }
  });
  const progressData = await Promise.all(progressPromises);

  list.innerHTML = challenges.map((c, i) => {
    const p = progressData[i];
    const completed = p ? p.progress.filter(g => g.isCompleted).length : 0;
    const total = p ? p.progress.length : c.goals.length;
    const hasPrizes = c.prizes && c.prizes.length;

    return `
    <div class="challenge-card" onclick="showProgress('${c.id}')">
      <div class="challenge-header">
        <strong>${escapeHtml(c.title)}</strong>
        <span class="badge ${c.type === 'SelfOnly' ? 'badge-self' : 'badge-family'}">${escapeHtml(c.type)}</span>
        ${c.currencyName ? `<span class="badge badge-currency">${escapeHtml(c.currencyName)}</span>` : ""}
      </div>
      <p>${escapeHtml(c.description)}</p>
      ${p ? `<div class="challenge-summary">${completed}/${total} goals completed</div>` : ""}
      ${c.goals.length ? `<p class="goals-preview">${c.goals.map(g => {
        let s = escapeHtml(g.description);
        if (g.type === "Achievement" && g.targetValue != null) s += " (" + g.targetValue + " " + escapeHtml(g.unit || "") + ")";
        if (g.activities && g.activities.length) {
          s += " → " + g.activities.map(a => escapeHtml(a.name) + " (" + a.pointValue + "/" + escapeHtml(a.unit) + ")").join(", ");
        }
        return s;
      }).join("; ")}</p>` : ""}
      ${hasPrizes ? `<p class="prizes-preview">🏅 ${c.prizes.map(p => {
        let s = escapeHtml(p.description);
        if (p.cost != null) s += " (cost: " + p.cost + ")";
        return s;
      }).join(", ")}</p>` : ""}
      <small class="click-hint">Click to view details & log progress</small>
    </div>
  `}).join("");

  updateDashboardStats(challenges, progressData);
}

function updateDashboardStats(challenges, progressData) {
  const total = challenges.length;
  let completedGoals = 0;
  let totalGoals = 0;
  let achievements = 0;

  if (progressData) {
    for (const p of progressData) {
      if (!p) continue;
      totalGoals += p.progress.length;
      completedGoals += p.progress.filter(g => g.isCompleted).length;
    }
  }

  const statsDiv = document.getElementById("dashboard-stats");
  if (!statsDiv) return;
  statsDiv.innerHTML = `
    <div class="stat-card"><strong>${total}</strong> Active challenges</div>
    <div class="stat-card"><strong>${completedGoals}/${totalGoals}</strong> Goals completed</div>
  `;
}

async function loadFamilies() {
  const res = await apiFetch("/api/families");
  if (!res.ok) return;
  const families = await res.json();
  const list = document.getElementById("family-list");
  if (families.length === 0) {
    list.innerHTML = "<p>No families yet. Create or join one above.</p>";
    return;
  }
  list.innerHTML = families.map(f => `
    <div class="family-card" onclick="loadFamilyDetail('${f.id}')">
      <strong>${escapeHtml(f.name)}</strong>
      <br>
      <small>Code: ${escapeHtml(f.inviteCode)} &middot; ${f.members.length} members</small>
    </div>
  `).join("");

  // Populate family selector for challenge creation
  const familySelect = document.getElementById("challenge-family");
  if (familySelect) {
    const currentValue = familySelect.value;
    familySelect.innerHTML = '<option value="">Select a family...</option>' +
      families.map(f => `<option value="${f.id}">${escapeHtml(f.name)}</option>`).join("");
    if (currentValue) familySelect.value = currentValue;
  }
}

async function loadFamilyDetail(familyId) {
  const res = await apiFetch("/api/families/" + familyId);
  if (!res.ok) return;
  const family = await res.json();
  const detail = document.getElementById("family-detail");
  detail.innerHTML = `
    <div class="family-detail">
      <h3>${escapeHtml(family.name)}</h3>
      <p>Invite code: <strong>${escapeHtml(family.inviteCode)}</strong></p>
      <p>Created: ${new Date(family.createdAt).toLocaleDateString()}</p>
      <h4>Members</h4>
      <table>
        <thead>
          <tr><th>Email</th><th>Role</th></tr>
        </thead>
        <tbody>
          ${family.members.map(m => `
            <tr>
              <td>${escapeHtml(m.email)}</td>
              <td>${escapeHtml(m.role)}</td>
            </tr>
          `).join("")}
        </tbody>
      </table>
      <button onclick="document.getElementById('family-detail').innerHTML = ''">Close</button>
    </div>
  `;
  detail.scrollIntoView({ behavior: "smooth" });
}

function escapeHtml(str) {
  const div = document.createElement("div");
  div.textContent = str;
  return div.innerHTML;
}

async function showProgress(challengeId) {
  currentChallengeId = challengeId;
  currentMemberId = null;
  const container = document.getElementById("challenge-progress");
  container.innerHTML = `<div class="progress-panel"><h3>Loading...</h3></div>`;
  container.scrollIntoView({ behavior: "smooth" });

  // Get challenge details
  const challengeRes = await fetch(API + "/api/challenges/" + challengeId, {
    headers: { ...authHeaders() },
  });
  if (!challengeRes.ok) return;
  const challenge = await challengeRes.json();

  const isFamily = challenge.type !== "SelfOnly";
  let html = `<div class="progress-panel">`;
  html += `<h3>${escapeHtml(challenge.title)}</h3>`;
  html += `<button onclick="closeProgress()">Close</button>`;
  html += `<p>${escapeHtml(challenge.description)}</p>`;

  if (isFamily) {
    await renderFamilyProgress(challenge, html, container);
  } else {
    await renderSelfProgress(challenge, html, container);
  }
}

async function renderSelfProgress(challenge, panelHtml, container) {
  const res = await fetch(API + "/api/challenges/" + challenge.id + "/progress", {
    headers: { ...authHeaders() },
  });
  if (!res.ok) return;
  const data = await res.json();

  let html = panelHtml;
  for (const g of data.progress) {
    const pct = g.targetValue ? Math.round((g.currentValue / g.targetValue) * 100) : 0;
    html += makeGoalCard(g, pct, challenge.id);
  }

  html += await renderPrizes(challenge);
  html += await renderAchievements(data.achievements);
  html += await renderActivityLog(challenge.id);
  html += `</div>`;
  container.innerHTML = html;

  // Load activity forms
  for (const goal of challenge.goals) {
    const actionsDiv = document.getElementById("goal-actions-" + goal.id);
    if (!actionsDiv || !goal.activities || goal.activities.length === 0) continue;
    actionsDiv.innerHTML = goal.activities.map(a => `
      <form onsubmit="logActivity('${challenge.id}', '${a.id}', event)">
        <label>${escapeHtml(a.name)} (${a.pointValue}/${escapeHtml(a.unit)}):</label>
        <input type="number" step="any" placeholder="${escapeHtml(a.unit)}" required>
        <input type="text" placeholder="Notes (optional)" class="notes-input">
        <button type="submit">Log</button>
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
  html += `<div class="member-tabs">`;
  html += data.members.map((m, i) =>
    `<button class="member-tab ${i === 0 ? 'active' : ''}" onclick="switchMember('${m.userId}')">${escapeHtml(m.email.split('@')[0])}</button>`
  ).join("");
  html += `</div>`;

  html += `<div id="member-progress-container">`;
  for (const m of data.members) {
    const isVisible = m === data.members[0];
    html += `<div class="member-progress" id="member-progress-${m.userId}" style="display:${isVisible ? 'block' : 'none'}">`;
    html += `<h4>${escapeHtml(m.email)}</h4>`;
    for (const g of m.goals) {
      const pct = g.targetValue ? Math.round((g.currentValue / g.targetValue) * 100) : 0;
      html += makeGoalCard(g, pct, challenge.id, m.userId);
    }
    html += `</div>`;
  }
  html += `</div>`;

  html += await renderPrizes(challenge);
  html += await renderAchievements(data.achievements);
  html += await renderActivityLog(challenge.id);
  html += `</div>`;
  container.innerHTML = html;

  // Load activity forms for the visible member
  const firstMember = data.members[0];
  if (firstMember) {
    currentMemberId = firstMember.userId;
    for (const goal of challenge.goals) {
      const actionsDiv = document.getElementById("goal-actions-" + goal.id);
      if (!actionsDiv || !goal.activities || goal.activities.length === 0) continue;
      actionsDiv.innerHTML = goal.activities.map(a => `
        <form onsubmit="logActivity('${challenge.id}', '${a.id}', event)">
          <label>${escapeHtml(a.name)} (${a.pointValue}/${escapeHtml(a.unit)}):</label>
          <input type="number" step="any" placeholder="${escapeHtml(a.unit)}" required>
          <input type="text" placeholder="Notes (optional)" class="notes-input">
          <button type="submit">Log</button>
        </form>
      `).join("");
    }
  }
}

function makeGoalCard(g, pct, challengeId, memberId) {
  return `
    <div class="goal-progress">
      <div class="goal-header">
        <strong>${escapeHtml(g.goalDescription)}</strong>
        <span class="goal-type-tag">${escapeHtml(g.goalType)}</span>
      </div>
      ${g.targetValue != null
        ? `<div class="goal-value">${g.currentValue} / ${g.targetValue} ${g.unit || ""}</div>`
        : `<div class="goal-value">${g.currentValue} pts</div>`}
      ${g.isCompleted ? '<div class="goal-complete">✅ Complete!</div>' : ""}
      ${g.targetValue ? `<div class="bar"><div class="bar-fill" style="width:${Math.min(pct, 100)}%"></div></div>` : ""}
      ${!g.isCompleted ? `<div class="goal-actions" id="goal-actions-${g.goalId}"><em>Loading activities...</em></div>` : ""}
    </div>
  `;
}

function switchMember(userId) {
  currentMemberId = userId;
  document.querySelectorAll(".member-tab").forEach(t => t.classList.remove("active"));
  document.querySelectorAll(".member-progress").forEach(d => d.style.display = "none");
  document.querySelector(`.member-tab[onclick*="'${userId}'"]`)?.classList.add("active");
  const mp = document.getElementById("member-progress-" + userId);
  if (mp) mp.style.display = "block";
}

function closeProgress() {
  currentChallengeId = null;
  currentMemberId = null;
  document.getElementById("challenge-progress").innerHTML = "";
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
    loadAchievements();
    loadChallenges();
  } else {
    const data = await res.json();
    alert(data.error || "Failed to log activity");
  }
}

async function renderActivityLog(challengeId) {
  const res = await fetch(API + `/api/challenges/${challengeId}/activity-log?count=10`, {
    headers: { ...authHeaders() },
  });
  if (!res.ok) return "";
  const entries = await res.json();
  if (!entries.length) return "";

  let html = `<div class="activity-log"><h4>Recent Activity</h4>`;
  for (const e of entries) {
    html += `<div class="activity-entry">
      <span class="activity-user">${escapeHtml(e.userEmail.split('@')[0])}</span>
      <span class="activity-action">${escapeHtml(e.activityName)}</span>
      <span class="activity-amount">${e.amount} ${escapeHtml(e.unit || "")}</span>
      ${e.notes ? `<span class="activity-notes">— ${escapeHtml(e.notes)}</span>` : ""}
      <span class="activity-time">${timeAgo(e.recordedAt)}</span>
    </div>`;
  }
  html += `</div>`;
  return html;
}

async function renderAchievements(achievements) {
  if (!achievements.length) return "";
  let html = `<div class="achievements-section"><h4>Achievements</h4>`;
  for (const a of achievements) {
    html += `<div class="achievement ${a.unlockedAt ? 'unlocked' : 'locked'}">
      ${a.unlockedAt ? "🏆" : "🔒"} ${escapeHtml(a.title)}
      ${a.unlockedAt ? " — " + new Date(a.unlockedAt).toLocaleDateString() : ""}
    </div>`;
  }
  html += `</div>`;
  return html;
}

async function loadAchievements() {
  const res = await fetch(API + "/api/achievements", {
    headers: { ...authHeaders() },
  });
  if (!res.ok) return;
  const achievements = await res.json();
  const list = document.getElementById("achievement-list");
  if (achievements.length === 0) {
    list.innerHTML = "<p>No achievements yet. Complete goals to unlock them!</p>";
    document.getElementById("achievement-count").textContent = "0";
    return;
  }
  document.getElementById("achievement-count").textContent = achievements.length;
  list.innerHTML = achievements.map(a => `
    <div class="achievement">
      🏆 ${escapeHtml(a.title)} — unlocked ${new Date(a.unlockedAt).toLocaleDateString()}
    </div>
  `).join("");
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

async function renderPrizes(challenge) {
  if (!challenge.prizes || challenge.prizes.length === 0) return "";
  let html = `<div class="prizes-section"><h4>Prizes</h4>`;
  for (const p of challenge.prizes) {
    const costStr = p.cost != null ? ` (Cost: ${p.cost} ${challenge.currencyName ? escapeHtml(challenge.currencyName) : "pts"})` : "";
    html += `<div class="prize-row">
      <span>🏅 ${escapeHtml(p.description)}${costStr}</span>
      <button onclick="generateRedemption('${challenge.id}', '${p.id}')">Generate QR</button>
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
  const img = new Image();
  img.src = qrUrl;
  img.crossOrigin = "anonymous";

  showQrModal(prize.description, prize.cost, challenge.currencyName, qrUrl);
}

function showQrModal(prizeDescription, cost, currencyName, qrUrl) {
  const modal = document.createElement("div");
  modal.id = "qr-modal";
  modal.className = "qr-modal-overlay";
  const costStr = cost != null ? `${cost} ${currencyName ? escapeHtml(currencyName) : "pts"}` : "";
  modal.innerHTML = `
    <div class="qr-modal-content">
      <div class="qr-modal-header">
        <h3>${escapeHtml(prizeDescription)}</h3>
        <button onclick="closeQrModal()" class="qr-close">&times;</button>
      </div>
      <div class="qr-print-area" id="qr-print-area">
        <div class="qr-card">
          <div class="qr-reward-label">REWARD COUPON</div>
          <div class="qr-prize-name">${escapeHtml(prizeDescription)}</div>
          ${costStr ? `<div class="qr-cost">${costStr}</div>` : ""}
          <img src="${qrUrl}" alt="QR Code" class="qr-image" crossorigin="anonymous">
        </div>
      </div>
      <div class="qr-modal-actions">
        <button onclick="printQr()">🖨️ Print</button>
        <button onclick="closeQrModal()">Close</button>
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
  const printArea = document.getElementById("qr-print-area");
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
      <h3>Redeem Prize</h3>
      <div id="redeem-status">Looking up prize...</div>
      <div id="redeem-prize-info" style="display:none"></div>
      <form id="redeem-form" style="display:none" onsubmit="confirmRedemption('${challengeId}', '${prizeId}', event)">
        <input type="text" id="redeem-notes" placeholder="Notes (optional)">
        <button type="submit">Confirm Redemption</button>
        <button type="button" onclick="closeQrModal()">Cancel</button>
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
      <div class="redeem-prize-card">
        <strong>🏅 ${escapeHtml(prize.description)}</strong>
        ${costStr ? `<div class="redeem-cost">${costStr}</div>` : ""}
      </div>
    `;
    document.getElementById("redeem-prize-info").style.display = "block";
    document.getElementById("redeem-form").style.display = "block";
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
    infoDiv.innerHTML = `
      <div class="redeem-success">✅ Redeemed! ${escapeHtml(data.prizeDescription)} for ${data.cost} pts.</div>
    `;
    document.getElementById("redeem-form").style.display = "none";
    loadChallenges();
  } else {
    const data = await res.json();
    document.getElementById("redeem-status").textContent = "❌ " + (data.error || "Redemption failed");
    btn.disabled = false;
    btn.textContent = "Confirm Redemption";
  }
}

// --- QR Scanner ---

let scannerStream = null;
let scannerAnimationId = null;

async function openScanner() {
  const existing = document.getElementById("scanner-modal");
  if (existing) existing.remove();

  const modal = document.createElement("div");
  modal.id = "scanner-modal";
  modal.className = "qr-modal-overlay";
  modal.innerHTML = `
    <div class="qr-modal-content scanner-content">
      <div class="qr-modal-header">
        <h3>Scan QR Code</h3>
        <button onclick="closeScanner()" class="qr-close">&times;</button>
      </div>
      <div class="scanner-view">
        <video id="scanner-video" autoplay playsinline muted></video>
        <canvas id="scanner-canvas" style="display:none"></canvas>
        <div id="scanner-status">Position the QR code in the camera view...</div>
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
    } catch { /* not a valid URL with claim param */ }
  }

  scannerAnimationId = requestAnimationFrame(scanFrame);
}
