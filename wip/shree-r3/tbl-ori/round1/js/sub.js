const {gt,cps,isCons,shape}=require('./lib.js'); const T=gt().truth;
const P=u=>T[u]?T[u].preferred:null;
const cons=[...'କଖଗଘଙଚଛଜଝଞଟଠଡଢଣତଥଦଧନପଫବଭମଯରଲଳଶଷସହୟ'].concat(['ଡ଼','ଢ଼','ୱ','ଵ']);
const signs=['','ୁ','ୂ','ୃ','୍ର','୍','ା','ି','ୀ','ୖ','ୗ','୍ୟ','ଂ','ୄ'];
// per X: suffix after C1's own spelling, counting
const out={};
for (const x of cons) {
  const row={};
  for (const s of signs) {
    const cnt={};
    for (const c1 of cons) {
      const u=c1+'୍'+x+s; const p=P(u); const pc=P(c1); if(!p||!pc) continue;
      const lig=P(c1+'୍'+x);
      let suf = p.startsWith(pc+' ')? p.slice(pc.length+1) : (p===pc? '' : '#'+p);
      cnt[suf]=(cnt[suf]||0)+1;
    }
    const best=Object.entries(cnt).sort((a,b)=>b[1]-a[1]);
    row[s||'-']=best.slice(0,3).map(e=>e[0]+'×'+e[1]).join(' , ');
  }
  out[x]=row;
}
for (const [x,row] of Object.entries(out)) console.log(x, JSON.stringify(row));
