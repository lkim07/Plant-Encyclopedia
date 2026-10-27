// Theme check (design 2026-10-26): every text colour pair in both themes meets its WCAG contrast floor, and no
// component hard-codes a colour outside the allowlist. Run: npm run check:theme. Exits 1 on any failure.
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';

const root = new URL('..', import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1');
const styles = readFileSync(join(root, 'src/styles.css'), 'utf8');

/** The --color-* hex tokens of one CSS block, found by its selector. */
function tokens(selector) {
  const start = styles.indexOf(`${selector} {`);
  if (start < 0) throw new Error(`No "${selector}" block in styles.css`);
  const block = styles.slice(start, styles.indexOf('}', start));
  return Object.fromEntries(
    [...block.matchAll(/--color-([a-z-]+):\s*(#[0-9a-fA-F]{6})\b/g)].map((m) => [m[1], m[2]]),
  );
}

function luminance(hex) {
  const [r, g, b] = [1, 3, 5]
    .map((i) => parseInt(hex.slice(i, i + 2), 16) / 255)
    .map((v) => (v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4));
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

function contrast(a, b) {
  const [x, y] = [luminance(a), luminance(b)].sort((p, q) => q - p);
  return (x + 0.05) / (y + 0.05);
}

// [foreground, background, floor]: 4.5 for text, 3 for focus rings and icons.
const PAIRS = [
  ['text', 'bg', 4.5],
  ['text', 'surface', 4.5],
  ['text', 'surface-strong', 4.5],
  ['text-muted', 'bg', 4.5],
  ['text-muted', 'surface', 4.5],
  ['text-muted', 'surface-strong', 4.5],
  ['on-accent', 'accent', 4.5],
  ['accent', 'bg', 4.5],
  ['danger', 'bg', 4.5],
  ['danger', 'surface', 4.5],
  ['focus', 'bg', 3],
  ['focus', 'surface', 3],
  ['icon', 'bg', 3],
];

const light = tokens(':root');
const themes = { light, dark: { ...light, ...tokens(":root[data-theme='dark']") } };

let failed = false;
for (const [name, colors] of Object.entries(themes)) {
  for (const [fg, bg, floor] of PAIRS) {
    if (!colors[fg] || !colors[bg]) {
      console.log(`FAIL ${name}: --color-${fg} or --color-${bg} is missing`);
      failed = true;
      continue;
    }
    const ratio = contrast(colors[fg], colors[bg]);
    const ok = ratio >= floor;
    failed ||= !ok;
    console.log(
      `${ok ? 'ok  ' : 'FAIL'} ${name.padEnd(5)} ${fg.padEnd(10)} on ${bg.padEnd(14)} ${ratio.toFixed(2)} (≥ ${floor})`,
    );
  }
}

// Hard-coded colours in components: allowed only for masks, provider logos and fixed photo overlays.
const COLOR = /#[0-9a-fA-F]{3,8}\b|rgba?\(/;
const ALLOWED = [
  (file, line) =>
    /mask-image|-webkit-mask-image/.test(line) ||
    /^\s*(linear-gradient\(to (right|bottom), transparent, #000)/.test(line),
  (file) => file.endsWith('features/account/provider-buttons.ts'),
  // The browser chrome colour (meta theme-color) needs literal values, mirrored from styles.css.
  (file) => file.endsWith('core/theme/theme.ts'),
  (file, line) => line.includes('/* photo overlay: fixed */'),
];

function* files(dir) {
  for (const entry of readdirSync(dir)) {
    const path = join(dir, entry);
    if (statSync(path).isDirectory()) yield* files(path);
    else if (/\.(css|html|ts)$/.test(entry) && !entry.endsWith('.spec.ts')) yield path;
  }
}

for (const path of files(join(root, 'src/app'))) {
  const file = relative(root, path).replaceAll('\\', '/');
  readFileSync(path, 'utf8')
    .split('\n')
    .forEach((line, index) => {
      if (COLOR.test(line) && !ALLOWED.some((allowed) => allowed(file, line))) {
        console.log(`FAIL hard-coded colour: ${file}:${index + 1}: ${line.trim()}`);
        failed = true;
      }
    });
}

process.exit(failed ? 1 : 0);
