const fs=require('fs'); const f=process.argv[2]; const b=fs.readFileSync(f);
const u16=o=>b.readUInt16BE(o), u32=o=>b.readUInt32BE(o);
const n=u16(4); const T={}; for(let i=0;i<n;i++){ const tag=b.toString('ascii',12+16*i,16+16*i); T[tag]=[u32(12+16*i+8),u32(12+16*i+12)]; }
console.log(Object.keys(T).join(' '));
const [po,pl]=T['post']; const fmt=u32(po); console.log('post format', (fmt/65536).toString(16));
if (fmt===0x20000){ const ng=u16(po+32); const idx=[]; for(let i=0;i<ng;i++) idx.push(u16(po+34+2*i)); let p=po+34+2*ng; const names=[]; while(p<po+pl){ const len=b[p]; names.push(b.toString('latin1',p+1,p+1+len)); p+=1+len; }
 // cmap 3,1
 const [co]=T['cmap']; const nt=u16(co+2); let sub=-1; for(let i=0;i<nt;i++){ if(u16(co+4+8*i)==3&&u16(co+6+8*i)==1) sub=co+u32(co+8+8*i); }
 const segX2=u16(sub+6), ends=sub+14, starts=ends+segX2+2, deltas=starts+segX2, ranges=deltas+segX2; const map={};
 for(let s=0;s<segX2/2;s++){ const e=u16(ends+2*s), st=u16(starts+2*s), d=b.readInt16BE(deltas+2*s), ro=u16(ranges+2*s); for(let c=st;c<=e&&c!=0xFFFF;c++){ let g= ro==0? (c+d)&0xFFFF : u16(ranges+2*s+ro+2*(c-st)); if(ro!=0&&g!=0) g=(g+d)&0xFFFF; if(g) map[c]=g; } }
 const gname=g=>{ const i=idx[g]; return i<258? 'std'+i : names[i-258]; };
 const want=(process.argv[3]||'').split(',').filter(Boolean).map(h=>parseInt(h,16));
 for (const c of (want.length?want:Object.keys(map).map(Number))) console.log(c.toString(16), 'gid', map[c], gname(map[c]));
}
