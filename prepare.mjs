import {generateKeyPairSync, createHash} from 'node:crypto';
import {readFileSync, writeFileSync, existsSync} from 'node:fs';
import {dirname,join} from 'node:path';
import {fileURLToPath} from 'node:url';
import {installFacebookCapture} from './extension/story.js';
const root=dirname(fileURLToPath(import.meta.url));
writeFileSync(join(root,'extension','story-capture.js'),'('+installFacebookCapture.toString()+')();\n');
const path=join(root,'extension','manifest.json');const manifest=JSON.parse(readFileSync(path,'utf8'));
if(!manifest.key){const {publicKey}=generateKeyPairSync('rsa',{modulusLength:2048});manifest.key=publicKey.export({type:'spki',format:'der'}).toString('base64');writeFileSync(path,JSON.stringify(manifest,null,2)+'\n');}
const id=[...createHash('sha256').update(Buffer.from(manifest.key,'base64')).digest().subarray(0,16)].map(b=>String.fromCharCode(97+(b>>4),97+(b&15))).join('');
const host=['LocalDownloadHost.v04.exe','LocalDownloadHost.v03.exe','LocalDownloadHost.v02.exe','LocalDownloadHost.exe'].find(name=>existsSync(join(root,'bin',name)));
writeFileSync(join(root,'bin','host.json'),JSON.stringify({name:'com.localdownload.manager',description:'Local Download Manager helper',path:join(root,'bin',host),type:'stdio',allowed_origins:[`chrome-extension://${id}/`]},null,2)+'\n');
writeFileSync(join(root,'extension-id.txt'),id+'\n');
console.log(`Extension ID: ${id}`);
