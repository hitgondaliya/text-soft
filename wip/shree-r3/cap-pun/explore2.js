// Second exploration: the Gurmukhi specifics before the big probe set.
const fs = require('fs');
const S = [];
const add = (hex, tag) => S.push({ codes: hex.split(' ').map(x => parseInt(x, 16)), tag });
[
 ['26 D7', 'ਸ nukta'], ['53 D7', 'ਖ nukta'], ['78 D7', 'ਗ nukta'], ['63 D7', 'ਜ nukta'], ['57 D7', 'ਫ nukta'], ['6D D7', 'ਲ nukta'], ['73 D7', 'ਕ nukta'], ['4F D7', 'ਡ nukta'],
 ['40 69', 'addak bindi'], ['69 40', 'bindi addak'], ['73 40 69', 'ਕ addak bindi'], ['73 69 40', 'ਕ bindi addak'], ['73 40 D6', 'ਕ addak tippi'],
 ['67 73 D1', 'i ਕ ra-sub'], ['67 73 64 66', 'i ਕ link ਤ'], ['67 26 D7', 'i ਸ nukta'], ['73 D1 67', 'ਕ ra-sub i(after)'],
 ['73 D1 6B', 'ਕ rasub aa'], ['73 D2 6B', 'ਕ hasub aa'], ['73 D0 62', 'ਕ vasub u'], ['73 4C 6B', 'ਕ yasub aa'], ['73 D1 62', 'ਕ rasub u'], ['73 D2 62', 'ਕ hasub u'], ['73 D2 6E', 'ਕ hasub uu'],
 ['26 64 66', 'ਸ link ਤ'], ['26 64 66 D1', 'ਸ link ਤ rasub'], ['73 64 73', 'ਕ link ਕ'], ['65 64 72', 'ਨ link ਦ'], ['73 64 6A', 'ਕ link ਰ'], ['73 64 6F', 'ਕ link ਹ'], ['73 64 79', 'ਕ link ਵ'], ['73 64 75', 'ਕ link ਯ'],
 ['24 6B', 'ਅ aa'], ['24 70', 'ਅ ai'], ['24 54', 'ਅ au'], ['67 47', 'i ੲ'], ['47 68', 'ੲ ii'], ['47 6C', 'ੲ e'], ['42 62', 'ੳ u'], ['42 6E', 'ੳ uu'], ['42 74', 'ੳ o'], ['4E', 'ਓ'], ['E4', 'ਇ code E4'], ['24 6B 69', 'ਆ bindi'], ['67 47 D6', 'ਇ tippi'],
 ['D3 D3', 'danda danda'], ['31 51', '1 onkar'], ['31 42 4D', '1 ura 4D'], ['51', 'onkar 51'], ['E0', 'onkar E0'], ['31 E0', '1 E0'],
 ['41', 'code 41'], ['6B 41', 'aa 41'], ['68 41', 'ii 41'], ['73 6B 41', 'ਕ aa 41'], ['73 68 EE', 'ਕ ii EE'], ['73 62 41', 'ਕ u 41'], ['EE', 'code EE'], ['73 6B EE', 'ਕ aa EE'], ['68 EE', 'ii EE'],
 ['73 6E 69', 'ਕ uu bindi'], ['73 62 D6', 'ਕ u tippi'], ['73 68 69', 'ਕ ii bindi'], ['67 73 D6', 'ਕਿੰ'], ['67 73 69', 'ਕਿਂ'],
 ['49 6B', 'ਠ aa'], ['65 6B', 'ਨ aa'], ['65 62', 'ਨ u'], ['49 62', 'ਠ u'], ['65 68', 'ਨ ii'], ['49 D1', 'ਠ rasub'], ['65 64 66', 'ਨ link ਤ'], ['65 6C', 'ਨ e'], ['65 69', 'ਨ bindi'],
 ['77 40 73 6B', 'ਪੱਕਾ'], ['67 73 40 66', 'ਕਿੱਤ'], ['73 44', 'ਕ halant'], ['73 44 66', 'ਕ halant ਤ'],
 ['73 64 E2', 'ਕ link zwj'], ['73 64 E3', 'ਕ link zwnj'], ['73 64 E2 66', 'ਕ link zwj ਤ'], ['73 64 E3 66', 'ਕ link zwnj ਤ'],
 ['75 64', 'ਯ link'], ['75 64 73', 'ਯ link ਕ'], ['73 4C', 'ਕ yasub'], ['73 64 4C', 'ਕ link yasub'],
 ['6A 64 73', 'ਰ link ਕ'], ['6A 62', 'ਰ u'], ['6A 6E', 'ਰ uu'], ['6F 62', 'ਹ u'], ['6F 6E', 'ਹ uu'], ['6F D2', 'ਹ hasub'],
 ['73 54', 'ਕ au'], ['73 74', 'ਕ o'], ['73 74 69', 'ਕ o bindi'], ['73 70 69', 'ਕ ai bindi'],
 ['73 D4', 'ਕ visarga'], ['73 6B D4', 'ਕ aa visarga'],
 ['26 D7 6B', 'ਸ਼ aa'], ['67 26 D7 D6', 'ਸ਼ਿੰ'], ['26 D7 64 66', 'ਸ਼ link ਤ'],
 ['EF', 'rupee'], ['7C', 'ldquo'], ['7E', 'rdquo'], ['D9', 'double danda'], ['22', 'lsquo'],
].forEach(([h, t]) => add(h, t));
fs.writeFileSync(__dirname + '/seq-explore2.json', JSON.stringify(S));
console.log(S.length);
