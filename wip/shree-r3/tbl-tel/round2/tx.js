const fs=require('fs');
const f=process.argv[2]; const mode=process.argv[3]||'text';
const lines=fs.readFileSync(f,'utf8').split('\n').filter(Boolean);
let out=[];
for(const l of lines){let j;try{j=JSON.parse(l)}catch{continue}
 const m=j.message; if(!m) continue;
 const c=Array.isArray(m.content)?m.content:[{type:'text',text:m.content}];
 for(const b of c){
  if(m.role==='assistant'&&b.type==='text') out.push('A: '+b.text);
  if(m.role==='assistant'&&b.type==='thinking'&&mode==='think') out.push('T: '+(b.thinking||'').slice(0,3000));
  if(m.role==='assistant'&&b.type==='tool_use') out.push('TOOL '+b.name+': '+JSON.stringify(b.input).slice(0, mode==='full'?4000:300));
  if(m.role==='user'&&b.type==='tool_result' && mode==='full'){let t=b.content; if(Array.isArray(t)) t=t.map(x=>x.text||'').join(''); out.push('R: '+String(t).slice(0,1500));}
 }}
console.log(out.join('\n'));
