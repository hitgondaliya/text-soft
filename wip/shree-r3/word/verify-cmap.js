// Independent check of step 1 (a second, separate TTF reader): (3,1) cmap of every face, same cmap per family,
// shared glyph ids, identical glyf outlines (raw contour data without hinting instructions), vs fam/<family>.json.
const fs = require("fs");
const path = require("path");
const root = __dirname;

function readTtf(file) {
  const b = fs.readFileSync(file);
  const n = b.readUInt16BE(4);
  const t = {};
  for (let i = 0; i < n; i++) {
    const o = 12 + i * 16;
    t[b.toString("latin1", o, o + 4)] = { off: b.readUInt32BE(o + 8), len: b.readUInt32BE(o + 12) };
  }
  const cm = t["cmap"].off;
  const nsub = b.readUInt16BE(cm + 2);
  let sub = -1;
  const subs = [];
  for (let i = 0; i < nsub; i++) {
    const pid = b.readUInt16BE(cm + 4 + i * 8), eid = b.readUInt16BE(cm + 6 + i * 8), off = b.readUInt32BE(cm + 8 + i * 8);
    subs.push(`${pid},${eid}/f${b.readUInt16BE(cm + off)}`);
    if (pid === 3 && eid === 1) sub = cm + off;
  }
  const map = new Map();
  if (sub >= 0) {
    const fmt = b.readUInt16BE(sub);
    if (fmt !== 4) throw new Error("fmt " + fmt);
    const segX2 = b.readUInt16BE(sub + 6);
    const ends = sub + 14, starts = ends + segX2 + 2, deltas = starts + segX2, ros = deltas + segX2;
    for (let s = 0; s < segX2 / 2; s++) {
      const e = b.readUInt16BE(ends + s * 2), st = b.readUInt16BE(starts + s * 2), d = b.readInt16BE(deltas + s * 2), ro = b.readUInt16BE(ros + s * 2);
      for (let c = st; c <= e && c !== 0xFFFF; c++) {
        let g;
        if (ro === 0) g = (c + d) & 0xFFFF;
        else {
          const ga = ros + s * 2 + ro + (c - st) * 2;
          g = b.readUInt16BE(ga);
          if (g !== 0) g = (g + d) & 0xFFFF;
        }
        if (g !== 0) map.set(c, g);
      }
    }
  }
  const head = t["head"].off, locFmt = b.readInt16BE(head + 50);
  const numGlyphs = b.readUInt16BE(t["maxp"].off + 4);
  const loca = t["loca"].off, glyf = t["glyf"].off;
  const glyphSig = g => {
    const a = locFmt ? b.readUInt32BE(loca + g * 4) : b.readUInt16BE(loca + g * 2) * 2;
    const z = locFmt ? b.readUInt32BE(loca + g * 4 + 4) : b.readUInt16BE(loca + g * 2 + 2) * 2;
    if (z <= a) return "empty";
    const o = glyf + a;
    const nc = b.readInt16BE(o);
    if (nc < 0) return "composite:" + b.toString("hex", o + 10, glyf + z);   // components (rare here)
    const endPts = [];
    for (let i = 0; i < nc; i++) endPts.push(b.readUInt16BE(o + 10 + i * 2));
    const il = b.readUInt16BE(o + 10 + nc * 2);
    const rest = b.toString("hex", o + 12 + nc * 2 + il, glyf + z);       // flags + coordinates, instructions dropped
    return nc + ":" + endPts.join(",") + ":" + b.toString("hex", o + 2, o + 10) + ":" + rest.replace(/(00)+$/, "");
  };
  return { map, subs, numGlyphs, glyphSig };
}

const fams = JSON.parse(fs.readFileSync(path.join(root, "families.json"), "utf8"));
const out = [];
let problems = 0;
for (const g of fams) {
  const rep = readTtf(g.rep);
  const facesSame = g.faces.map(f => {
    const t = readTtf(f.file);
    if (t.map.size !== rep.map.size) return false;
    for (const [c, gid] of rep.map) if (t.map.get(c) !== gid) return false;
    return true;
  });
  const byGid = new Map();
  for (const [c, gid] of rep.map) byGid.set(gid, (byGid.get(gid) || []).concat(c));
  const shared = [...byGid.values()].filter(l => l.length > 1);
  const bySig = new Map();
  for (const [c, gid] of rep.map) { const s = rep.glyphSig(gid); if (s === "empty") continue; bySig.set(s, (bySig.get(s) || []).concat(c)); }
  const sameOutline = [...bySig.values()].filter(l => l.length > 1);
  // compare with the per-family JSON
  const F = JSON.parse(fs.readFileSync(path.join(root, "fam", g.group + ".json"), "utf8"));
  const famMapped = F.codepoints.filter(c => c.kind === "mapped");
  let gidMismatch = 0, outlineMismatch = 0;
  const hex = c => "U+" + c.toString(16).toUpperCase().padStart(4, "0");
  for (const c of famMapped) {
    const cp = parseInt(c.cp.slice(2), 16);
    if (rep.map.get(cp) !== c.gid) gidMismatch++;
    const mine = (sameOutline.find(l => l.includes(cp)) || []).filter(x => x !== cp).map(hex).sort();
    const theirs = (c.sameOutlineAs || []).slice().sort();
    if (mine.join() !== theirs.join()) { outlineMismatch++; if (outlineMismatch <= 3) out.push(`  outline diff ${g.group} ${c.cp}: mine [${mine}] json [${theirs}]`); }
  }
  // C0 controls (<0x20) are in the cmap but were not written as lines
  const below20 = [...rep.map.keys()].filter(c => c < 0x20).length;
  const line = `${g.group}: faces ${g.faces.length} sameCmap ${facesSame.every(x => x)} | cmap(3,1) ${rep.map.size} cps (${below20} below U+0020), subtables ${rep.subs.join(" ")} | sharedGid ${shared.length ? shared.map(l => l.map(hex).join("=")).join(" ") : "none"} | sameOutline ${sameOutline.map(l => l.map(hex).join("=")).join(" ") || "none"} | json mapped ${famMapped.length} gidMismatch ${gidMismatch} outlineMismatch ${outlineMismatch}`;
  if (!facesSame.every(x => x) || gidMismatch || shared.length || famMapped.length + below20 !== rep.map.size) problems++;
  out.push(line);
}
out.push("problems " + problems);
fs.writeFileSync(path.join(root, "verify-cmap.txt"), out.join("\n"));
console.log(out.join("\n"));
