import {chromium} from 'playwright';
import {dirname,join} from 'node:path';import {fileURLToPath} from 'node:url';import {readFileSync} from 'node:fs';import assert from 'node:assert/strict';
const root=dirname(dirname(fileURLToPath(import.meta.url)));const extension=join(root,'extension');
const context=await chromium.launchPersistentContext(join(root,'tests/output/popup-profile'),{channel:'chromium',headless:true,args:[`--disable-extensions-except=${extension}`,`--load-extension=${extension}`]});
try{
  await context.route('https://www.instagram.com/**',route=>route.fulfill({status:200,contentType:'text/html',body:'<html><title>Owned Instagram UI fixture</title><body><video style="width:320px;height:200px"></video></body></html>'}));
  const video=await context.newPage();await video.goto('https://www.instagram.com/reels/DdgUlmZikAU/');
  const worker=context.serviceWorkers()[0]||await context.waitForEvent('serviceworker');
  const tab=await worker.evaluate(async()=>{const tabs=await chrome.tabs.query({});return tabs.find(x=>x.url?.includes('instagram.com/reels/'));});assert(tab);
  await context.addCookies([{name:'sessionid',value:'OWNED_TEST_ONLY',domain:'.instagram.com',path:'/',secure:true,httpOnly:true},{name:'unrelated',value:'DO_NOT_FORWARD',domain:'.facebook.com',path:'/',secure:true}]);
  await worker.evaluate(()=>{chrome.runtime.sendNativeMessage=async(host,request)=>{await chrome.storage.session.set({testNativeRequest:request});return {ok:true,message:'Test request captured'};};});
  const popup=await context.newPage();await popup.addInitScript(tab=>{chrome.tabs.query=async()=>[tab];},tab);
  const id=readFileSync(join(root,'extension-id.txt'),'utf8').trim();await popup.goto(`chrome-extension://${id}/popup.html`);
  await popup.getByRole('button',{name:'Download Instagram Reel (MP4)',exact:true}).waitFor();
  assert.equal(await popup.locator('#session').isChecked(),false);
  await popup.locator('#page-download').click();await popup.getByRole('status').filter({hasText:'Test request captured'}).waitFor();
  let request=await worker.evaluate(async()=>(await chrome.storage.session.get('testNativeRequest')).testNativeRequest);
  assert.equal(request.url,'https://www.instagram.com/reel/DdgUlmZikAU/');assert.equal(request.kind,'page');assert.deepEqual(request.cookies,[]);
  await popup.locator('#session').check();await popup.locator('#page-download').click();
  await popup.waitForFunction(()=>!document.querySelector('#page-download').disabled);
  request=await worker.evaluate(async()=>(await chrome.storage.session.get('testNativeRequest')).testNativeRequest);
  assert(request.cookies.some(c=>c.name==='sessionid'&&c.httpOnly));assert(!request.cookies.some(c=>c.name==='unrelated'));
  await popup.locator('#page-url').fill('https://www.facebook.com/reel/1822430045852437');
  assert.equal(await popup.locator('#session').isChecked(),false);await popup.getByRole('button',{name:'Download Facebook Reel (MP4)',exact:true}).waitFor();
  await popup.locator('#page-url').fill('https://www.instagram.com/');assert.equal(await popup.locator('#page-download').isDisabled(),true);
  await popup.locator('#page-url').fill('https://www.instagram.com/reels/DdgUlmZikAU/');
  await popup.screenshot({path:join(root,'tests/output/social-popup.png')});
  console.log('PASS popup social labels, URL normalization, session off by default, same-site cookie opt-in, cross-site isolation, and feed rejection');
}finally{await context.close();}
