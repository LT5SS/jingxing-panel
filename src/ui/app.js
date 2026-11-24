const params = new URLSearchParams(location.search);
const DEMO = params.has('demo');
const MINIMAL_DEMO = DEMO && params.get('minimal') === '1';
const HAS_HOST = !!(window.chrome && window.chrome.webview);

const $ = (id) => document.getElementById(id);
const post = (msg) => window.chrome?.webview?.postMessage(msg);

setTimeout(() => {
  try {
    post({
      action: 'jsError',
      text: 'vp ' + window.innerWidth + 'x' + window.innerHeight + ' dpr ' + (window.devicePixelRatio || 1) + ' scroll ' + document.documentElement.scrollWidth
    });
  } catch (_) {}
}, 1200);

window.onerror = (m, s, l, c) => {
  try { post({ action: 'jsError', text: String(m) + ' @' + l + ':' + c }); } catch (_) {}
};

/* ==================== Material You Color Engine ==================== */
function hexToRgb(hex) {
  const m = String(hex || '').trim().match(/^#?([0-9a-f]{6})$/i);
  if (!m) return { r: 124, g: 93, b: 255 };
  const n = parseInt(m[1], 16);
  return { r: (n >> 16) & 255, g: (n >> 8) & 255, b: n & 255 };
}

function rgbToHex(r, g, b) {
  const clamp = (x) => Math.max(0, Math.min(255, Math.round(x)));
  return '#' + [clamp(r), clamp(g), clamp(b)].map((v) => v.toString(16).padStart(2, '0')).join('');
}

function rgbToHsl(r, g, b) {
  r /= 255; g /= 255; b /= 255;
  const max = Math.max(r, g, b), min = Math.min(r, g, b);
  let h = 0, s = 0, l = (max + min) / 2;
  if (max !== min) {
    const d = max - min;
    s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
    if (max === r) h = ((g - b) / d + (g < b ? 6 : 0)) / 6;
    else if (max === g) h = ((b - r) / d + 2) / 6;
    else h = ((r - g) / d + 4) / 6;
  }
  return { h: h * 360, s: s * 100, l: l * 100 };
}

function hslToHex(h, s, l) {
  h = ((h % 360) + 360) % 360;
  s = Math.max(0, Math.min(100, s)) / 100;
  l = Math.max(0, Math.min(100, l)) / 100;
  const a = s * Math.min(l, 1 - l);
  const f = (n) => {
    const k = (n + h / 30) % 12;
    return l - a * Math.max(Math.min(k - 3, 9 - k, 1), -1);
  };
  return rgbToHex(f(0) * 255, f(8) * 255, f(4) * 255);
}

function mixRgb(hexA, hexB, t) {
  const a = hexToRgb(hexA), b = hexToRgb(hexB);
  return rgbToHex(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t);
}

function rgba(hex, alpha) {
  const { r, g, b } = hexToRgb(hex);
  return `rgba(${r},${g},${b},${alpha})`;
}

let systemAccentHex = '#6669cc';
let themeMode = params.get('theme') || localStorage.getItem('jx-theme-mode') || 'auto';
let seedChoice = localStorage.getItem('jx-seed') || '#7c5dff';
let customSeedHex = localStorage.getItem('jx-custom-seed') || '#7c5dff';
let pureBlack = localStorage.getItem('jx-pure-black') === '1';
let compactMode = localStorage.getItem('jx-compact') === '1';
let railDefaultOpen = params.get('rail') === '1' || localStorage.getItem('jx-rail-open') === '1';

function activeSeedHex() {
  if (seedChoice === 'system') return systemAccentHex || '#6669cc';
  return /^#[0-9a-f]{6}$/i.test(seedChoice) ? seedChoice : '#7c5dff';
}

function isDarkResolved(mode = themeMode) {
  return mode === 'dark' || (mode === 'auto' && !window.matchMedia('(prefers-color-scheme: light)').matches);
}

function applyMaterialPalette(syncImmediate = false) {
  const dark = isDarkResolved();
  const seed = activeSeedHex();
  const { h, s } = rgbToHsl(...Object.values(hexToRgb(seed)));
  const sat = Math.max(36, Math.min(82, s || 58));

  let tokens;
  if (dark) {
    const primary = hslToHex(h, Math.min(90, sat + 10), 82);
    const onPrimary = hslToHex(h, Math.min(72, sat), 22);
    const priCont = hslToHex(h, Math.min(62, sat), 32);
    const onPriCont = hslToHex(h, Math.min(85, sat + 8), 90);
    const secCont = hslToHex(h, Math.max(14, sat * 0.28), 28);
    const onSecCont = hslToHex(h, Math.max(18, sat * 0.35), 90);
    const bg = pureBlack ? '#000000' : hslToHex(h, 14, 8.2);
    const surf = pureBlack ? '#09090b' : hslToHex(h, 14, 10.8);
    const s1 = pureBlack ? '#111115' : hslToHex(h, 14, 13.5);
    const s2 = pureBlack ? '#1a1a20' : hslToHex(h, 13, 17.5);
    const s3 = pureBlack ? '#24242c' : hslToHex(h, 12, 22.0);
    const fabBg = hslToHex(h, Math.min(72, sat + 6), 62);
    const heroWave = pureBlack ? mixRgb('#0d0c14', seed, 0.22) : hslToHex(h, Math.max(18, sat * 0.34), 19.5);
    tokens = {
      '--md-bg': bg, '--md-surface': surf, '--md-surface-1': s1, '--md-surface-2': s2, '--md-surface-3': s3,
      '--md-on-surface': '#e6e0e9', '--md-on-surface-var': '#c8c2ce', '--md-outline': '#8d8795',
      '--md-outline-variant': 'rgba(202,196,208,0.11)',
      '--md-primary': primary, '--md-on-primary': onPrimary,
      '--md-primary-container': priCont, '--md-on-primary-container': onPriCont,
      '--md-secondary-container': secCont, '--md-on-secondary-container': onSecCont,
      '--md-seg-border': 'rgba(255,255,255,0.06)',
      '--md-primary-tint': rgba(primary, 0.13), '--md-primary-glow': rgba(primary, 0.22),
      '--md-fab-bg': fabBg, '--md-fab-fg': '#ffffff', '--md-fab-aura': rgba(fabBg, 0.44),
      '--md-hero-wave': heroWave,
      '--md-run-bg': 'rgba(34,197,94,0.20)', '--md-run-fg': '#4ade80',
      '--md-ok': '#7ee787', '--md-ok-bg': 'rgba(126,231,135,0.14)',
      '--md-warn': '#f2c97d', '--md-warn-bg': 'rgba(242,201,125,0.15)',
      '--md-err': '#f2b8b5', '--md-err-bg': 'rgba(242,184,181,0.16)',
    };
  } else {
    const primary = hslToHex(h, Math.min(76, sat + 4), 42);
    const onPrimary = '#ffffff';
    const priCont = hslToHex(h, Math.min(82, sat + 6), 90);
    const onPriCont = hslToHex(h, Math.min(76, sat), 18);
    const secCont = hslToHex(h, Math.max(36, sat * 0.58), 82);
    const onSecCont = hslToHex(h, Math.min(78, sat * 0.8), 15);
    const bg = mixRgb('#faf8fc', seed, 0.035);
    const surf = mixRgb('#f3eff8', seed, 0.055);
    const s1 = '#ffffff';
    const s2 = mixRgb('#f1ecf7', seed, 0.05);
    const s3 = mixRgb('#e7dff1', seed, 0.07);
    const fabBg = hslToHex(h, Math.min(76, sat + 5), 48);
    const heroWave = hslToHex(h, Math.min(62, sat * 0.68), 91.5);
    tokens = {
      '--md-bg': bg, '--md-surface': surf, '--md-surface-1': s1, '--md-surface-2': s2, '--md-surface-3': s3,
      '--md-on-surface': '#1d1b20', '--md-on-surface-var': '#49454f', '--md-outline': '#76707d',
      '--md-outline-variant': 'rgba(29,27,32,0.10)',
      '--md-primary': primary, '--md-on-primary': onPrimary,
      '--md-primary-container': priCont, '--md-on-primary-container': onPriCont,
      '--md-secondary-container': secCont, '--md-on-secondary-container': onSecCont,
      '--md-seg-border': rgba(primary, 0.32),
      '--md-primary-tint': rgba(primary, 0.11), '--md-primary-glow': rgba(primary, 0.18),
      '--md-fab-bg': fabBg, '--md-fab-fg': '#ffffff', '--md-fab-aura': rgba(fabBg, 0.30),
      '--md-hero-wave': heroWave,
      '--md-run-bg': 'rgba(22,163,74,0.14)', '--md-run-fg': '#15803d',
      '--md-ok': '#1a7f37', '--md-ok-bg': 'rgba(26,127,55,0.12)',
      '--md-warn': '#9a6700', '--md-warn-bg': 'rgba(154,103,0,0.12)',
      '--md-err': '#b3261e', '--md-err-bg': 'rgba(179,38,30,0.11)',
    };
  }

  const root = document.documentElement;
  root.style.colorScheme = dark ? 'dark' : 'light';
  for (const [k, v] of Object.entries(tokens)) root.style.setProperty(k, v);

  document.body.classList.toggle('light', !dark);
  document.body.classList.toggle('pure-black', dark && pureBlack);
  document.body.classList.toggle('compact', compactMode);

  const META = {
    auto: ['brightness_auto', '跟随系统'],
    light: ['light_mode', '浅色'],
    dark: ['dark_mode', '深色']
  };
  const [ico, txt] = META[themeMode] || META.auto;
  if ($('theme-icon')) $('theme-icon').textContent = ico;
  if ($('theme-label')) $('theme-label').textContent = txt;
  if ($('theme-btn')) $('theme-btn').dataset.tip = '主题：' + txt;
  if ($('seed-hex-label')) $('seed-hex-label').textContent = seed.toUpperCase();
  if ($('sys-accent-dot')) $('sys-accent-dot').style.background = systemAccentHex;
  if ($('custom-dot')) $('custom-dot').style.background = customSeedHex;

  document.querySelectorAll('#theme-seg .seg').forEach((b) => {
    b.classList.toggle('active', b.dataset.themeMode === themeMode);
  });
  if (typeof activePage !== 'undefined' && activePage === 'theme') {
    syncSegThumb('theme-seg', syncImmediate);
  }
  document.querySelectorAll('#swatch-grid .swatch-card').forEach((c) => {
    const ds = c.dataset.seed;
    const isCustom = !ds;
    const active = isCustom
      ? (seedChoice !== 'system' && !['#7c5dff', '#3b82f6', '#0ea5a0', '#e05375', '#e59824', '#3ba55d'].includes(seedChoice.toLowerCase()))
      : ds.toLowerCase() === seedChoice.toLowerCase();
    c.classList.toggle('active', active);
  });
  if ($('sw-pure-black')) $('sw-pure-black').classList.toggle('on', pureBlack);
  if ($('sw-compact')) $('sw-compact').classList.toggle('on', compactMode);
  if ($('sw-rail-expand')) $('sw-rail-expand').classList.toggle('on', railDefaultOpen);

  post({ action: 'theme', dark, bg: tokens['--md-bg'] });
}

function runViewRipple(originEl, applyFn) {
  const el = originEl || $('theme-btn');
  const r = el ? el.getBoundingClientRect() : { left: 40, top: innerHeight - 40, width: 0, height: 0 };
  const cx = r.left + r.width / 2;
  const cy = r.top + r.height / 2;
  const rad = Math.hypot(Math.max(cx, innerWidth - cx), Math.max(cy, innerHeight - cy));
  document.documentElement.style.setProperty('--ripple-x', cx + 'px');
  document.documentElement.style.setProperty('--ripple-y', cy + 'px');
  document.documentElement.style.setProperty('--ripple-r', rad + 'px');

  if (!document.startViewTransition || document.body.classList.contains('no-trans')) {
    applyFn();
    return;
  }
  const vt = document.startViewTransition(() => applyFn());
  vt.finished.catch(() => {});
}

function setThemeWithRipple(nextMode, originEl) {
  const prevDark = !document.body.classList.contains('light');
  const nextDark = isDarkResolved(nextMode);
  themeMode = nextMode;
  localStorage.setItem('jx-theme-mode', themeMode);
  if (prevDark !== nextDark) {
    runViewRipple(originEl, () => applyMaterialPalette(true));
  } else {
    applyMaterialPalette(false);
  }
}

window.applyThemeFromHost = (t) => setThemeWithRipple(t, $('theme-btn'));

/* ==================== Helpers & Formatting ==================== */
const setText = (id, v) => { const e = $(id); if (e) e.textContent = v; };
function setDot(el, state) {
  if (!el) return;
  el.className = 'dot ' + (state === 'green' ? 'dot-green' : state === 'warn' ? 'dot-warn' : state === 'gray' ? 'dot-gray' : 'dot-red');
}
function fmtTime(ms) {
  if (!ms) return '—';
  return new Date(ms).toLocaleTimeString('zh-CN', { hour12: false }).slice(0, 5);
}
function fmtDur(min) {
  if (min == null || isNaN(min)) return '—';
  const m = Math.max(0, Math.round(min));
  if (m < 60) return m + 'm';
  return Math.floor(m / 60) + 'h' + String(m % 60).padStart(2, '0') + 'm';
}

let snackTimer = null;
function toast(msg) {
  const sb = $('snackbar');
  if (!sb || !msg) return;
  sb.textContent = msg;
  sb.classList.add('show');
  clearTimeout(snackTimer);
  snackTimer = setTimeout(() => sb.classList.remove('show'), 2600);
}

/* ==================== Sparklines ==================== */
const vramHist = DEMO || !HAS_HOST ? [1.8, 1.9, 2.3, 2.3, 3.8, 4.9, 4.9, 3.2, 2.5, 2.4, 2.4, 2.6] : [];
const memHist = DEMO || !HAS_HOST ? [10.1, 10.2, 10.3, 10.2, 10.4, 10.4, 10.5, 10.4, 10.4] : [];

function pushHist(arr, val, maxLen = 22) {
  if (val == null || isNaN(val)) return;
  arr.push(Number(val));
  if (arr.length > maxLen) arr.shift();
}

function drawSpark(arr, lineId, areaId, w, h, fixedMax) {
  const line = $(lineId);
  const area = areaId ? $(areaId) : null;
  if (!line) return;
  if (!arr.length) {
    line.setAttribute('d', '');
    if (area) area.setAttribute('d', '');
    return;
  }
  const pts = arr.length === 1 ? [arr[0], arr[0]] : arr;
  const max = fixedMax > 0 ? fixedMax : Math.max(...pts, 1);
  const min = fixedMax > 0 ? 0 : Math.min(...pts) * 0.85;
  const span = Math.max(max - min, 0.1);
  const coords = pts.map((v, i) => {
    const x = (i / (pts.length - 1)) * w;
    const y = h - 3 - ((v - min) / span) * (h - 6);
    return [Number(x.toFixed(1)), Number(y.toFixed(1))];
  });
  const d = coords.map(([x, y], i) => (i === 0 ? `M${x},${y}` : `L${x},${y}`)).join(' ');
  line.setAttribute('d', d);
  if (area) {
    const last = coords[coords.length - 1];
    area.setAttribute('d', `${d} L${last[0]},${h} L0,${h} Z`);
  }
}

/* ==================== Expressive Polar Curves & Hero Anim ==================== */
function polarWavePath(lobes, rBase, amp, phase = 0, steps = 144) {
  const pts = [];
  for (let i = 0; i < steps; i++) {
    const a = (i / steps) * Math.PI * 2;
    const r = rBase + amp * Math.cos(lobes * (a - phase));
    const x = 50 + r * Math.cos(a);
    const y = 50 + r * Math.sin(a);
    pts.push((i === 0 ? 'M' : 'L') + x.toFixed(2) + ',' + y.toFixed(2));
  }
  return pts.join(' ') + ' Z';
}

const FAB_TARGET_AMP = 2.45;

function initExpressiveShapes() {
  const wave = $('hero-wave-path');
  const clover = $('hero-clover-path');
  const fab = $('hero-fab-path');
  if (wave) wave.setAttribute('d', polarWavePath(10, 45.4, 3.2, 0, 160));
  if (clover) clover.setAttribute('d', polarWavePath(4, 41.5, 6.8, Math.PI / 4, 120));
  if (fab) fab.setAttribute('d', polarWavePath(12, 42.5, 0, 0, 180));
}

let currentFabAmp = 0;
let fabTargetAmp = -1;
let fabMorphRaf = 0;

function morphFabAmp(targetAmp, immediate = false) {
  const fab = $('hero-fab-path');
  if (!fab) return;
  if (Math.abs(targetAmp - fabTargetAmp) < 0.01 && !immediate) return;
  fabTargetAmp = targetAmp;
  if (fabMorphRaf) cancelAnimationFrame(fabMorphRaf);
  if (immediate || document.body.classList.contains('no-trans')) {
    currentFabAmp = targetAmp;
    const rBase = 42.2 + (currentFabAmp / FAB_TARGET_AMP) * 2.5;
    fab.setAttribute('d', polarWavePath(12, rBase, currentFabAmp, 0, 180));
    return;
  }
  const startAmp = currentFabAmp;
  const startT = performance.now();
  const dur = 460;
  const easeOutBack = (t) => {
    const c1 = 1.45, c3 = c1 + 1;
    return 1 + c3 * Math.pow(t - 1, 3) + c1 * Math.pow(t - 1, 2);
  };
  function step(now) {
    const raw = Math.min(1, (now - startT) / dur);
    const k = easeOutBack(raw);
    currentFabAmp = Math.max(0, startAmp + (targetAmp - startAmp) * k);
    const rBase = 42.2 + Math.min(1.15, currentFabAmp / FAB_TARGET_AMP) * 2.5;
    fab.setAttribute('d', polarWavePath(12, rBase, currentFabAmp, 0, 180));
    if (raw < 1) fabMorphRaf = requestAnimationFrame(step);
    else {
      currentFabAmp = targetAmp;
      fabMorphRaf = 0;
    }
  }
  fabMorphRaf = requestAnimationFrame(step);
}

let bridgeStartMs = null;
function fmtHms(ms) {
  if (!ms || ms < 0) return '00:00:00';
  const sec = Math.floor(ms / 1000);
  const h = Math.floor(sec / 3600);
  const m = Math.floor((sec % 3600) / 60);
  const s = sec % 60;
  return [h, m, s].map((v) => String(v).padStart(2, '0')).join(':');
}
function tickUptime() {
  if (!heroRunning || !bridgeStartMs) {
    setText('hero-uptime-hms', '00:00:00');
    return;
  }
  setText('hero-uptime-hms', fmtHms(Date.now() - bridgeStartMs));
}
setInterval(tickUptime, 1000);

/* ==================== Adaptive Feature Layout ==================== */
let lastFeatures = MINIMAL_DEMO
  ? { comfyui: false, tts: false, sleep: false, backup: false }
  : { comfyui: true, tts: true, sleep: true, backup: true };

function setSpanClass(el, span) {
  if (!el) return;
  el.classList.remove('col-3', 'col-4', 'col-6', 'col-8', 'col-12');
  el.classList.add('col-' + span);
}

function applyFeatureVisibility(features, ttsUp = false) {
  const f = features || lastFeatures;
  lastFeatures = {
    comfyui: f.comfyui !== false,
    tts: f.tts !== false,
    sleep: f.sleep !== false,
    backup: f.backup !== false,
  };
  const showComfy = lastFeatures.comfyui;
  const showTts = lastFeatures.tts;
  const showSleep = lastFeatures.sleep;
  const showBackup = lastFeatures.backup;
  const showFourthNode = showComfy || showTts;

  $('vram-card')?.classList.toggle('hidden', !showComfy);
  $('sleep-card')?.classList.toggle('hidden', !showSleep);
  $('comfy-card')?.classList.toggle('hidden', !showFourthNode);
  $('btn-backup')?.classList.toggle('hidden', !showBackup);
  $('btn-comfy-start')?.classList.toggle('hidden', !showComfy);
  $('btn-comfy-stop')?.classList.toggle('hidden', !showComfy);
  $('tile-draws')?.classList.toggle('hidden', !showComfy);
  $('tile-debt')?.classList.toggle('hidden', !showSleep);
  $('tts-line')?.classList.toggle('hidden', !showTts);

  // Row 1 spans: 6+3+3, or 8+4, or 12
  if (showComfy && showSleep) {
    setSpanClass($('hero-card'), 6);
    setSpanClass($('vram-card'), 3);
    setSpanClass($('sleep-card'), 3);
  } else if (showComfy || showSleep) {
    setSpanClass($('hero-card'), 8);
    if (showComfy) setSpanClass($('vram-card'), 4);
    if (showSleep) setSpanClass($('sleep-card'), 4);
  } else {
    setSpanClass($('hero-card'), 12);
  }

  // Row 2 service nodes: 3+3+3+3 or 4+4+4
  const nodeSpan = showFourthNode ? 3 : 4;
  setSpanClass($('node-bridge'), nodeSpan);
  setSpanClass($('node-dsh'), nodeSpan);
  setSpanClass($('node-sl'), nodeSpan);

  // Fourth service node adaptation (ComfyUI vs TTS-only)
  const comfyCard = $('comfy-card');
  const comfySw = $('comfy-switch');
  if (comfyCard) {
    if (showComfy) {
      setText('comfy-node-title', showTts ? '画图 / 语音' : 'ComfyUI');
      comfyCard.title = '点击切换 ComfyUI 启停';
      comfyCard.classList.add('clickable');
      if (comfySw) comfySw.classList.remove('hidden');
    } else if (showTts) {
      setText('comfy-node-title', 'TTS 语音');
      comfyCard.title = '本地语音合成状态';
      comfyCard.classList.remove('clickable');
      if (comfySw) comfySw.classList.add('hidden');
      setText('m-comfy', ttsUp ? '在线' : '离线');
      setDot($('m-dot-comfy'), ttsUp ? 'green' : 'gray');
      setText('comfy-sub', '本地服务');
    }
  }

  // Metric grid columns (2 or 3 or 4 visible tiles -> never leave orphan cell)
  const visibleMetrics = 2 + (showComfy ? 1 : 0) + (showSleep ? 1 : 0);
  $('metric-grid')?.classList.toggle('cols-3', visibleMetrics === 3);
}

/* ==================== Status Renderer ==================== */
let heroRunning = false;
let heroBusy = false;
let comfyUp = false;
let demoToggleOverride = params.get('state') === 'stopped' ? false : null;

const tweenState = new Map();
function tweenNumber(elId, target, fmtFn, dur = 380) {
  const el = $(elId);
  if (!el) return;
  if (target == null || isNaN(target)) {
    tweenState.delete(elId);
    el.textContent = '—';
    return;
  }
  const to = Number(target);
  const st = tweenState.get(elId);
  if (!st || document.body.classList.contains('no-trans') || Math.abs(st.val - to) < 0.001) {
    if (st?.raf) cancelAnimationFrame(st.raf);
    tweenState.set(elId, { val: to, raf: 0 });
    el.textContent = fmtFn(to);
    return;
  }
  if (st.raf) cancelAnimationFrame(st.raf);
  const from = st.val;
  const t0 = performance.now();
  const entry = { val: from, raf: 0 };
  tweenState.set(elId, entry);
  function step(now) {
    const p = Math.min(1, (now - t0) / dur);
    const e = 1 - Math.pow(1 - p, 3);
    entry.val = from + (to - from) * e;
    el.textContent = fmtFn(p >= 1 ? to : entry.val);
    if (p < 1) entry.raf = requestAnimationFrame(step);
    else { entry.val = to; entry.raf = 0; }
  }
  entry.raf = requestAnimationFrame(step);
}

let heroStateInited = false;
let prevHeroRunning = false;

function syncHeroBtn(immediate = false) {
  const btn = $('hero-toggle');
  const card = $('hero-card');
  if (!btn) return;
  const active = heroRunning || heroBusy;
  if (card) card.classList.toggle('is-running', active);
  if (heroStateInited && prevHeroRunning !== heroRunning && !immediate) {
    const clover = document.querySelector('.clover-badge');
    if (clover) {
      clover.classList.remove('spin-pop');
      void clover.offsetWidth;
      clover.classList.add('spin-pop');
      setTimeout(() => clover.classList.remove('spin-pop'), 560);
    }
  }
  heroStateInited = true;
  prevHeroRunning = heroRunning;

  if (heroBusy) {
    btn.className = 'hero-toggle busy';
    btn.title = '正在执行操作…';
    morphFabAmp(FAB_TARGET_AMP, immediate);
    return;
  }
  btn.className = 'hero-toggle ' + (heroRunning ? 'stop' : 'play');
  btn.title = heroRunning ? '服务正在运行 · 点击全部停止' : '服务已停止 · 点击全部启动';
  morphFabAmp(heroRunning ? FAB_TARGET_AMP : 0, immediate);
}

function updatePortLabels(ports, appName, companionName) {
  const p = ports || {};
  const bPort = p.bridge || 3100;
  const dPort = p.dsh || 3080;
  const slPort = p.snowluma || 3000;
  if ($('port-bridge-sub')) $('port-bridge-sub').innerHTML = `127.0.0.1:${bPort} · 控制台 <span class="ms inline-ms">open_in_new</span>`;
  if ($('port-dsh-sub')) $('port-dsh-sub').innerHTML = `127.0.0.1:${dPort} · Web GUI <span class="ms inline-ms">open_in_new</span>`;
  if ($('port-sl-sub')) $('port-sl-sub').innerHTML = `127.0.0.1:${slPort} · QQ 网关 <span class="ms inline-ms">open_in_new</span>`;

  if (appName) {
    setText('doc-title', appName);
    setText('brand-text', appName);
    if ($('brand-mark')) $('brand-mark').title = appName;
    setText('about-app-title', `${appName} · 通用伴聊状态与服务面板`);
  }
  if (companionName) {
    setText('hero-name', companionName);
  }
  const ad = $('about-desc');
  if (ad && p.bridge) {
    ad.innerHTML = `仅以只读方式读取本地状态文件（<code>mode.json</code> / <code>night-rhythm.json</code> / <code>rest.json</code> / <code>token-usage.jsonl</code>）并探测本机服务端口（${bPort} / ${dPort} / ${slPort}），全程不经过模型，也不会写入或改动记忆与对话上下文。`;
  }
}

function render(s) {
  if (!s) return;
  if (s.systemAccent && /^#[0-9a-f]{6}$/i.test(s.systemAccent) && s.systemAccent.toLowerCase() !== systemAccentHex.toLowerCase()) {
    systemAccentHex = s.systemAccent;
    if (seedChoice === 'system') applyMaterialPalette();
    else if ($('sys-accent-dot')) $('sys-accent-dot').style.background = systemAccentHex;
  }

  updatePortLabels(s.ports, s.meta?.appName, s.meta?.companionName);

  const effFeatures = MINIMAL_DEMO
    ? { comfyui: false, tts: false, sleep: false, backup: false }
    : (s.features || lastFeatures);
  const ttsUp = !!s.tts?.up;
  applyFeatureVisibility(effFeatures, ttsUp);

  $('setup-banner')?.classList.toggle('hidden', DEMO || !HAS_HOST || !s.meta || s.meta.configured !== false);

  const upBridge = demoToggleOverride !== null ? demoToggleOverride : !!s.bridge?.up;
  heroRunning = upBridge;
  syncHeroBtn();

  if (upBridge) {
    if (s.bridgeStartMs) bridgeStartMs = s.bridgeStartMs;
    else if (s.bridgeUptimeMin != null && !bridgeStartMs) bridgeStartMs = Date.now() - s.bridgeUptimeMin * 60000;
  } else {
    bridgeStartMs = null;
  }
  tickUptime();

  const asleep = effFeatures.sleep && s.sleep?.asleep === true;
  const napping = effFeatures.sleep && s.sleep?.napping === true;
  const recentTalk = s.activityAgoMin != null && s.activityAgoMin < 15;

  const pillStateText = !upBridge ? '已停止' : napping ? '运行中 · 小睡' : asleep ? '运行中 · 在睡' : '运行中';
  const topStateText = !upBridge ? '服务离线' : napping ? '小睡中' : asleep ? '在睡' : recentTalk ? '在线 · 刚说过话' : '在线';
  const dotType = !upBridge ? 'red' : asleep || napping ? 'gray' : 'green';
  setDot($('hero-dot'), dotType);
  setDot($('tb-pill-dot'), dotType);
  setDot($('rail-dot'), dotType);

  const heroPill = $('hero-state-pill');
  if (heroPill) {
    heroPill.classList.toggle('running', upBridge);
    heroPill.classList.toggle('stopped', !upBridge);
  }
  setText('hero-state', pillStateText);
  setText('tb-pill-text', (DEMO ? '演示 · ' : '') + topStateText + (s.mode && s.mode !== 'unknown' && s.mode !== 'unconfigured' ? ' · ' + s.mode : ''));
  setText('rail-status-text', topStateText);
  if ($('rail-status')) $('rail-status').dataset.tip = '当前：' + topStateText;

  setText('hero-mode', s.mode && s.mode !== 'unknown' && s.mode !== 'unconfigured' ? s.mode : (s.meta?.configured === false ? '未配置' : '未知模式'));
  const talkStr = s.lastTalkAgoMin != null ? (s.lastTalkAgoMin < 1 ? '刚刚' : fmtDur(s.lastTalkAgoMin) + '前') : '—';
  const beatStr = s.bridge?.beatAgoSec != null ? Math.round(s.bridge.beatAgoSec) + ' 秒前' : '—';
  setText('hero-talk-chip', recentTalk ? '刚说过话' : ('对话 ' + talkStr));
  setText('hero-sub', `${beatStr} · ${talkStr}`);
  setText('k-talk-meta', `上次对话 ${talkStr}`);

  // VRAM Telemetry
  comfyUp = !!s.comfy?.up;
  const qCount = s.comfy?.queue ?? 0;
  if (effFeatures.comfyui) {
    const vUsed = s.comfy?.vramUsedGB;
    const vTot = s.comfy?.vramTotalGB;
    tweenNumber('stat-vram', vUsed, (v) => v.toFixed(1));
    setText('vram-sub', vTot ? `共 ${vTot} GB` : comfyUp ? '已连接' : '未启动');
    const vPct = vTot ? Math.min(100, (vUsed / vTot) * 100) : 0;
    if ($('bar-vram')) $('bar-vram').style.width = vPct + '%';
    pushHist(vramHist, vUsed != null ? vUsed : 0);
    drawSpark(vramHist, 'vram-line', 'vram-area', 180, 38, vTot || 8);
    setText('comfy-foot', effFeatures.tts ? `队列 ${qCount} · TTS ${ttsUp ? '在线' : '离线'}` : `队列 ${qCount}`);
  }
  setText('tts-line', `TTS ${ttsUp ? '在线' : '离线'}`);

  // Sleep Card
  if (effFeatures.sleep && s.sleep) {
    setText('sleep-bed', fmtTime(s.sleep.bedAt));
    setText('sleep-wake', fmtTime(s.sleep.wakeAt));
    const bedMs = s.sleep.bedAt || 0;
    const baseBedMs = s.sleep.baseBedAt || bedMs;
    const nowMs = Date.now();
    const stillIn = !s.sleep.wakeAt || nowMs < s.sleep.wakeAt;
    const over = !asleep && !napping && baseBedMs && nowMs > baseBedMs && stillIn;
    const napRemain = napping && s.sleep.napUntil ? Math.max(0, Math.floor((s.sleep.napUntil - nowMs) / 60000)) : null;
    const nap = s.nap || {};
    const showNap = !asleep && !over && s.sleep.bedSource !== 'plan' && nap.inWindow;

    const sc = $('sleep-count');
    const srcChip = $('sleep-src');
    if (showNap) {
      setText('sleep-cap', '小睡概率');
      setText('sleep-src', `${nap.startHour}:00-${nap.endHour}:00`);
      setText('sleep-count', (nap.chancePct || 0) + '%');
      if (sc) sc.classList.remove('over');
      if (srcChip) srcChip.classList.remove('over');
      if ($('bar-sleep')) $('bar-sleep').style.width = Math.min(100, nap.chancePct || 0) + '%';
    } else {
      setText('sleep-cap', napping ? '小睡结束' : asleep ? '距离起床' : over ? '已熬夜' : '距离睡觉');
      setText('sleep-src', over ? '已过入睡点' : (s.sleep.bedSource === 'plan' ? '今晚计划' : '基准估时'));
      if (sc) sc.classList.toggle('over', !!over);
      if (srcChip) srcChip.classList.toggle('over', !!over);
      const val = napping ? (napRemain == null ? '—' : fmtDur(napRemain)) : over ? (Math.floor((nowMs - baseBedMs) / 60000) + 'm') : fmtDur(s.sleep.remainMin);
      setText('sleep-count', val);
      let sleepPct = 20;
      if (s.sleep.bedAt && s.sleep.wakeAt && asleep) {
        const span = Math.max(1, s.sleep.wakeAt - s.sleep.bedAt);
        sleepPct = Math.max(5, Math.min(100, ((nowMs - s.sleep.bedAt) / span) * 100));
      } else if (!asleep && s.sleep.remainMin != null) {
        sleepPct = Math.max(8, Math.min(100, 100 - (s.sleep.remainMin / (16 * 60)) * 100));
      }
      if ($('bar-sleep')) $('bar-sleep').style.width = sleepPct + '%';
    }

    const bell = document.querySelector('.bell-btn');
    if (bell) {
      bell.classList.toggle('idle', !asleep);
      bell.disabled = !asleep;
      bell.title = asleep ? '叫醒（立刻检查未读）' : '醒着，不用叫';
    }
  } else if (effFeatures.sleep) {
    setText('sleep-bed', '—');
    setText('sleep-wake', '—');
    setText('sleep-count', '—');
    setText('sleep-src', '暂无数据');
  }

  // Service Nodes
  setText('m-bridge', upBridge ? '在线' : '离线');
  setDot($('m-dot-bridge'), upBridge ? 'green' : 'red');

  setText('m-dsh', s.dsh?.up ? '在线' : '离线');
  setDot($('m-dot-dsh'), s.dsh?.up ? 'green' : 'red');

  setText('m-sl', s.snowluma?.up ? '在线' : '离线');
  setDot($('m-dot-sl'), s.snowluma?.up ? 'green' : 'red');

  if (effFeatures.comfyui) {
    setText('m-comfy', comfyUp ? '运行中' : '已休眠');
    setDot($('m-dot-comfy'), comfyUp ? 'green' : 'gray');
    setText('comfy-sub', `队列 ${qCount}`);
    if ($('comfy-switch')) $('comfy-switch').classList.toggle('on', comfyUp);
  }

  // Metrics
  tweenNumber('k-turns', s.todayTurns ?? 0, (v) => Math.round(v) + ' 轮');
  if (effFeatures.comfyui) setText('k-draws', (s.todayDraws ?? 0) + ' 张');
  if (effFeatures.sleep) setText('k-debt', fmtDur(s.sleep?.debtMin ?? 0));
  tweenNumber('k-mem', s.memMB, (v) => v.toFixed(1) + ' MB');
  setText('k-beat', beatStr);
  setDot($('hb-dot'), upBridge ? ((s.bridge?.beatAgoSec ?? 0) > 120 ? 'warn' : 'green') : 'red');
  pushHist(memHist, s.memMB);
  drawSpark(memHist, 'mem-line', null, 64, 20, 0);
}

/* ==================== Navigation & Rail ==================== */
const PAGE_TITLES = { dash: '仪表盘', log: '日志', theme: '外观', settings: '环境配置', about: '关于' };
let activePage = 'dash';
let logPollTimer = null;

function syncRailIndicator() {
  const ind = $('rail-indicator');
  const activeBtn = document.querySelector('.rail-btn[data-page].active');
  if (!ind || !activeBtn) return;
  ind.style.setProperty('--ind-y', activeBtn.offsetTop + 'px');
}

function syncSegThumb(groupId, immediate = false) {
  const grp = $(groupId);
  if (!grp) return;
  const thumb = grp.querySelector('.seg-thumb');
  const active = grp.querySelector('.seg.active');
  if (!thumb || !active || active.offsetWidth === 0) return;
  if (immediate) thumb.style.transition = 'none';
  thumb.style.width = active.offsetWidth + 'px';
  thumb.style.transform = `translateX(${active.offsetLeft}px)`;
  if (immediate) {
    void thumb.offsetWidth;
    thumb.style.transition = '';
  }
}

function switchPage(page) {
  if (!PAGE_TITLES[page]) page = 'dash';
  const changed = activePage !== page;
  activePage = page;
  document.querySelectorAll('.rail-btn[data-page]').forEach((b) => {
    b.classList.toggle('active', b.dataset.page === page);
  });
  syncRailIndicator();

  Object.keys(PAGE_TITLES).forEach((p) => {
    const sec = $('page-' + p);
    if (sec) sec.classList.toggle('active', p === page);
  });
  const titleEl = $('page-title');
  if (titleEl) {
    titleEl.textContent = PAGE_TITLES[page];
    if (changed) {
      titleEl.classList.remove('title-swap');
      void titleEl.offsetWidth;
      titleEl.classList.add('title-swap');
    }
  }
  $('main-scroll')?.scrollTo({ top: 0, behavior: 'auto' });

  if (page === 'log') {
    syncSegThumb('log-filters', true);
    if (HAS_HOST && !DEMO) post({ action: 'log' });
    else renderLogs(DEMO_LOG_TEXT);
    syncLogPolling();
  } else {
    stopLogPolling();
  }
  if (page === 'theme') {
    syncSegThumb('theme-seg', true);
  }
  if (page === 'settings') {
    ['mod-comfyui', 'mod-sleep', 'mod-tts', 'mod-backup'].forEach((id) => syncSegThumb(id, true));
    if (HAS_HOST) post({ action: 'getConfig' });
  }
}

document.querySelectorAll('.rail-btn[data-page]').forEach((b) => {
  b.addEventListener('click', () => switchPage(b.dataset.page));
});

const rail = $('rail');
const railToggle = $('rail-toggle');
function setRailExpanded(open) {
  if (!rail) return;
  rail.classList.toggle('expanded', !!open);
  if (railToggle) railToggle.dataset.tip = open ? '收起侧栏' : '展开侧栏';
  if ($('tip')) $('tip').style.display = 'none';
  syncRailIndicator();
}
setRailExpanded(railDefaultOpen);

railToggle?.addEventListener('click', () => {
  const next = !rail.classList.contains('expanded');
  setRailExpanded(next);
  post({ action: 'railWidth', width: next ? 208 : 72 });
});

// Rail Hover Tooltip (only when collapsed)
const tip = $('tip');
document.addEventListener('mouseover', (e) => {
  const el = e.target.closest?.('[data-tip]');
  if (!el || !tip) { if (tip) tip.style.display = 'none'; return; }
  if (rail?.classList.contains('expanded') && el.closest('#rail') && !el.classList.contains('rail-toggle')) {
    tip.style.display = 'none';
    return;
  }
  const r = el.getBoundingClientRect();
  tip.textContent = el.dataset.tip;
  tip.style.display = 'block';
  tip.style.top = (r.top + r.height / 2 - 13) + 'px';
  tip.style.left = (r.right + 10) + 'px';
});
document.addEventListener('mouseout', (e) => {
  if (e.target.closest?.('[data-tip]') && tip) tip.style.display = 'none';
});

$('main-scroll')?.addEventListener('scroll', (e) => {
  document.querySelector('.titlebar')?.classList.toggle('scrolled', e.target.scrollTop > 4);
});

/* ==================== Structured Log Viewer ==================== */
let rawLogText = '';
let logFilter = 'all';
let logAutoRefresh = false;
let logFollow = true;

function parseLogLine(line) {
  const m = line.match(/^(\d{2}:\d{2}:\d{2}(?:\.\d+)?)\s+(.*)$/);
  const ts = m ? m[1] : '';
  let rest = m ? m[2] : line;
  const tags = [];
  while (rest.startsWith('[')) {
    const end = rest.indexOf(']');
    if (end <= 1 || end > 28) break;
    tags.push(rest.slice(1, end));
    rest = rest.slice(end + 1).trimStart();
  }
  const low = line.toLowerCase();
  const isWarn = /失败|异常|error|err|warn|超限|阻塞|超时|断开/.test(low);
  const isUnread = /unread|wake|唤醒|未读|merge|合并/.test(low);
  const isChat = /reserved2|发送|role|注入|reply|引用/.test(low);
  return { ts, tags, msg: rest, isWarn, isUnread, isChat, raw: line };
}

function renderLogs(text) {
  rawLogText = text || '';
  const box = $('log-box');
  if (!box) return;
  const q = ($('log-search')?.value || '').trim().toLowerCase();
  const lines = rawLogText.split(/\r?\n/).filter((l) => l.trim().length > 0);
  const items = lines.map(parseLogLine).filter((item) => {
    if (logFilter === 'warn' && !item.isWarn) return false;
    if (logFilter === 'unread' && !item.isUnread) return false;
    if (logFilter === 'chat' && !item.isChat) return false;
    if (q && !item.raw.toLowerCase().includes(q)) return false;
    return true;
  });

  setText('log-count', items.length + ' 行');
  if (!items.length) {
    box.innerHTML = '<div class="log-empty">没有匹配的日志记录</div>';
    return;
  }

  const frag = document.createDocumentFragment();
  for (const it of items) {
    const row = document.createElement('div');
    row.className = 'log-row';
    if (it.ts) {
      const s = document.createElement('span');
      s.className = 'log-ts';
      s.textContent = it.ts;
      row.appendChild(s);
    }
    for (const tg of it.tags.slice(0, 3)) {
      const span = document.createElement('span');
      const tLow = tg.toLowerCase();
      const cls = /warn|err|fail/.test(tLow) ? 'tag-warn'
        : /unread|merge|wake/.test(tLow) ? 'tag-unread'
        : /reserved|role|reply/.test(tLow) ? 'tag-chat' : '';
      span.className = 'log-tag ' + cls;
      span.textContent = tg;
      row.appendChild(span);
    }
    const msg = document.createElement('span');
    msg.className = 'log-msg';
    msg.textContent = it.msg;
    row.appendChild(msg);
    frag.appendChild(row);
  }
  box.textContent = '';
  box.appendChild(frag);
  if (logFollow) box.scrollTop = box.scrollHeight;
}

function stopLogPolling() {
  if (logPollTimer) { clearInterval(logPollTimer); logPollTimer = null; }
}
function syncLogPolling() {
  stopLogPolling();
  if (logAutoRefresh && activePage === 'log' && HAS_HOST && !DEMO) {
    logPollTimer = setInterval(() => post({ action: 'log' }), 3000);
  }
}

$('log-search')?.addEventListener('input', () => renderLogs(rawLogText));
document.querySelectorAll('#log-filters .seg').forEach((b) => {
  b.addEventListener('click', () => {
    logFilter = b.dataset.filter || 'all';
    document.querySelectorAll('#log-filters .seg').forEach((x) => x.classList.toggle('active', x === b));
    syncSegThumb('log-filters');
    renderLogs(rawLogText);
  });
});
$('log-auto')?.closest('.switch-chip')?.addEventListener('click', () => {
  logAutoRefresh = !logAutoRefresh;
  $('log-auto').classList.toggle('on', logAutoRefresh);
  syncLogPolling();
});
$('log-follow')?.closest('.switch-chip')?.addEventListener('click', () => {
  logFollow = !logFollow;
  $('log-follow').classList.toggle('on', logFollow);
  if (logFollow && $('log-box')) $('log-box').scrollTop = $('log-box').scrollHeight;
});
let copyResetTimer = null;
$('log-copy')?.addEventListener('click', async () => {
  try {
    await navigator.clipboard.writeText(rawLogText || '');
    const btn = $('log-copy');
    if (btn) {
      btn.innerHTML = '<span class="ms">check</span>已复制';
      clearTimeout(copyResetTimer);
      copyResetTimer = setTimeout(() => {
        btn.innerHTML = '<span class="ms">content_copy</span>复制';
      }, 1400);
    }
    toast('已复制当前日志到剪贴板');
  } catch (_) {
    toast('复制失败，请手动选中复制');
  }
});

/* ==================== Environment Settings & Auto-Sniffer ==================== */
const SOURCE_LABELS = {
  process: '运行进程',
  'bridge-config': 'bridge 配置',
  env: '环境变量',
  registry: '注册表',
  scan: '目录扫描',
  manual: '手动配置',
  missing: '未配置',
};
let currentConfig = null;
let currentReport = null;

function setModSeg(groupId, val) {
  const grp = $(groupId);
  if (!grp) return;
  grp.querySelectorAll('.seg').forEach((b) => b.classList.toggle('active', b.dataset.val === (val || 'auto')));
  syncSegThumb(groupId, activePage !== 'settings');
}

function getModSeg(groupId) {
  const active = document.querySelector(`#${groupId} .seg.active`);
  return active ? active.dataset.val : 'auto';
}

function renderConfig(cfg, rep, demoFeatures) {
  if (!cfg) return;
  currentConfig = cfg;
  currentReport = rep || currentReport;

  const textFields = [
    'bridgeDir', 'dshHome', 'snowlumaDir', 'qqExe', 'nodeExe',
    'backupScript', 'ownerQQ', 'companionName', 'appName', 'guardianTaskName'
  ];
  for (const key of textFields) {
    const inp = $('cfg-' + key);
    if (inp) inp.value = cfg[key] ?? '';
  }

  const p = cfg.ports || {};
  for (const pk of ['bridge', 'dsh', 'snowluma', 'snowlumaWeb', 'comfyui', 'tts']) {
    const inp = $('port-' + pk);
    if (inp && p[pk] != null) inp.value = p[pk];
  }

  const f = cfg.features || {};
  setModSeg('mod-comfyui', f.comfyui || 'auto');
  setModSeg('mod-sleep', f.sleep || 'auto');
  setModSeg('mod-tts', f.tts || 'auto');
  setModSeg('mod-backup', f.backup || 'auto');

  const fields = rep?.fields || {};
  for (const fk of ['bridgeDir', 'dshHome', 'snowlumaDir', 'qqExe', 'nodeExe', 'backupScript', 'ownerQQ']) {
    const badge = $('badge-' + fk);
    const info = fields[fk];
    if (!badge) continue;
    if (info) {
      badge.className = 'cfg-badge ' + (info.found ? 'ok' : 'warn');
      badge.textContent = info.found ? `已识别 · ${SOURCE_LABELS[info.source] || info.source}` : '未找到';
      badge.title = info.detail || '';
    }
  }

  // Clickable DSH pack chips
  const chipsWrap = $('dsh-pack-chips');
  if (chipsWrap) {
    const cands = rep?.dshPackCandidates || [];
    chipsWrap.textContent = '';
    if (cands.length > 0) {
      chipsWrap.classList.remove('hidden');
      for (const pathStr of cands) {
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'pack-chip' + (pathStr.toLowerCase() === (cfg.dshHome || '').toLowerCase() ? ' active' : '');
        const parts = pathStr.replace(/[\\/]+$/, '').split(/[\\/]/);
        btn.textContent = parts[parts.length - 1] || pathStr;
        btn.title = pathStr;
        btn.addEventListener('click', () => {
          if ($('cfg-dshHome')) $('cfg-dshHome').value = pathStr;
          chipsWrap.querySelectorAll('.pack-chip').forEach((c) => c.classList.toggle('active', c === btn));
        });
        chipsWrap.appendChild(btn);
      }
    } else {
      chipsWrap.classList.add('hidden');
    }
  }

  if (DEMO && demoFeatures) {
    DEMO_STATUS.features = demoFeatures;
    DEMO_STATUS.ports = cfg.ports || DEMO_STATUS.ports;
    DEMO_STATUS.meta = {
      appName: cfg.appName || '鲸醒',
      companionName: cfg.companionName || 'AI 伙伴',
      configured: true,
    };
    render(DEMO_STATUS);
  }
}

function collectConfigFromForm() {
  const num = (id, def) => {
    const v = Number($(id)?.value || def);
    return Number.isFinite(v) && v >= 1 && v <= 65535 ? Math.round(v) : def;
  };
  return {
    appName: ($('cfg-appName')?.value || '').trim() || '鲸醒',
    companionName: ($('cfg-companionName')?.value || '').trim() || 'AI 伙伴',
    bridgeDir: ($('cfg-bridgeDir')?.value || '').trim(),
    dshHome: ($('cfg-dshHome')?.value || '').trim(),
    snowlumaDir: ($('cfg-snowlumaDir')?.value || '').trim(),
    qqExe: ($('cfg-qqExe')?.value || '').trim(),
    nodeExe: ($('cfg-nodeExe')?.value || '').trim(),
    ownerQQ: ($('cfg-ownerQQ')?.value || '').trim(),
    backupScript: ($('cfg-backupScript')?.value || '').trim(),
    guardianTaskName: ($('cfg-guardianTaskName')?.value || '').trim(),
    ports: {
      bridge: num('port-bridge', 3100),
      dsh: num('port-dsh', 3080),
      snowluma: num('port-snowluma', 3000),
      snowlumaWeb: num('port-snowlumaWeb', 5099),
      comfyui: num('port-comfyui', 8188),
      tts: num('port-tts', 7860),
    },
    features: {
      comfyui: getModSeg('mod-comfyui'),
      sleep: getModSeg('mod-sleep'),
      tts: getModSeg('mod-tts'),
      backup: getModSeg('mod-backup'),
    },
  };
}

document.querySelectorAll('.mod-seg .seg').forEach((b) => {
  b.addEventListener('click', () => {
    const grp = b.closest('.mod-seg');
    if (!grp) return;
    grp.querySelectorAll('.seg').forEach((x) => x.classList.toggle('active', x === b));
    syncSegThumb(grp.id);
  });
});

$('btn-redetect')?.addEventListener('click', () => {
  if (HAS_HOST) {
    post({ action: 'redetectConfig' });
  } else {
    renderConfig(DEMO_CONFIG, DEMO_REPORT, DEMO_STATUS.features);
    toast('演示预览：已模拟完成环境嗅探');
  }
});

$('btn-save-cfg')?.addEventListener('click', () => {
  const cfg = collectConfigFromForm();
  if (HAS_HOST) {
    post({ action: 'saveConfig', config: cfg });
  } else {
    const resolve = (mode, def) => (mode === 'show' ? true : mode === 'hide' ? false : def);
    const resolved = {
      comfyui: resolve(cfg.features.comfyui, !MINIMAL_DEMO),
      sleep: resolve(cfg.features.sleep, !MINIMAL_DEMO),
      tts: resolve(cfg.features.tts, !MINIMAL_DEMO),
      backup: resolve(cfg.features.backup, !!cfg.backupScript),
    };
    renderConfig(cfg, DEMO_REPORT, resolved);
    toast('演示预览：配置已应用（纯页面模式不写磁盘）');
  }
});

document.querySelectorAll('[data-browse-folder]').forEach((btn) => {
  btn.addEventListener('click', () => {
    const field = btn.dataset.browseFolder;
    const initial = $('cfg-' + field)?.value || '';
    if (HAS_HOST) post({ action: 'browseFolder', field, initial });
    else toast('桌面客户端中点击可打开文件夹选择框');
  });
});

document.querySelectorAll('[data-browse-file]').forEach((btn) => {
  btn.addEventListener('click', () => {
    const field = btn.dataset.browseFile;
    const initial = $('cfg-' + field)?.value || '';
    const filter = field.endsWith('Exe') ? '可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*' : '脚本文件 (*.mjs;*.js;*.ps1;*.bat;*.cmd)|*.mjs;*.js;*.ps1;*.bat;*.cmd|所有文件 (*.*)|*.*';
    if (HAS_HOST) post({ action: 'browseFile', field, initial, filter });
    else toast('桌面客户端中点击可打开文件选择框');
  });
});

/* ==================== Progress & Actions ==================== */
function showProgress(text, pct) {
  const box = $('prog-box');
  const bar = $('prog-bar');
  if (!box || !bar) return;
  const p = Math.max(4, Math.min(100, pct || 0));
  heroBusy = p < 100;
  syncHeroBtn();
  box.style.display = 'block';
  setText('prog-text', text || '执行中…');
  setText('prog-pct', p + '%');
  bar.style.width = p + '%';
  if (p >= 100) {
    setTimeout(() => {
      box.style.display = 'none';
      bar.style.width = '0%';
      heroBusy = false;
      syncHeroBtn();
    }, 1200);
  }
}

let demoProgTimers = [];
function runDemoProgress(targetRunning, title) {
  demoProgTimers.forEach(clearTimeout);
  demoProgTimers = [];
  const steps = [
    [0, `${title}：准备阶段…`, 18],
    [450, `${title}：模拟调度中…`, 52],
    [950, `${title}：模拟状态收敛…`, 84],
    [1450, `${title}完成（未触碰真实服务）`, 100]
  ];
  steps.forEach(([delay, msg, pct]) => {
    demoProgTimers.push(setTimeout(() => {
      showProgress(msg, pct);
      if (pct >= 100) {
        demoToggleOverride = targetRunning;
        DEMO_STATUS.bridgeStartMs = targetRunning ? Date.now() - (5 * 3600 + 15 * 60 + 18) * 1000 : null;
        render(DEMO_STATUS);
        toast(targetRunning ? '测试版：已模拟进入运行状态' : '测试版：已模拟进入停止状态');
      }
    }, delay));
  });
}

document.querySelectorAll('[data-act]').forEach((b) => {
  b.addEventListener('click', () => {
    const act = b.dataset.act;
    if (!act) return;
    if (DEMO) {
      if (['min', 'max', 'close', 'refresh'].includes(act)) {
        if (act === 'refresh') {
          render(DEMO_STATUS);
          toast('测试版：已刷新演示视图');
        }
        post({ action: act });
        return;
      }
      if (act === 'wake') {
        b.classList.remove('ringing');
        void b.offsetWidth;
        b.classList.add('ringing');
        setTimeout(() => b.classList.remove('ringing'), 700);
        toast('测试版：已触发模拟叫醒震铃（点睡眠卡空白处可切换睡/醒）');
        return;
      }
      if (act === 'restartAll') { runDemoProgress(true, '模拟重启服务'); return; }
      if (act === 'stopAll') { runDemoProgress(false, '模拟停止服务'); return; }
      if (act === 'comfyStart' || act === 'comfyStop') {
        const nextUp = act === 'comfyStart';
        DEMO_STATUS.comfy.up = nextUp;
        DEMO_STATUS.comfy.vramUsedGB = nextUp ? 2.6 : null;
        render(DEMO_STATUS);
        toast(`测试版：已模拟${nextUp ? '启动' : '停止'} ComfyUI（未执行真实进程）`);
        return;
      }
      if (act === 'log') {
        renderLogs(DEMO_LOG_TEXT);
        toast('测试版：已刷新模拟日志');
        return;
      }
      toast('测试版：仅演示界面交互，未执行真实功能');
      return;
    }
    if (!HAS_HOST && act === 'log') { renderLogs(DEMO_LOG_TEXT); toast('已刷新演示日志'); return; }
    if (!HAS_HOST && act === 'refresh') { toast('已刷新状态'); return; }
    post({ action: act });
  });
});

$('btn-refresh')?.addEventListener('click', () => {
  const b = $('btn-refresh');
  if (b) {
    b.classList.remove('spinning');
    void b.offsetWidth;
    b.classList.add('spinning');
    setTimeout(() => b.classList.remove('spinning'), 560);
  }
});

$('sleep-card')?.addEventListener('click', (e) => {
  if (!DEMO || e.target.closest('.bell-btn')) return;
  DEMO_STATUS.sleep.asleep = !DEMO_STATUS.sleep.asleep;
  render(DEMO_STATUS);
  toast(DEMO_STATUS.sleep.asleep ? '测试版：已切换为「在睡」（可点右上角小铃铛）' : '测试版：已切换为「醒着」');
});

$('btn-palette')?.addEventListener('click', () => switchPage('theme'));
$('btn-goto-log')?.addEventListener('click', () => switchPage('log'));
$('btn-goto-settings')?.addEventListener('click', () => switchPage('settings'));

$('comfy-card')?.addEventListener('click', () => {
  if (!lastFeatures.comfyui) return;
  if (DEMO) {
    const nextUp = !DEMO_STATUS.comfy.up;
    DEMO_STATUS.comfy.up = nextUp;
    DEMO_STATUS.comfy.vramUsedGB = nextUp ? 2.6 : null;
    render(DEMO_STATUS);
    toast(`测试版：已模拟${nextUp ? '开启' : '关闭'} ComfyUI 开关`);
    return;
  }
  post({ action: comfyUp ? 'comfyStop' : 'comfyStart' });
});

$('hero-toggle')?.addEventListener('click', () => {
  if (heroBusy) return;
  if (DEMO) {
    const next = !(demoToggleOverride !== null ? demoToggleOverride : heroRunning);
    demoToggleOverride = next;
    DEMO_STATUS.bridgeStartMs = next ? Date.now() - (5 * 3600 + 15 * 60 + 18) * 1000 : null;
    render(DEMO_STATUS);
    toast(next ? '测试版：已启动花瓣波浪动效' : '测试版：已切换回停止态');
    return;
  }
  post({ action: heroRunning ? 'stopAll' : 'restartAll' });
});

/* ==================== Theme & Settings Bindings ==================== */
const THEME_CYCLE = ['auto', 'light', 'dark'];
$('theme-btn')?.addEventListener('click', () => {
  const next = THEME_CYCLE[(THEME_CYCLE.indexOf(themeMode) + 1) % THEME_CYCLE.length];
  setThemeWithRipple(next, $('theme-btn'));
});

document.querySelectorAll('#theme-seg .seg').forEach((b) => {
  b.addEventListener('click', () => setThemeWithRipple(b.dataset.themeMode, b));
});

document.querySelectorAll('#swatch-grid .swatch-card[data-seed]').forEach((c) => {
  c.addEventListener('click', () => {
    seedChoice = c.dataset.seed;
    localStorage.setItem('jx-seed', seedChoice);
    runViewRipple(c, applyMaterialPalette);
  });
});

$('seed-picker')?.addEventListener('input', (e) => {
  customSeedHex = e.target.value;
  seedChoice = customSeedHex;
  localStorage.setItem('jx-custom-seed', customSeedHex);
  localStorage.setItem('jx-seed', seedChoice);
  applyMaterialPalette();
});

$('opt-pure-black')?.addEventListener('click', (e) => {
  pureBlack = !pureBlack;
  localStorage.setItem('jx-pure-black', pureBlack ? '1' : '0');
  runViewRipple($('sw-pure-black') || e.currentTarget, applyMaterialPalette);
});
$('opt-compact')?.addEventListener('click', () => {
  compactMode = !compactMode;
  localStorage.setItem('jx-compact', compactMode ? '1' : '0');
  applyMaterialPalette();
});
$('opt-rail-expand')?.addEventListener('click', () => {
  railDefaultOpen = !railDefaultOpen;
  localStorage.setItem('jx-rail-open', railDefaultOpen ? '1' : '0');
  setRailExpanded(railDefaultOpen);
  applyMaterialPalette();
});

/* ==================== WebView2 Messages ==================== */
window.chrome?.webview?.addEventListener('message', (e) => {
  const m = e.data || {};
  if (m.type === 'status') {
    render(m.data || {});
  } else if (m.type === 'config') {
    renderConfig(m.data, m.report, m.features);
  } else if (m.type === 'pickedPath' && m.field && m.path) {
    const inp = $('cfg-' + m.field);
    if (inp) inp.value = m.path;
  } else if (m.type === 'hostAccent' && m.systemAccent) {
    systemAccentHex = m.systemAccent;
    applyMaterialPalette();
  } else if (m.type === 'windowState') {
    const ic = $('max-icon');
    if (ic) ic.textContent = m.maximized ? 'filter_none' : 'crop_square';
  } else if (m.type === 'progress') {
    showProgress(m.text, m.pct);
  } else if (m.type === 'result') {
    if (m.action === 'log') { renderLogs(m.text || ''); return; }
    if (m.action === 'refresh' || m.action === 'railWidth' || m.action === 'theme') return;
    const txt = String(m.text || '').trim();
    if (!txt || txt === 'ok') return;
    toast(txt.slice(0, 80));
    if (['restartBridge', 'restartAll', 'stopAll', 'wake', 'comfyStart', 'comfyStop', 'backup'].includes(m.action)) {
      setText('action-result', txt.slice(0, 160));
      setTimeout(() => setText('action-result', ''), 6000);
    }
  }
});

/* ==================== MD3 Ink Ripple & Edge Resize ==================== */
document.addEventListener('pointerdown', (e) => {
  const host = e.target.closest?.('.act-btn, .pill-btn, .card.clickable, .rail-btn, .swatch-card');
  if (!host || e.button !== 0) return;
  const r = host.getBoundingClientRect();
  const sz = Math.max(r.width, r.height);
  const ink = document.createElement('span');
  ink.className = 'md-ripple-ink';
  ink.style.width = ink.style.height = sz + 'px';
  ink.style.left = (e.clientX - r.left - sz / 2) + 'px';
  ink.style.top = (e.clientY - r.top - sz / 2) + 'px';
  host.appendChild(ink);
  setTimeout(() => ink.remove(), 550);
});

document.addEventListener('mousedown', (e) => {
  if (e.button !== 0) return;
  const m = 6;
  const w = window.innerWidth, h = window.innerHeight;
  const x = e.clientX, y = e.clientY;
  const inTitle = !!e.target.closest?.('.titlebar, .rail-head');
  const l = x <= m, r = x >= w - m, b = y >= h - m;
  const t = !inTitle && y <= m;
  if (!(l || r || t || b)) return;
  const edge = (t ? 't' : '') + (b ? 'b' : '') + (l ? 'l' : '') + (r ? 'r' : '') || 'br';
  e.preventDefault();
  post({ action: 'resizeStart', edge });
});

/* ==================== Keyboard Shortcuts & Live Theme Sync ==================== */
document.addEventListener('keydown', (e) => {
  if (e.ctrlKey && ['1', '2', '3', '4', '5'].includes(e.key)) {
    e.preventDefault();
    switchPage(['dash', 'log', 'theme', 'settings', 'about'][Number(e.key) - 1]);
  } else if (e.key === 'F5' || (e.ctrlKey && e.key.toLowerCase() === 'r')) {
    e.preventDefault();
    post({ action: 'refresh' });
    toast('已刷新状态');
  } else if (e.key === 'Escape' && $('log-search') === document.activeElement) {
    $('log-search').value = '';
    renderLogs(rawLogText);
  }
});

try {
  window.matchMedia('(prefers-color-scheme: light)').addEventListener('change', () => {
    if (themeMode === 'auto') applyMaterialPalette();
  });
} catch (_) {}

/* ==================== Demo Data & Init ==================== */
const DEMO_STATUS = {
  ts: Date.now(),
  systemAccent: '#6669cc',
  meta: { appName: '鲸醒', companionName: 'AI 伙伴', configured: true },
  ports: { bridge: 3100, dsh: 3080, snowluma: 3000, snowlumaWeb: 5099, tts: 7860, comfyui: 8188 },
  features: MINIMAL_DEMO
    ? { comfyui: false, tts: false, sleep: false, backup: false }
    : { comfyui: true, tts: true, sleep: true, backup: true },
  bridge: { up: true, port: 3100, beatAgoSec: 18 },
  dsh: { up: true, port: 3080 },
  snowluma: { up: true, port: 3000 },
  tts: { up: true, port: 7860 },
  comfy: { up: true, port: 8188, vramUsedGB: 2.6, vramTotalGB: 8.0, queue: 1 },
  mode: 'reserved2',
  activityAgoMin: 2,
  sleep: {
    bedAt: new Date().setHours(25, 0, 0, 0),
    baseBedAt: new Date().setHours(25, 0, 0, 0),
    wakeAt: new Date().setHours(32, 42, 0, 0),
    asleep: false, napping: false, napUntil: 0,
    phase: 'awake', remainMin: 181,
    bedSource: 'plan', kind: 'normal', debtMin: 15
  },
  nap: { inWindow: false, chancePct: 0, debtThresholdMin: 60, startHour: 12, endHour: 17 },
  todayDraws: 3,
  bridgeUptimeMin: 315,
  bridgeStartMs: Date.now() - (5 * 3600 + 15 * 60 + 18) * 1000,
  lastTalkAgoMin: 2,
  todayTurns: 42,
  memMB: 10.4
};

const DEMO_CONFIG = {
  appName: '鲸醒',
  companionName: 'AI 伙伴',
  bridgeDir: 'D:\\AI_Companion\\qq-bridge',
  dshHome: 'C:\\Users\\Demo\\AppData\\Roaming\\dsh-launcher\\dsh-packs\\default',
  snowlumaDir: 'D:\\AI_Companion\\SnowLuma',
  qqExe: 'C:\\Program Files\\Tencent\\QQNT\\QQ.exe',
  nodeExe: 'C:\\Program Files\\nodejs\\node.exe',
  ownerQQ: '10000001',
  backupScript: '',
  guardianTaskName: '',
  ports: { bridge: 3100, dsh: 3080, snowluma: 3000, snowlumaWeb: 5099, tts: 7860, comfyui: 8188 },
  features: { comfyui: 'auto', tts: 'auto', sleep: 'auto', backup: 'auto' },
};

const DEMO_REPORT = {
  dshPackCandidates: [
    'C:\\Users\\Demo\\AppData\\Roaming\\dsh-launcher\\dsh-packs\\default',
    'C:\\Users\\Demo\\AppData\\Roaming\\dsh-launcher\\dsh-packs\\custom-role',
  ],
  fields: {
    bridgeDir: { found: true, source: 'process' },
    dshHome: { found: true, source: 'env' },
    snowlumaDir: { found: true, source: 'bridge-config' },
    qqExe: { found: true, source: 'registry' },
    nodeExe: { found: true, source: 'env' },
    backupScript: { found: false, source: 'missing' },
    ownerQQ: { found: true, source: 'bridge-config' },
  },
};

const DEMO_LOG_TEXT = [
  '22:43:10 [bridge] token 账本明细裁剪至最近 20000 条（仅修剪旧日志，不影响上下文）',
  '22:43:48 [bridge] [reserved2] 工具统一发送 private:10000001: 成功 2/2 条',
  '22:43:48 [bridge] [reserved2] 已安排防重复唤醒 private:10000001，30s 后检',
  '22:43:58 [bridge] [reserved2] private:10000001 思考中检测到新消息 private@seq4150，合并进当前轮',
  '22:44:13 [bridge] [unread] +seq=4155 游标=4155 队列=0',
  '22:44:16 [bridge] [merge] 状态同步 ok，已合并连续发言',
  '22:44:33 [bridge] [role] 注入角色卡 8922 字（限制 10500）',
  '22:45:02 [bridge] [warn] 示例告警：端口探活重连 1/2'
].join('\n');

initExpressiveShapes();
applyMaterialPalette();
syncRailIndicator();
if (DEMO || !HAS_HOST) {
  renderConfig(DEMO_CONFIG, DEMO_REPORT, DEMO_STATUS.features);
  render(DEMO_STATUS);
  renderLogs(DEMO_LOG_TEXT);
}
const initPage = params.get('page');
if (initPage && PAGE_TITLES[initPage]) switchPage(initPage);
if (typeof ResizeObserver !== 'undefined') {
  const segRO = new ResizeObserver((entries) => {
    for (const ent of entries) {
      if (ent.target.id) syncSegThumb(ent.target.id, true);
    }
  });
  ['theme-seg', 'log-filters', 'mod-comfyui', 'mod-sleep', 'mod-tts', 'mod-backup'].forEach((id) => {
    const el = $(id);
    if (el) segRO.observe(el);
  });
}
window.addEventListener('resize', () => {
  syncRailIndicator();
  if (activePage === 'log') syncSegThumb('log-filters', true);
  if (activePage === 'theme') syncSegThumb('theme-seg', true);
  if (activePage === 'settings') ['mod-comfyui', 'mod-sleep', 'mod-tts', 'mod-backup'].forEach((id) => syncSegThumb(id, true));
});
post({ action: 'refresh' });
