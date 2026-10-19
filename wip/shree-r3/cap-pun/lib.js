// Shared helpers for the Punjabi (Gurmukhi) capture: read a probe TSV, decode answers, keyboard files.
const fs = require('fs');
const path = require('path');
const REPO = 'C:/Users/harde/Desktop/Windows Font Software';
const DIR = __dirname;
const SD = 'C:\\Users\\harde\\AppData\\Local\\Temp\\claude\\C--USE~1\\767392~1\\SCRATC~1\\r3\\cap-pun\\'; // 8.3 form of DIR
const PUN = JSON.parse(fs.readFileSync(REPO + '/assets/config/shree-kbd/PUN.json', 'utf8').replace(/^\uFEFF/, ''));
// code (int) -> text: the importer's table, overridden by what this capture learned (codes.json, written by codes.js)
const CODES = {};
for (const [k, v] of Object.entries(PUN.codes)) CODES[parseInt(k, 16)] = v;
const LEARNED = path.join(DIR, 'codes.json');
if (fs.existsSync(LEARNED)) for (const [k, v] of Object.entries(JSON.parse(fs.readFileSync(LEARNED, 'utf8')).codes)) if (v != null) CODES[parseInt(k, 16)] = v;

const hex2 = n => n.toString(16).toUpperCase().padStart(2, '0');

function readTsv(file) {
  const rows = [];
  let load = null, loaded = null;
  for (const line of fs.readFileSync(file, 'utf8').split('\n')) {
    if (!line) continue;
    if (line.startsWith('#')) { const p = line.split('\t'); if (p[0] === '#stable') load = Number(p[1]); if (p[0] === '#loaded') loaded = p[1]; continue; }
    const [layer, key, uu, uh, lu, lh] = line.split('\t');
    const uUnits = Number(uu), lUnits = Number(lu);
    const u = (uUnits > 0 && uUnits < 64 && uh) ? uh.split(' ').map(h => String.fromCharCode(parseInt(h, 16))).join('') : null;
    const lb = (lUnits > 0 && lUnits < 64 && lh) ? lh.split(' ').map(h => parseInt(h, 16)) : null;
    rows.push({ layer: Number(layer), key: String.fromCharCode(parseInt(key, 16)), keyCode: parseInt(key, 16), uUnits, uHex: uh, u, lUnits, lb });
  }
  return { load, loaded, rows };
}

// Gurmukhi dependent signs, bindi/tippi/addak/nukta/virama/visarga/udaat/yakash
const SIGN = /[\u0A01-\u0A03\u0A3C\u0A3E-\u0A4D\u0A51\u0A70\u0A71\u0A75]/;
// The Unicode composer shows a lone sign on a carrier (a space or a '.').
function cleanU(u) {
  if (u == null) return null;
  u = u.replace(/\u0000+$/, '');
  if (/[\u0A00-\u0A7F]/.test(u)) {
    let t = u;
    if (t.length > 1 && (t[0] === ' ' || t[0] === '.') && SIGN.test(t[1])) t = t.slice(1);
    else if (t.length > 1 && t[0] === ' ') t = t.slice(1);
    return t;
  }
  if (u.length > 1 && /^[A-Za-z]+$/.test(u)) return null; // "Lk", "Hf" placeholders
  return u;
}

function readKbd(file) {
  const b = fs.readFileSync(file);
  const slots = [];
  for (let n = 0; n < 48; n++) {
    const o = 166 + n * 5;
    const codes = [];
    for (let j = 1; j <= 4; j++) if (b[o + j] !== 0xFF) codes.push(b[o + j]);
    slots.push(codes.reverse()); // stored last piece first -> typed order
  }
  return { bytes: b, slots };
}

const CTRL_SPACE_ORDER = '1234567890-=qwertyuiopasdfghjklzxcvbnm';
const uhex = s => s == null ? null : [...s].map(c => c.codePointAt(0).toString(16).toUpperCase().padStart(4, '0')).join(' ');

module.exports = { fs, path, REPO, DIR, SD, PUN, CODES, hex2, readTsv, cleanU, readKbd, CTRL_SPACE_ORDER, SIGN, uhex };
