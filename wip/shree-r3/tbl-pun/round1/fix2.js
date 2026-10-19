const fs=require("fs"); const p=process.argv[2]; let s=fs.readFileSync(p,"utf8"); const L=s.split("\n");
const B=String.fromCharCode(92); const e=cp=>B+"u"+cp.toString(16).toUpperCase().padStart(4,"0");
for (let i=0;i<L.length;i++){
 if (L[i].includes("public static bool IsVowelSign(")) L[i]="    public static bool IsVowelSign(char c) => (c >= '"+e(0xA3E)+"' && c <= '"+e(0xA42)+"') || c is '"+e(0xA47)+"' or '"+e(0xA48)+"' or '"+e(0xA4B)+"' or '"+e(0xA4C)+"';";
 if (L[i].includes("public static bool IsVowel(")) L[i]="    public static bool IsVowel(char c) => (c >= '"+e(0xA05)+"' && c <= '"+e(0xA0A)+"') || c is '"+e(0xA0F)+"' or '"+e(0xA10)+"' or '"+e(0xA13)+"' or '"+e(0xA14)+"' or '"+e(0xA72)+"' or '"+e(0xA73)+"';";
}
fs.writeFileSync(p,L.join("\n"));
