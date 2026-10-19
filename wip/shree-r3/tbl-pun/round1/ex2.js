const g=require(process.argv[2]); const re=new RegExp(process.argv[3]); const kre=new RegExp(process.argv[4]||"."); let n=0;
const cp=s=>[...s].map(c=>c.codePointAt(0).toString(16).toUpperCase().padStart(4,"0")).join(" ");
for (const p of g.pairs) if (re.test(p.unicode) && kre.test(p.kind)) { n++; if (n<=+(process.argv[5]||40)) console.log(p.unicode+"\t["+cp(p.unicode)+"]\t"+p.bytes+"\t"+p.kind.slice(0,70)+"\t"+p.source.slice(0,80)); }
console.log("matched",n);
