import {chromium} from 'playwright';
import {createServer} from 'node:http';
import {readFileSync,writeFileSync} from 'node:fs';
import {dirname,join} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=dirname(dirname(fileURLToPath(import.meta.url)));
const server=createServer((req,res)=>{
  if(req.url==='/sample.mp4'){res.writeHead(200,{'Content-Type':'video/mp4'});res.end(Buffer.alloc(2048));}
  else {res.writeHead(200,{'Content-Type':'text/html'});res.end('<html><body><h1>Local video fixture</h1><video controls style="width:640px;height:360px" src="/sample.mp4"></video></body></html>');}
});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
let context;
try {
  const extension=join(root,'extension');
  context=await chromium.launchPersistentContext(join(root,'tests/output/browser-profile'),{channel:'chromium',headless:true,args:[`--disable-extensions-except=${extension}`,`--load-extension=${extension}`]});
  const page=await context.newPage();
  await page.goto(`http://127.0.0.1:${server.address().port}`);
  let worker=context.serviceWorkers()[0]||await context.waitForEvent('serviceworker');
  const records=await worker.evaluate(async()=>{
    for(let attempt=0;attempt<30;attempt++){
      const state=await chrome.storage.session.get(null);
      const found=Object.values(state).flat().filter(x=>x.url?.includes('sample.mp4'));
      if(found.length)return found;
      await new Promise(resolve=>setTimeout(resolve,100));
    }
    throw Error('No media captured');
  });
  console.log('PASS loaded Manifest V3 extension and detected media',records.length);
  await page.waitForFunction(()=>Array.from(document.querySelectorAll('div')).some(x=>x.style.zIndex==='2147483646'&&x.style.display==='block'&&x.getBoundingClientRect().width>100&&x.getBoundingClientRect().width<400));
  const overlay=await page.evaluate(()=>Array.from(document.querySelectorAll('div')).find(x=>x.style.zIndex==='2147483646')?.getBoundingClientRect().toJSON());
  if(!overlay?.width)throw Error('Video overlay was not visible');
  console.log('PASS native-like video download overlay rendered');
  await page.screenshot({path:join(root,'tests/output/browser-overlay.png')});
  // Keep the fixture as the active tab: popup resolves the current tab when loaded.
  const extensionId=readFileSync(join(root,'extension-id.txt'),'utf8').trim();
  const native=await worker.evaluate(async()=>{
    try{return await chrome.runtime.sendNativeMessage('com.localdownload.manager',{op:'ping'});}catch(error){return {ok:false,error:error.message};}
  });
  if(!native.ok)throw Error('Chrome native handoff: '+native.error);
  console.log('PASS Chrome-to-native-host ping',native.version);
  writeFileSync(join(root,'tests/output/browser-verification.json'),JSON.stringify({mediaCaptured:true,overlayRendered:true,nativeHost:native},null,2));
}finally{if(context)await context.close();await new Promise(resolve=>server.close(resolve));}
