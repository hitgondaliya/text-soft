const fs=require('fs');
const lines=fs.readFileSync(process.argv[2],'utf8').split('\n').filter(l=>l.startsWith('    OTHER\t'));
const cls={}; const ex={};
for(const l of lines){
  const p=l.split('\t'); const hex=p[1]; const typed=p[2].replace(/^typed /,''); const read=p[3].replace(/^read /,'').replace(/ \[.*$/,'');
  let c;
  if(typed.includes('్ఝ')) c='C్ఝ: composer drops ఝ after the link';
  else if(typed.includes('ౄ')) c='ౄ: no glyph, composer drops it / writes A0';
  else if(/[ఌౡౣ]/.test(typed)) c='ఌ ౡ ౣ: composer types nothing';
  else if(typed.includes('్క్ష')) c='C్క్ష: Unicode composer drops క్ష';
  else if(/హ్.[ెే]/.test(typed)||/హ్[^్]+[ెే]/.test(typed)) c='హ్C + ె/ే: sign dropped';
  else if(typed.includes('ై')) c='ై placement/quirk';
  else if(typed.includes('ఙ్ర')||typed.includes('్ఙ')) c='్ఙ (5D read as ః)';
  else c='other';
  cls[c]=(cls[c]||0)+1; (ex[c]=ex[c]||[]).push(hex+' typed '+typed+' read '+read);
}
for(const [k,v] of Object.entries(cls).sort((a,b)=>b[1]-a[1])) console.log(v,k,'\n    e.g. '+ex[k].slice(0,4).join('\n    e.g. '));
