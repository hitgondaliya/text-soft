const fs=require('fs');
const f=process.argv[2];
for (const line of fs.readFileSync(f,'utf8').split('\n')) {
  if(!line||line[0]=='#'){console.log(line);continue;}
  const [layer,key,uu,uh,lu,lh]=line.split('\t');
  const u=uh?uh.split(' ').map(h=>String.fromCharCode(parseInt(h,16))).join(''):'';
  console.log(layer, String.fromCharCode(parseInt(key,16)), JSON.stringify(u), uh, '|', lu, lh);
}
