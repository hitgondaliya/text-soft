const fs=require('fs');
const lines=fs.readFileSync(process.argv[2],'utf8').split('\n').filter(l=>l.startsWith('    OTHER\t'));
const cls={}; const ex={};
const sign=/[ా-ౌౕౖఁ-ః]/;
for(const l of lines){
  const p=l.split('\t'); const hex=p[1]; const typed=p[2].replace(/^typed /,''); const read=p[3].replace(/^read /,'').replace(/ \[.*$/,'');
  let c;
  if(typed.includes('్ఝ')) c='C్ఝ: composer drops ఝ after the link';
  else if(typed.includes('ౄ')) c='ౄ: no glyph';
  else if(/[ఌౡౣౢ]/.test(typed)) c='ఌ ౡ ౣ ౢ: composer types nothing / pieces';
  else if(/^[ా-ౌ]/.test(typed) || hex==='4F F2' || hex.startsWith('E6 61')) c='lone sign / lone ై key quirk';
  else if(/[ా-ౌఁ-ః]్ర/.test(typed) || /ం్ర|ః్ర|ఁ్ర/.test(typed)) c='ra-vattu struck after a sign/tail (alt order; bytes = the ra-vattu on the letter)';
  else if(typed.includes('్క్ష')) c='C్క్ష';
  else if(typed.includes('్ఙ')) c='్ఙ';
  else if(/హ్/.test(typed)) c='హ్C';
  else if(/్ర[^ా-ౌ]|్ర$/.test(typed)) c='ra-vattu other';
  else c='other';
  cls[c]=(cls[c]||0)+1; (ex[c]=ex[c]||[]).push(hex+' typed '+typed+' read '+read);
}
for(const [k,v] of Object.entries(cls).sort((a,b)=>b[1]-a[1])) console.log(v,k,'\n    e.g. '+ex[k].slice(0,5).join('\n    e.g. '));
