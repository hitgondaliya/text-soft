const fs=require('fs'); const vendor=JSON.parse(fs.readFileSync('../../../cap-pun/pun-vendor-keys.json','utf8'));
const layoutOf={CRTRONIC:'pun-crtronic',DOE:'pun-doe',ENG:'pun-eng',GODREJ:'pun-godrej',INSCRIPT:'pa-inscript',ITR:'pun-itr',NEWROMA:'pun-newroma',PHONETIC:'pun-phonetic',RAMING:'pun-raming',ROMA:'pun-roma',TYPEWRIT:'pun-typewrit',VARITYPE:'pun-varitype'};
const nfc=s=>s==null?s:s.normalize('NFC');
for (const f of process.argv.slice(2)) {
  const ts=JSON.parse(fs.readFileSync('results/'+f,'utf8'));
  const S={withShree:0, b7eq:0, b456eq:0, uEq:0, both:0, nonPhon:{withShree:0,b7eq:0,uEq:0,both:0}, shreeNothing:0, shreeNothingTsTypes:0};
  for (const [file,kb] of Object.entries(vendor.keyboards)) { const name=file.replace('.PUN',''); const tk=ts[layoutOf[name]]; const phon=['ENG','NEWROMA','PHONETIC','ROMA'].includes(name);
    for (const L of ['0','1']) for (const t of tk.keys.filter(k=>k.layer===Number(L))) { const v=(kb.layers[L]||{})[t.key]; const su=v&&v.u?v.u:null, sb=v&&v.b7?v.b7:null;
      if (su==null && sb==null) { S.shreeNothing++; if (t.uniHandled && t.uni!=='') S.shreeNothingTsTypes++; continue; }
      S.withShree++; if(!phon) S.nonPhon.withShree++;
      const b7=(sb??'')===t.p7Hex, b456=(sb??'').split(' ').map(x=>x==='3C'?'U+20B9':x).join(' ')===t.p456Hex, ue=su!=null&&nfc(su)===nfc(t.uni);
      if(b7)S.b7eq++; if(b456)S.b456eq++; if(ue)S.uEq++; if(b7&&ue&&b456)S.both++;
      if(!phon){ if(b7)S.nonPhon.b7eq++; if(ue)S.nonPhon.uEq++; if(b7&&ue&&b456)S.nonPhon.both++; }
    } }
  let cs=0,csOk=0; for (const [id,tk] of Object.entries(ts)) for (const [key,s] of Object.entries(vendor.ctrlShift1.keys)) { const t=tk.keys.find(k=>k.layer===2&&k.key===key); cs++; if(t.uni===s.u&&t.p7Hex===s.b7) csOk++; }
  console.log(f, JSON.stringify(S), 'ctrlShift1', csOk+'/'+cs);
}
