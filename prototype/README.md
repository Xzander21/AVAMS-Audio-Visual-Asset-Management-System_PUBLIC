# AV Equipment Manager — Interactive Prototype

A fully self-contained, browser-based click-through prototype for the **AV Equipment Manager** application. No build step, no npm install, no backend required.

---

## How to Run

### Option A — Open directly (simplest)

1. Navigate to the `prototype/` folder.
2. Double-click `index.html` to open it in your browser.  
   *(Chrome / Edge / Firefox all work. Safari should work too.)*

> **Note:** Some browsers block `localStorage` for `file://` URLs.  
> If state doesn't persist between refreshes, use Option B.

### Option B — Simple static server (recommended)

Any static HTTP server works. Examples:

```bash
# Python (built-in)
cd prototype
python -m http.server 8080
# → open http://localhost:8080

# Node.js (npx, no install needed)
cd prototype
npx serve .
# → open the URL shown

# VS Code Live Server extension
# Right-click index.html → "Open with Live Server"
```

---

## What the Prototype Covers

| Screen | Hash route | Description |
|--------|-----------|-------------|
| **Login / Welcome** | `#` | Sign-in form — any non-empty credentials work |
| **Dashboard** | `#dashboard` | Summary stat cards, filterable/searchable equipment table |
| **Asset Detail** | `#detail` | Full detail view, lifetime progress bar, maintenance timeline |
| **Chatbot** | `#chat` | Rule-based mock chatbot with quick-action chips |
| **Settings** | `#settings` | Profile fields, toggle preferences, sign-out |

### Navigation flow

```
Login ──► Dashboard ──► Asset Detail (click any row)
              │
              ├──► Chatbot
              │
              └──► Settings ──► Sign Out ──► Login
```

All navigation is hash-based (e.g. `index.html#chat`) so browser back/forward buttons work.

---

## Interacting With the Prototype

### Dashboard
- Use the **search box** to filter by name or serial number.
- Use the **filter chips** (All / Active / Maintenance / Retired / Decommissioned) to narrow the list.
- **Click any row** to open the Asset Detail screen.
- Edit and Delete buttons show a toast notification (no real data mutation in the prototype).

### Chatbot
Supports these queries (case-insensitive):

| Query | Response |
|-------|---------|
| `Room 1` / `Room 2` / `Room 3` | Lists all equipment in that room |
| `All rooms` | Lists equipment for every room |
| `Summary` / `Overview` | Aggregated counts |
| `Active` | Lists all active equipment |
| `Maintenance` | Lists equipment under maintenance |
| `Retired` | Lists retired/decommissioned equipment |
| A serial number e.g. `AV-R1-001` | Looks up a specific asset |
| `How many` / `Total` | Equipment count |
| `Help` | Shows available commands |

Use the **quick-action chips** below the chat for one-click queries.

### Settings
- Edit display name and email — saved to `localStorage`.
- Toggle preferences (notifications, auto-refresh, compact view, sounds).
- **Save Settings** persists to `localStorage`.
- **Sign Out** returns to the Login screen.

---

## Modifying Mock Data

All mock data lives in `app.js`. No build step is needed — just edit the file and refresh the browser.

### Asset list — `MOCK_ASSETS` (line ~6)

```js
const MOCK_ASSETS = [
  {
    id: 1,
    name: 'Laser Projector 4K',   // Display name
    serial: 'AV-R1-001',          // Serial number (used for chatbot lookup)
    room: 'Room 1',               // Must match "Room 1" / "Room 2" / "Room 3"
    installed: '2021-03-15',      // ISO date string
    lifeYears: 7,                 // Expected life in years
    status: 'Active',             // Active | UnderMaintenance | Retired | Decommissioned
  },
  // …add more rows here
];
```

### Maintenance history — `MOCK_HISTORY` (line ~22)

Keys are asset `id` values. Use `default` as the fallback for assets without a specific history:

```js
const MOCK_HISTORY = {
  1: [                                           // asset id = 1
    { date: '2024-01-10', event: 'Lamp replaced' },
    { date: '2021-03-15', event: 'Installed'     },
  ],
  default: [                                     // fallback
    { date: '2023-01-15', event: 'Installed and commissioned' },
  ],
};
```

### Chatbot rules — `BOT_RULES` (line ~41)

Each rule is a `{ pattern, reply }` object. `pattern` is a JavaScript `RegExp`; `reply` is a function returning a string.

```js
{ pattern: /\bbudget\b/i,
  reply: () => 'The equipment budget report is not available in this prototype.' },
```

---

## File Structure

```
prototype/
├── index.html   ← All screens (single HTML file)
├── styles.css   ← Material-design dark theme, responsive
├── app.js       ← Router, mock data, chat engine, state
└── README.md    ← This file
```

---

## Technical Notes

- **No external dependencies** — Google Fonts is the only CDN call (optional; falls back to system sans-serif).
- **No build step** — plain HTML/CSS/JS.
- **State persistence** — `localStorage` key `av_proto_state` stores login status, chat history, and settings.  
  Clear with `localStorage.removeItem('av_proto_state')` in the browser console to reset.
- **Responsive** — tested at 360 px (mobile), 768 px (tablet), 1280 px (desktop).
- **Accessible** — semantic HTML, `aria-label` attributes on interactive elements, `role="status"` on toast.
