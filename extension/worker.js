import {classify, validPage, youtubePage, pageInfo} from './media.js';
import {inspectFacebookStory} from './story.js';
const HOST = 'com.localdownload.manager';
// Serialize updates: Chrome's service worker can receive overlapping network events.
let updates = Promise.resolve();
const key = tabId => `media:${tabId}`;
const read = async tabId => ((await chrome.storage.session.get(key(tabId)))[key(tabId)] || []).filter(record=>classify(record.url,record.mime));
function remember(tabId, url, mime, page, frameId = 0) {
  const kind = classify(url, mime);
  if (tabId < 0 || !kind) return Promise.resolve(null);
  const task = updates.then(async () => {
    const records = await read(tabId);
    let record = records.find(x => x.url === url);
    if (!record) {
      record = {id: crypto.randomUUID(), url, kind, mime, page, frameId, time: Date.now()};
      records.unshift(record);
    } else { record.time = Date.now(); }
    await chrome.storage.session.set({[key(tabId)]: records.slice(0,100)});
    await chrome.action.setBadgeText({tabId, text: String(Math.min(records.length,100))});
    return record;
  });
  updates = task.catch(() => {});
  return task;
}
chrome.webRequest.onHeadersReceived.addListener(details => {
  if (details.statusCode < 200 || details.statusCode >= 300) return;
  const mime = details.responseHeaders?.find(h => h.name.toLowerCase() === 'content-type')?.value || '';
  remember(details.tabId, details.url, mime, details.initiator, details.frameId).catch(() => {});
}, {urls:['http://*/*','https://*/*']}, ['responseHeaders']);
chrome.tabs.onRemoved.addListener(tabId => {chrome.storage.session.remove(key(tabId));});
chrome.tabs.onUpdated.addListener((tabId, changes) => {
  if (changes.status === 'loading' || changes.url) {
    updates = updates.then(async () => {
      await chrome.storage.session.remove(key(tabId));
      await chrome.action.setBadgeText({tabId, text:''});
    }).catch(() => {});
  }
});
async function send(tabId, id, page) {
  await updates;
  const record = (await read(tabId)).find(x => x.id === id);
  if (!record) throw Error('This media is no longer available. Play the video again.');
  if (record.kind !== 'file') return sendPage(tabId, record.url, false, true);
  const tab = await chrome.tabs.get(tabId);
  const cookies = await chrome.cookies.getAll({url:record.url});
  // Only collect cookies for the explicitly selected URL, at the time of download.
  const request = {op:'download', url:record.url, kind:record.kind, title:tab.title || '',
    sourceUrl:validPage(tab.url)?tab.url:record.url,
    referer: validPage(page) ? page : (validPage(tab.url) ? tab.url : ''),
    userAgent:navigator.userAgent, cookie:cookies.map(c => `${c.name}=${c.value}`).join('; ')};
  const response = await chrome.runtime.sendNativeMessage(HOST, request);
  if (!response?.ok) throw Error(response?.error || 'The desktop helper did not accept this download.');
  return response;
}
async function sendPage(tabId, requested, useSession, mediaUrl=false, content=false) {
  const tab=await chrome.tabs.get(tabId);const current=pageInfo(tab.url);let info=pageInfo(requested||tab.url);
  if(mediaUrl) {
    await updates;const known=(await read(tabId)).some(x=>x.url===requested&&x.kind==='manifest');
    info=known?{url:requested,site:'Website',domain:new URL(requested).hostname,label:'Download stream'}:current;
  }
  if(!info)throw Error('Open an individual video/reel, or paste its share link.');
  let mediaKind='page';const sourceUrl=info.url;
  if(info.site==='Facebook'&&new URL(info.url).pathname.startsWith('/stories/')) {
    if(!current||new URL(current.url).pathname!==new URL(info.url).pathname)throw Error('Open this Facebook story in the current tab, play it, then click Download again.');
    const results=await chrome.scripting.executeScript({target:{tabId},world:'MAIN',func:inspectFacebookStory});
    const story=results[0]?.result;
    if(!story)throw Error('The active story video is not available yet. Refresh this Facebook tab after reloading the extension, play the story, and retry. Photo stories cannot be downloaded as videos.');
    // Page-world data is untrusted: permit only Facebook-owned media URLs or
    // a canonical Facebook video ID, and never carry Facebook cookies to a CDN.
    const storyUrl=new URL(story.url);
    const allowed=storyUrl.protocol==='https:'&&!storyUrl.username&&!storyUrl.password&&['fbcdn.net','fbsbx.com','facebook.com'].some(host=>storyUrl.hostname===host||storyUrl.hostname.endsWith('.'+host));
    if(!allowed||!['file','page'].includes(story.kind))throw Error('Invalid story media URL.');
    if(story.kind==='page'&&(storyUrl.hostname!=='www.facebook.com'||storyUrl.pathname!=='/watch/'||!/^\?v=\d+$/.test(storyUrl.search)))throw Error('Invalid Facebook video ID.');
    info={...info,url:story.url};mediaKind=story.kind;
  }
  if(content && requested && info.url!==current?.url) {
    const tabHost=new URL(tab.url).hostname.replace(/^(www|m|web|mbasic)\./,'');
    if(info.site==='Website'||!['facebook.com','instagram.com','youtube.com'].includes(tabHost)||info.domain!==tabHost)throw Error('This video link belongs to another site. Paste it into the extension popup to download it.');
  }
  let cookies=[];
  if(useSession) {
    const stores=await chrome.cookies.getAllCookieStores();const store=stores.find(s=>s.tabIds.includes(tabId));
    const filter=info.site==='Website'?{url:info.url}:{domain:info.domain};if(store)filter.storeId=store.id;
    const sourceHost=new URL(tab.url).hostname;
    if(sourceHost!==info.domain&&!sourceHost.endsWith('.'+info.domain))throw Error('Open that video site in this tab before using its signed-in session.');
    if(mediaKind==='page')cookies=(await chrome.cookies.getAll(filter)).filter(c=>!c.partitionKey).map(c=>({domain:c.domain,path:c.path,name:c.name,value:c.value,secure:c.secure,httpOnly:c.httpOnly,hostOnly:c.hostOnly,expirationDate:c.expirationDate||0}));
  }
  const directCookies=mediaKind==='file'?await chrome.cookies.getAll({url:info.url}):[];
  const response=await chrome.runtime.sendNativeMessage(HOST,{op:'download',kind:mediaKind,url:info.url,sourceUrl,title:requested&&sourceUrl!==current?.url?'':tab.title||'',cookies,cookie:directCookies.map(c=>`${c.name}=${c.value}`).join('; '),referer:sourceUrl,userAgent:navigator.userAgent});
  if(!response?.ok)throw Error(response?.error||'Desktop helper did not accept the video.');return response;
}
chrome.runtime.onMessage.addListener((message, sender, reply) => {
  (async () => {
    const ownPage = sender.id === chrome.runtime.id && sender.url?.startsWith(chrome.runtime.getURL(''));
    const content = sender.id === chrome.runtime.id && sender.tab && validPage(sender.url);
    if (!ownPage && !content) throw Error('Untrusted sender.');
    const tabId = content ? sender.tab.id : message.tabId;
    if (!Number.isInteger(tabId) && message.op !== 'ping') throw Error('Select a browser tab.');
    if (message.op === 'list') { await updates; return {ok:true, media:await read(tabId)}; }
    if (message.op === 'discover' && content) {
      const record = await remember(tabId, message.url, message.mime || '', sender.url, sender.frameId);
      return {ok:true,record};
    }
    if (message.op === 'download') return await send(tabId, message.id, content ? sender.url : null);
    if (message.op === 'page')return await sendPage(tabId,message.url,message.useSession===true,false,!!content);
    if (message.op === 'youtube') {
      const tab=await chrome.tabs.get(tabId);
      const url=youtubePage(tab.url);
      if(!url)throw Error('Open a YouTube video page first.');
      const response=await chrome.runtime.sendNativeMessage(HOST,{op:'download',kind:'youtube',url,title:tab.title||''});
      if(!response?.ok)throw Error(response?.error||'Desktop helper did not accept the video.');
      return response;
    }
    if (message.op === 'ping' && ownPage) return await chrome.runtime.sendNativeMessage(HOST,{op:'ping'});
    throw Error('Unsupported operation.');
  })().then(reply).catch(error => reply({ok:false,error:error.message}));
  return true;
});
chrome.runtime.onInstalled.addListener(() => {
  chrome.contextMenus.create({id:'local-download',title:'Download video with Local Download Manager',contexts:['video','audio']});
});
chrome.contextMenus.onClicked.addListener(async (info,tab) => {
  if (info.menuItemId !== 'local-download') return;
  try {
    const record = await remember(tab.id,info.srcUrl,'',info.frameUrl || info.pageUrl,info.frameId);
    if (!record) await sendPage(tab.id,null,false);
    else await send(tab.id,record.id,info.frameUrl || info.pageUrl);
  } catch (error) {
    chrome.tabs.sendMessage(tab.id,{op:'status',error:error.message},{frameId:info.frameId || 0}).catch(()=>{});
  }
});
