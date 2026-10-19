const fs=require('fs');
const GT='C:/Users/harde/AppData/Local/Temp/claude/C--Users-harde-Desktop-Windows-Font-Software/7673923b-96b4-4287-87dd-a8e7c6533eca/scratchpad/r3/cap-ori/ori7-groundtruth.json';
let _d=null; function gt(){ return _d||(_d=JSON.parse(fs.readFileSync(GT,'utf8'))); }
const cps=s=>[...s].map(c=>c.codePointAt(0).toString(16).toUpperCase().padStart(4,'0')).join(' ');
function isCons(c){const x=c.codePointAt(0); return (x>=0xB15&&x<=0xB39&&![0xB29,0xB31,0xB34].includes(x))||[0xB5C,0xB5D,0xB5F,0xB71].includes(x);}
function shape(u){return [...u].map(c=>{const x=c.codePointAt(0); if(isCons(c))return 'C'; if(x==0xB4D)return 'H'; if(x==0x200D)return 'J'; if(x==0x200C)return 'N'; if((x>=0xB3E&&x<=0xB4C)||x==0xB56||x==0xB57||x==0xB62||x==0xB63)return 's'; if(x>=0xB01&&x<=0xB03)return 't'; if(x==0xB3C)return 'n'; if((x>=0xB05&&x<=0xB14)||x==0xB60||x==0xB61) return 'V'; return 'o';}).join('');}
module.exports={gt,cps,isCons,shape};
