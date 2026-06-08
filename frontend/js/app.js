const API = API_BASE;

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

function authHeaders() {
  const token = localStorage.getItem("token");
  return token ? { "Authorization": "Bearer " + token } : {};
}

async function apiFetch(path, options = {}) {
  const res = await fetch(API + path, {
    ...options,
    headers: { ...authHeaders(), ...options.headers },
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
}

document.addEventListener("DOMContentLoaded", () => {
  const token = localStorage.getItem("token");
  if (token) {
    const payload = JSON.parse(atob(token.split(".")[1]));
    showDashboard(payload["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"]);
  }

  document.getElementById("create-family-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    const name = document.getElementById("family-name").value;
    const formData = new URLSearchParams({ name });
    const res = await apiFetch("/api/families", {
      method: "POST",
      body: formData,
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
    });
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
    const res = await apiFetch("/api/families/join", {
      method: "POST",
      body: formData,
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
    });
    if (res.ok) {
      document.getElementById("invite-code").value = "";
      loadFamilies();
    } else {
      const data = await res.json();
      alert(data.error || "Failed to join family");
    }
  });
});

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

function logout() {
  localStorage.removeItem("token");
  window.location.reload();
}

function handleAuthResponse(target, responseText) {
  const data = JSON.parse(responseText);
  localStorage.setItem("token", data.token);
  window.location.reload();
}

function escapeHtml(str) {
  const div = document.createElement("div");
  div.textContent = str;
  return div.innerHTML;
}
