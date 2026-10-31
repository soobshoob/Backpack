import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

// Read the Unicode cmap in the shipped TTF. No font service or OS font fallback.
function supports(font,code){
 let cmap=0;for(let i=0;i<font.readUInt16BE(4);i++){const table=12+i*16;if(font.toString('ascii',table,table+4)==='cmap')cmap=font.readUInt32BE(table+8);}
 assert(cmap,'cmap table is missing');
 for(let i=0;i<font.readUInt16BE(cmap+2);i++){
  const record=cmap+4+i*8,platform=font.readUInt16BE(record);if(platform!==0&&platform!==3)continue;
  const table=cmap+font.readUInt32BE(record+4),format=font.readUInt16BE(table);
  if(format===12){for(let j=0;j<font.readUInt32BE(table+12);j++){const group=table+16+j*12;if(code>=font.readUInt32BE(group)&&code<=font.readUInt32BE(group+4))return font.readUInt32BE(group+8)+code-font.readUInt32BE(group)>0;}}
  if(format===4&&code<=65535){const count=font.readUInt16BE(table+6)/2;for(let j=0;j<count;j++){
   const end=font.readUInt16BE(table+14+j*2),start=font.readUInt16BE(table+16+count*2+j*2);if(code<start||code>end)continue;
   const delta=font.readInt16BE(table+16+count*4+j*2),rangeAt=table+16+count*6+j*2,range=font.readUInt16BE(rangeAt);
   const glyph=range?font.readUInt16BE(rangeAt+range+(code-start)*2):(code+delta)&65535;if(glyph)return true;
  }}
 }
 return false;
}
test('All four local Spectral faces contain Latin, Russian and Ukrainian alphabets',async()=>{
 const characters=Array.from('ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyzАБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЫЬЪЭЮЯабвгдеёжзийклмнопрстуфхцчшщыьъэюяІЇЄҐіїєґ');
 for(const face of ['Regular','SemiBold','Bold','Italic']){const font=await readFile(new URL(`../public/fonts/Spectral-${face}.ttf`,import.meta.url));assert.deepEqual(characters.filter(character=>!supports(font,character.codePointAt(0))),[],face);}
});

test('Local font chain covers the letters in every selectable translation',async()=>{
 const spectral=await readFile(new URL('../public/fonts/Spectral-Regular.ttf',import.meta.url));
 const greek=await readFile(new URL('../public/fonts/NotoSerif-400.ttf',import.meta.url));
 const {readdir}=await import('node:fs/promises');
 for(const file of await readdir(new URL('../src/locales/',import.meta.url))){const text=await readFile(new URL('../src/locales/'+file,import.meta.url),'utf8');const letters=[...new Set(Array.from(text).filter(c=>/\p{Letter}/u.test(c)))];assert.deepEqual(letters.filter(c=>!supports(spectral,c.codePointAt(0))&&!supports(greek,c.codePointAt(0))),[],file);}
});
