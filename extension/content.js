(async () => {
  const {pageInfo}=await import(chrome.runtime.getURL('media.js'));
  const attached = new WeakSet();
  function notify(text) {
    const box=document.createElement('div');box.textContent=text;
    Object.assign(box.style,{position:'fixed',right:'20px',top:'20px',zIndex:'2147483647',padding:'12px',maxWidth:'420px',background:'#fff',color:'#222',border:'1px solid #888',font:'13px Segoe UI, sans-serif'});
    document.documentElement.append(box);setTimeout(()=>box.remove(),7000);
  }
  chrome.runtime.onMessage.addListener(msg=>{if(msg.op==='status')notify(msg.error);});
  function attach(video) {
    if(attached.has(video))return;attached.add(video);
    const host=document.createElement('div');const shadow=host.attachShadow({mode:'closed'});
    const button=document.createElement('button');button.textContent='↓ Download this video';
    button.style.cssText='all:initial;display:block;cursor:pointer;padding:5px 12px;border:1px solid #729063;border-radius:3px;background:linear-gradient(#f4ffeb,#c9e7b3);color:#143300;font:12px Segoe UI,sans-serif;box-shadow:0 1px 3px #777';
    const choices=document.createElement('div');choices.style.cssText='display:none;max-width:330px;max-height:240px;overflow:auto;background:white;border:1px solid #888;padding:6px;color:#222;font:12px Segoe UI,sans-serif';
    shadow.append(button,choices);document.documentElement.append(host);
    Object.assign(host.style,{position:'fixed',zIndex:'2147483646'});
    let visible=false;
    const observer=new IntersectionObserver(entries=>{visible=entries[0].isIntersecting;position();});observer.observe(video);
    function position(){const r=video.getBoundingClientRect();host.style.display=visible&&r.width>160&&r.height>80?'block':'none';host.style.left=Math.max(0,Math.min(innerWidth-210,r.right-210))+'px';host.style.top=Math.max(0,r.top+8)+'px';}
    const interval=setInterval(()=>{if(!video.isConnected){clearInterval(interval);observer.disconnect();removeEventListener('scroll',position);host.remove();return;}position();},750);
    addEventListener('scroll',position,{passive:true});
    async function discover(){const url=video.currentSrc||video.src;if(/^https?:/.test(url))await chrome.runtime.sendMessage({op:'discover',url,mime:video.querySelector('source')?.type||'video/mp4'});}
    video.addEventListener('loadedmetadata',()=>discover().catch(()=>{}));discover().catch(()=>{});
    button.addEventListener('click',async event=>{
      if(!event.isTrusted)return;event.preventDefault();event.stopPropagation();
      choices.replaceChildren();choices.style.display='block';
      try{
        let info=pageInfo(location.href);
        if(!info){const article=video.closest('article,[role="article"]');if(article)for(const link of article.querySelectorAll('a[href]')){const candidate=pageInfo(link.href);if(candidate&&candidate.site!=='Website'){info=candidate;break;}}}
        if(info){
          const label=document.createElement('label');const session=document.createElement('input');session.type='checkbox';label.append(session,' Use this site’s signed-in session');label.style.cssText='display:block;font-size:11px;margin:4px';
          const item=document.createElement('button');item.textContent=info.label;item.style.cssText='padding:8px;cursor:pointer';
          item.addEventListener('click',async e=>{if(!e.isTrusted)return;item.disabled=true;try{const response=await chrome.runtime.sendMessage({op:'page',url:info.url,useSession:session.checked});if(!response.ok)throw Error(response.error);notify(response.message);choices.style.display='none';}catch(error){notify(error.message);}finally{item.disabled=false;}});
          choices.append(item,label);
        }else{
          const note=document.createElement('p');note.textContent='Open this video/reel in its own page, or paste its share link into the extension popup.';choices.append(note);
        }
        await discover();const result=await chrome.runtime.sendMessage({op:'list'});if(!result.ok)throw Error(result.error);
        const media=result.media.filter(m=>m.frameId===undefined||m.frameId===0||m.url===video.currentSrc);
        const items=media.length?media:result.media;
        if(!items.length)return;
        for(const m of items){
          const item=document.createElement('button');item.textContent=(m.kind==='manifest'?'[HLS/DASH] ':m.kind==='stream'?'[YouTube track] ':m.mime?.startsWith('audio/')?'[Audio] ':'')+new URL(m.url).pathname.split('/').pop().slice(0,55);item.title=m.url;
          item.style.cssText='display:block;width:100%;margin:3px 0;padding:6px;text-align:left;background:#eee;border:1px solid #aaa;cursor:pointer;color:#222';
          item.addEventListener('click',async e=>{if(!e.isTrusted)return;item.disabled=true;try{const response=await chrome.runtime.sendMessage({op:'download',id:m.id});if(!response.ok)throw Error(response.error);notify(response.message);choices.style.display='none';}catch(error){notify(error.message);}finally{item.disabled=false;}});choices.append(item);
        }
      }catch(error){choices.textContent=error.message;}
    });
  }
  function scan(){document.querySelectorAll('video').forEach(attach);}
  let scheduled=false;new MutationObserver(()=>{if(!scheduled){scheduled=true;setTimeout(()=>{scheduled=false;scan();},250);}}).observe(document.documentElement,{childList:true,subtree:true});scan();
})();
