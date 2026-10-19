const fs=require("fs");
const CAP="C:/Users/harde/AppData/Local/Temp/claude/C--Users-harde-Desktop-Windows-Font-Software/7673923b-96b4-4287-87dd-a8e7c6533eca/scratchpad/r3/cap-tel/";
function load(f){return JSON.parse(fs.readFileSync(CAP+(f||"tel7-groundtruth.json"),"utf8"));}
const isCons=c=>{const x=c.codePointAt(0);return (x>=0xC15&&x<=0xC39&&x!==0xC29)||x===0xC58||x===0xC59||x===0xC5A;};
const isSign=c=>{const x=c.codePointAt(0);return (x>=0xC3E&&x<=0xC4C&&x!==0xC45&&x!==0xC49)||x===0xC62||x===0xC63||x===0xC55||x===0xC56;};
const isTail=c=>"ఀఁంఃఄ".includes(c);
const isVowel=c=>{const x=c.codePointAt(0);return x>=0xC05&&x<=0xC14||x===0xC60||x===0xC61;};
function shape(u){return [...u].map(c=>isCons(c)?"C":c==="\u0C4D"?"H":c==="\u200D"?"J":c==="\u200C"?"N":isSign(c)?"s":isTail(c)?"t":isVowel(c)?"V":"o").join("");}
const cps=u=>[...u].map(c=>c.codePointAt(0).toString(16).toUpperCase().padStart(4,"0")).join(" ");
module.exports={load,isCons,isSign,isTail,isVowel,shape,cps,CAP};
