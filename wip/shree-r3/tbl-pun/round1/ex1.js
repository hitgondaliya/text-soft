const g=require(process.argv[2]); const t=g.truth;
const cp=s=>[...s].map(c=>c.codePointAt(0).toString(16).toUpperCase().padStart(4,"0")).join(" ");
const want=process.argv[3]; const re=new RegExp(want);
let n=0; for (const k of Object.keys(t)) { if (re.test(k)) { n++; if (n<=+process.argv[4]) console.log(k+"\t["+cp(k)+"]\t"+t[k].preferred+"\t|acc "+t[k].accepted.join(" | ")+(t[k].alternatives.length?"\t|alt "+JSON.stringify(t[k].alternatives):"")); } }
console.log("matched",n);
