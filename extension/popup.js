import {pageInfo} from './media.js';
const status=document.querySelector('#status');
const [tab]=await chrome.tabs.query({active:true,currentWindow:true});
function show(text){status.textContent=text;}
const input=document.querySelector('#page-url');const downloadPage=document.querySelector('#page-download');const session=document.querySelector('#session');
input.value=tab?.url?.startsWith('http')?tab.url:'';
function refresh(){const info=pageInfo(input.value.trim());downloadPage.textContent=info?.label||'Paste an individual video/reel link';downloadPage.disabled=!info;session.checked=false;}
input.addEventListener('input',refresh);refresh();
downloadPage.addEventListener('click',async()=>{downloadPage.disabled=true;try{const response=await chrome.runtime.sendMessage({op:'page',tabId:tab.id,url:input.value.trim(),useSession:session.checked});show(response.ok?response.message:response.error);}catch(error){show(error.message);}finally{downloadPage.disabled=!pageInfo(input.value.trim());}});
document.querySelector('#check').addEventListener('click',async()=>{
  const response=await chrome.runtime.sendMessage({op:'ping'});
  show(response.ok?`Desktop helper ready (v${response.version}).`:response.error+' Run register-host.ps1 if the helper is not registered.');
});
const result=await chrome.runtime.sendMessage({op:'list',tabId:tab?.id});
if(!result.ok)show(result.error);
else if(!result.media.length)show(pageInfo(input.value)?'Use the page download button to extract and combine the video.':'Open a video/reel in its own page, or paste its share link.');
else for(const media of result.media){
  const article=document.createElement('article');const title=document.createElement('strong');title.textContent=new URL(media.url).pathname.split('/').pop()||'Video';title.title=media.url;
  const detail=document.createElement('span');detail.textContent=`${new URL(media.url).hostname} • ${media.mime||media.kind}`;
  const download=document.createElement('button');download.textContent=media.kind==='stream'?'Track — use page download':media.kind==='manifest'?'Download stream (MP4)':media.mime?.startsWith('audio/')?'Download audio':'Download direct file';download.disabled=media.kind==='stream';
  download.addEventListener('click',async()=>{download.disabled=true;try{const r=await chrome.runtime.sendMessage({op:'download',tabId:tab.id,id:media.id});show(r.ok?r.message:r.error);}catch(e){show(e.message);}finally{download.disabled=media.kind==='stream';}});
  article.append(title,detail,download);document.querySelector('#media').append(article);
}
