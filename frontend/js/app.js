const API = API_BASE;

if ("serviceWorker" in navigator) {
  navigator.serviceWorker.register("/sw.js");
}

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
}

function showAuth() {
  document.getElementById("auth-section").style.display = "block";
  document.getElementById("dashboard").style.display = "none";
}

function logout() {
  localStorage.removeItem("token");
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

  document.getElementById("create-challenge-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    const title = document.getElementById("challenge-title").value;
    const description = document.getElementById("challenge-description").value;
    const goals = document.getElementById("challenge-goals").value.split(",").map(s => s.trim()).filter(Boolean);
    const prizes = document.getElementById("challenge-prizes").value.split(",").map(s => s.trim()).filter(Boolean);

    const body = {
      title, description, type: "SelfOnly",
      goals: goals.map(g => ({ description: g })),
      prizes: prizes.map(p => ({ description: p })),
    };
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
    return;
  }
  list.innerHTML = challenges.map(c => `
    <div class="challenge-card">
      <strong>${escapeHtml(c.title)}</strong>
      ${c.currencyName ? `<span class="badge">${escapeHtml(c.currencyName)}</span>` : ""}
      <p>${escapeHtml(c.description)}</p>
      ${c.goals.length ? `<p><strong>Goals:</strong> ${c.goals.map(g => {
        let s = escapeHtml(g.description);
        if (g.targetValue != null) s += " (" + g.targetValue + (g.unit ? " " + escapeHtml(g.unit) : "") + ")";
        if (g.pointValue != null) s += " — " + g.pointValue + " pts";
        return s;
      }).join(", ")}</p>` : ""}
      ${c.prizes.length ? `<p><strong>Prizes:</strong> ${c.prizes.map(p => {
        let s = escapeHtml(p.description);
        if (p.cost != null) s += " (cost: " + p.cost + ")";
        return s;
      }).join(", ")}</p>` : ""}
    </div>
  `).join("");
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
