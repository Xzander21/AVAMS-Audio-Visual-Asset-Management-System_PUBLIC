/* ============================================================
   AV Equipment Manager — Prototype App Logic
   Hash-based router, mock data, chat simulation, state mgmt.
   No external dependencies — pure vanilla JS.
   ============================================================ */

// ─── Mock Data ───────────────────────────────────────────────
const MOCK_ASSETS = [
  { id: 1, name: 'Laser Projector 4K',    serial: 'AV-R1-001', room: 'Room 1', installed: '2021-03-15', lifeYears: 7,  status: 'Active' },
  { id: 2, name: 'HDMI Matrix Switch',    serial: 'AV-R1-002', room: 'Room 1', installed: '2020-06-22', lifeYears: 5,  status: 'UnderMaintenance' },
  { id: 3, name: 'Ceiling Microphone Array', serial: 'AV-R1-003', room: 'Room 1', installed: '2022-01-10', lifeYears: 6,  status: 'Active' },
  { id: 4, name: 'Video Conferencing Unit', serial: 'AV-R2-001', room: 'Room 2', installed: '2019-11-05', lifeYears: 6,  status: 'Retired' },
  { id: 5, name: 'Digital Signage Display', serial: 'AV-R2-002', room: 'Room 2', installed: '2023-02-28', lifeYears: 8,  status: 'Active' },
  { id: 6, name: 'Audio DSP Processor',   serial: 'AV-R2-003', room: 'Room 2', installed: '2021-08-14', lifeYears: 5,  status: 'Active' },
  { id: 7, name: 'Interactive Whiteboard', serial: 'AV-R3-001', room: 'Room 3', installed: '2018-07-30', lifeYears: 8,  status: 'Decommissioned' },
  { id: 8, name: 'Document Camera',       serial: 'AV-R3-002', room: 'Room 3', installed: '2022-09-12', lifeYears: 5,  status: 'Active' },
  { id: 9, name: 'PTZ Camera',            serial: 'AV-R3-003', room: 'Room 3', installed: '2023-05-20', lifeYears: 7,  status: 'UnderMaintenance' },
  { id: 10, name: 'Power Conditioner',    serial: 'AV-R1-004', room: 'Room 1', installed: '2020-12-01', lifeYears: 10, status: 'Active' },
];

const MOCK_HISTORY = {
  1: [
    { date: '2024-01-10', event: 'Lamp replaced — 3 200 hrs reached' },
    { date: '2023-06-05', event: 'Firmware updated to v3.4.1' },
    { date: '2021-03-15', event: 'Installed and commissioned' },
  ],
  2: [
    { date: '2025-02-20', event: 'Sent to repair shop — HDMI port failure' },
    { date: '2022-11-14', event: 'Firmware updated to v2.1' },
    { date: '2020-06-22', event: 'Installed and commissioned' },
  ],
  default: [
    { date: '2024-06-01', event: 'Routine inspection passed' },
    { date: '2023-01-15', event: 'Installed and commissioned' },
  ],
};

// Bot knowledge base
const BOT_RULES = [
  { pattern: /\b(hello|hi|hey|greetings)\b/i,
    reply: () => 'Hello! 👋 I\'m your AV Equipment Assistant. Ask me about equipment in any room, a serial number, or request a summary.' },
  { pattern: /\broom\s*1\b/i,
    reply: () => roomSummary('Room 1') },
  { pattern: /\broom\s*2\b/i,
    reply: () => roomSummary('Room 2') },
  { pattern: /\broom\s*3\b/i,
    reply: () => roomSummary('Room 3') },
  { pattern: /\b(all rooms|every room|rooms)\b/i,
    reply: () => ['Room 1', 'Room 2', 'Room 3'].map(roomSummary).join('\n\n') },
  { pattern: /\bsummar(y|ise|ize)\b|\boverview\b/i,
    reply: () => overviewSummary() },
  { pattern: /\bactive\b/i,
    reply: () => listByStatus('Active') },
  { pattern: /\b(maintenance|under maintenance)\b/i,
    reply: () => listByStatus('UnderMaintenance') },
  { pattern: /\b(retired|decommissioned)\b/i,
    reply: () => listByStatus('Retired') + '\n' + listByStatus('Decommissioned') },
  { pattern: /AV-[A-Z0-9-]+/i,
    reply: (m) => serialLookup(m[0].toUpperCase()) },
  { pattern: /\b(total|count|how many)\b/i,
    reply: () => `There are ${MOCK_ASSETS.length} equipment items across all rooms.` },
  { pattern: /\b(help|what can you do)\b/i,
    reply: () => 'You can ask me:\n• "Room 1" — list equipment in a room\n• "Summary" — overall overview\n• "Active" / "Maintenance" — filter by status\n• A serial number like "AV-R1-001"' },
];

function roomSummary(room) {
  const items = MOCK_ASSETS.filter(a => a.room === room);
  if (!items.length) return `No equipment found in ${room}.`;
  const lines = items.map(a => `  • ${a.name} (${a.serial}) — ${statusLabel(a.status)}`);
  return `${room} has ${items.length} item(s):\n${lines.join('\n')}`;
}
function overviewSummary() {
  const total   = MOCK_ASSETS.length;
  const active  = MOCK_ASSETS.filter(a => a.status === 'Active').length;
  const maint   = MOCK_ASSETS.filter(a => a.status === 'UnderMaintenance').length;
  const retired = MOCK_ASSETS.filter(a => a.status === 'Retired' || a.status === 'Decommissioned').length;
  return `📊 Equipment Overview\n──────────────────\nTotal:             ${total}\nActive:            ${active}\nUnder Maintenance: ${maint}\nRetired/Decomm:    ${retired}`;
}
function listByStatus(status) {
  const items = MOCK_ASSETS.filter(a => a.status === status);
  if (!items.length) return `No equipment with status "${statusLabel(status)}".`;
  const lines = items.map(a => `  • ${a.name} (${a.serial}, ${a.room})`);
  return `${statusLabel(status)} equipment (${items.length}):\n${lines.join('\n')}`;
}
function serialLookup(serial) {
  const a = MOCK_ASSETS.find(x => x.serial.toUpperCase() === serial);
  if (!a) return `No equipment found with serial "${serial}".`;
  const life = remainingLife(a);
  return `Found: ${a.name}\nSerial: ${a.serial}\nRoom: ${a.room}\nStatus: ${statusLabel(a.status)}\nInstalled: ${a.installed}\nRemaining life: ${life}`;
}
function statusLabel(s) {
  return { Active: 'Active', UnderMaintenance: 'Under Maintenance', Retired: 'Retired', Decommissioned: 'Decommissioned' }[s] || s;
}
function remainingLife(asset) {
  const end = new Date(asset.installed);
  end.setFullYear(end.getFullYear() + asset.lifeYears);
  const diffDays = (end - Date.now()) / 86400000;
  if (diffDays <= 0) return 'Expired';
  const years  = Math.floor(diffDays / 365);
  const months = Math.floor((diffDays % 365) / 30);
  return `${years}y ${months}m`;
}
function statusBadgeClass(s) {
  return { Active: 'badge-active', UnderMaintenance: 'badge-maint', Retired: 'badge-retired', Decommissioned: 'badge-decomm' }[s] || '';
}
function lifePercent(asset) {
  const start = new Date(asset.installed).getTime();
  const end   = start + asset.lifeYears * 365.25 * 86400000;
  const now   = Date.now();
  return Math.min(100, Math.max(0, Math.round(((now - start) / (end - start)) * 100)));
}

// ─── State ────────────────────────────────────────────────────
let state = {
  loggedIn:       false,
  currentScreen:  'login',
  chatMessages:   [],
  assets:         JSON.parse(JSON.stringify(MOCK_ASSETS)),
  filterStatus:   'all',
  searchQuery:    '',
  currentAssetId: null,
  settings: {
    darkMode:           true,
    notifications:      true,
    autoRefresh:        false,
    compactView:        false,
    botSounds:          false,
    name:               'Admin User',
    email:              'admin@avmanager.local',
  },
};

function loadState() {
  try {
    const saved = localStorage.getItem('av_proto_state');
    if (saved) {
      const parsed = JSON.parse(saved);
      // Only restore non-volatile fields
      state.loggedIn      = parsed.loggedIn      ?? false;
      state.chatMessages  = parsed.chatMessages  ?? [];
      state.settings      = { ...state.settings, ...parsed.settings };
    }
  } catch (_) { /* ignore */ }
}
function saveState() {
  try {
    localStorage.setItem('av_proto_state', JSON.stringify({
      loggedIn:     state.loggedIn,
      chatMessages: state.chatMessages.slice(-60),
      settings:     state.settings,
    }));
  } catch (_) { /* ignore */ }
}

// ─── Router ───────────────────────────────────────────────────
const SCREENS = ['login', 'dashboard', 'chat', 'detail', 'settings'];

function navigate(screen, params = {}) {
  if (!SCREENS.includes(screen)) return;
  if (!state.loggedIn && screen !== 'login') { navigate('login'); return; }
  if (state.loggedIn  && screen === 'login') { navigate('dashboard'); return; }

  if (screen === 'detail' && params.assetId) {
    state.currentAssetId = params.assetId;
  }

  state.currentScreen = screen;
  window.location.hash = screen === 'login' ? '' : screen;
  render();
  saveState();
}

function handleHashChange() {
  const hash = window.location.hash.replace('#', '').trim() || 'login';
  if (!state.loggedIn && hash !== 'login') { navigate('login'); return; }
  if (state.loggedIn  && (hash === 'login' || hash === '')) { navigate('dashboard'); return; }
  if (SCREENS.includes(hash)) {
    state.currentScreen = hash;
    render();
  }
}

// ─── Render ───────────────────────────────────────────────────
function render() {
  const screen = state.currentScreen;

  // Hide / show shell
  const sidebar = document.getElementById('sidebar');
  const topbar  = document.getElementById('topbar');
  const needShell = screen !== 'login';
  sidebar.classList.toggle('hidden', !needShell);
  topbar.classList.toggle('hidden', !needShell);

  // Active nav button
  document.querySelectorAll('.nav-btn').forEach(btn => {
    btn.classList.toggle('active', btn.dataset.screen === screen);
  });

  // Topbar title
  const titles = { dashboard: 'Dashboard', chat: 'AV Equipment Chatbot', detail: 'Asset Detail', settings: 'Settings' };
  document.getElementById('topbar-title').textContent = titles[screen] || '';

  // Show correct screen
  SCREENS.forEach(s => {
    const el = document.getElementById(`screen-${s}`);
    if (el) el.classList.toggle('active', s === screen);
  });

  // Per-screen rendering
  if      (screen === 'dashboard') renderDashboard();
  else if (screen === 'chat')      renderChat();
  else if (screen === 'detail')    renderDetail();
  else if (screen === 'settings')  renderSettings();
}

// ─── Login ────────────────────────────────────────────────────
function handleLogin(e) {
  e.preventDefault();
  const email = document.getElementById('login-email').value.trim();
  const pass  = document.getElementById('login-pass').value;
  if (!email) { showToast('Please enter an email.'); return; }
  if (!pass)  { showToast('Please enter a password.'); return; }

  // Mock: any non-empty credentials work
  state.loggedIn = true;
  if (!state.chatMessages.length) {
    state.chatMessages.push({
      isUser: false,
      text: "Hello! I'm your AV Equipment Assistant. You can ask me about equipment in any room. Try typing 'Room 1' or ask about a specific serial number like 'AV-R1-001'!",
      time: nowTime(),
    });
  }
  navigate('dashboard');
}

// ─── Dashboard ────────────────────────────────────────────────
function renderDashboard() {
  const assets = filteredAssets();
  const total  = state.assets.length;
  const active = state.assets.filter(a => a.status === 'Active').length;
  const maint  = state.assets.filter(a => a.status === 'UnderMaintenance').length;
  const retd   = state.assets.filter(a => a.status === 'Retired' || a.status === 'Decommissioned').length;

  document.getElementById('stat-total').textContent  = total;
  document.getElementById('stat-active').textContent = active;
  document.getElementById('stat-maint').textContent  = maint;
  document.getElementById('stat-retd').textContent   = retd;

  // Sync filter chips
  document.querySelectorAll('.filter-chip').forEach(chip => {
    chip.classList.toggle('active', chip.dataset.filter === state.filterStatus);
  });

  // Sync search box
  const searchEl = document.getElementById('asset-search');
  if (searchEl && document.activeElement !== searchEl) {
    searchEl.value = state.searchQuery;
  }

  // Render table rows
  const tbody = document.getElementById('assets-tbody');
  tbody.innerHTML = assets.length
    ? assets.map(a => {
        const life = remainingLife(a);
        const lifeClass = life === 'Expired' ? 'badge-retired' : '';
        return `<tr data-id="${a.id}" onclick="navigate('detail', {assetId: ${a.id}})">
          <td>${a.name}</td>
          <td>${a.serial}</td>
          <td>${a.room}</td>
          <td>${a.installed}</td>
          <td><span class="badge ${lifeClass}">${life}</span></td>
          <td><span class="badge ${statusBadgeClass(a.status)}">${statusLabel(a.status)}</span></td>
          <td>
            <button class="btn-icon edit" title="Edit" onclick="event.stopPropagation(); showToast('Edit dialog — prototype only')">✏️</button>
            <button class="btn-icon delete" title="Delete" onclick="event.stopPropagation(); showToast('Delete — prototype only')">🗑️</button>
          </td>
        </tr>`;
      }).join('')
    : `<tr><td colspan="7" style="text-align:center;color:var(--text-secondary);padding:1.5rem">No assets found.</td></tr>`;
}

function filteredAssets() {
  return state.assets.filter(a => {
    const matchStatus = state.filterStatus === 'all' || a.status === state.filterStatus;
    const q = state.searchQuery.toLowerCase();
    const matchSearch = !q || a.name.toLowerCase().includes(q) || a.serial.toLowerCase().includes(q);
    return matchStatus && matchSearch;
  });
}

function setFilter(f) {
  state.filterStatus = f;
  renderDashboard();
}

function handleSearch(q) {
  state.searchQuery = q;
  renderDashboard();
}

// ─── Asset Detail ─────────────────────────────────────────────
function renderDetail() {
  const asset = state.assets.find(a => a.id === state.currentAssetId);
  if (!asset) { navigate('dashboard'); return; }

  document.getElementById('detail-name').textContent   = asset.name;
  document.getElementById('detail-serial').textContent = `Serial: ${asset.serial}`;

  const badgeEl = document.getElementById('detail-badge');
  badgeEl.textContent = statusLabel(asset.status);
  badgeEl.className   = `badge ${statusBadgeClass(asset.status)}`;

  document.getElementById('detail-room').textContent      = asset.room;
  document.getElementById('detail-installed').textContent = asset.installed;
  document.getElementById('detail-life').textContent      = `${asset.lifeYears} yr(s)`;
  document.getElementById('detail-remaining').textContent = remainingLife(asset);

  const pct      = lifePercent(asset);
  const fillEl   = document.getElementById('detail-life-fill');
  fillEl.style.width = `${pct}%`;
  fillEl.className   = `progress-bar-fill ${pct < 50 ? 'fill-green' : pct < 80 ? 'fill-orange' : 'fill-red'}`;
  document.getElementById('detail-life-pct').textContent = `${pct}% of expected life used`;

  const history = MOCK_HISTORY[asset.id] || MOCK_HISTORY.default;
  document.getElementById('detail-timeline').innerHTML = history.map(h =>
    `<div class="timeline-item">
       <div class="tl-dot"></div>
       <div><div class="tl-date">${h.date}</div><div class="tl-event">${h.event}</div></div>
     </div>`
  ).join('');
}

// ─── Chat ─────────────────────────────────────────────────────
function renderChat() {
  rebuildChatMessages();
}

function rebuildChatMessages() {
  const container = document.getElementById('chat-messages');
  container.innerHTML = state.chatMessages.map(m => messageBubbleHTML(m)).join('');
  container.scrollTop = container.scrollHeight;
}

function messageBubbleHTML(m) {
  const side = m.isUser ? 'user' : 'bot';
  return `<div class="msg-row ${side}">
    <div>
      <div class="msg-bubble ${side}">${escapeHTML(m.text)}</div>
      <div class="msg-time">${m.time}</div>
    </div>
  </div>`;
}

function appendMessage(m) {
  state.chatMessages.push(m);
  const container = document.getElementById('chat-messages');
  if (!container) return;
  const div = document.createElement('div');
  div.innerHTML = messageBubbleHTML(m);
  container.appendChild(div.firstElementChild);
  container.scrollTop = container.scrollHeight;
}

function showTypingIndicator() {
  const container = document.getElementById('chat-messages');
  if (!container) return;
  const el = document.createElement('div');
  el.id = 'typing';
  el.className = 'msg-row bot';
  el.innerHTML = `<div class="typing-indicator"><div class="dot"></div><div class="dot"></div><div class="dot"></div></div>`;
  container.appendChild(el);
  container.scrollTop = container.scrollHeight;
}
function hideTypingIndicator() {
  const el = document.getElementById('typing');
  if (el) el.remove();
}

function sendChatMessage() {
  const input = document.getElementById('chat-input');
  const text = input.value.trim();
  if (!text) return;
  input.value = '';
  input.focus();

  appendMessage({ isUser: true, text, time: nowTime() });
  simulateBotReply(text);
  saveState();
}

function sendQuickChip(label) {
  appendMessage({ isUser: true, text: label, time: nowTime() });
  simulateBotReply(label);
  saveState();
}

function simulateBotReply(query) {
  showTypingIndicator();
  const delay = 600 + Math.random() * 700;
  setTimeout(() => {
    hideTypingIndicator();
    const reply = getBotReply(query);
    appendMessage({ isUser: false, text: reply, time: nowTime() });
    saveState();
  }, delay);
}

function getBotReply(query) {
  for (const rule of BOT_RULES) {
    const m = query.match(rule.pattern);
    if (m) return rule.reply(m);
  }
  return `I'm not sure how to answer that. Try asking about a room ("Room 1"), a serial number ("AV-R1-001"), or type "help" for options.`;
}

function handleChatKey(e) {
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault();
    sendChatMessage();
  }
}

// ─── Settings ─────────────────────────────────────────────────
function renderSettings() {
  const s = state.settings;
  document.getElementById('set-name').value   = s.name;
  document.getElementById('set-email').value  = s.email;
  document.getElementById('toggle-dark').checked    = s.darkMode;
  document.getElementById('toggle-notif').checked   = s.notifications;
  document.getElementById('toggle-refresh').checked = s.autoRefresh;
  document.getElementById('toggle-compact').checked = s.compactView;
  document.getElementById('toggle-sounds').checked  = s.botSounds;
}

function saveSettings() {
  state.settings.name   = document.getElementById('set-name').value.trim() || state.settings.name;
  state.settings.email  = document.getElementById('set-email').value.trim() || state.settings.email;
  state.settings.darkMode      = document.getElementById('toggle-dark').checked;
  state.settings.notifications = document.getElementById('toggle-notif').checked;
  state.settings.autoRefresh   = document.getElementById('toggle-refresh').checked;
  state.settings.compactView   = document.getElementById('toggle-compact').checked;
  state.settings.botSounds     = document.getElementById('toggle-sounds').checked;
  saveState();
  showToast('Settings saved ✓');
}

function logout() {
  state.loggedIn = false;
  saveState();
  navigate('login');
}

// ─── Sidebar mobile toggle ────────────────────────────────────
function toggleSidebar() {
  document.getElementById('sidebar').classList.toggle('mobile-open');
}

// ─── Utilities ────────────────────────────────────────────────
function nowTime() {
  return new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
}
function escapeHTML(str) {
  return str.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
}

let toastTimer;
function showToast(msg) {
  const el = document.getElementById('toast');
  el.textContent = msg;
  el.classList.add('show');
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => el.classList.remove('show'), 2800);
}

// ─── Bootstrap ────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
  loadState();

  // Allow screenshot/test pages to override state before rendering
  if (window.__OVERRIDE__) {
    Object.assign(state, window.__OVERRIDE__);
    render();
    return;
  }

  window.addEventListener('hashchange', handleHashChange);

  // Initial screen
  const hash = window.location.hash.replace('#', '').trim();
  if (state.loggedIn && hash && SCREENS.includes(hash)) {
    state.currentScreen = hash;
  } else if (state.loggedIn) {
    state.currentScreen = 'dashboard';
  } else {
    state.currentScreen = 'login';
  }
  render();
});
