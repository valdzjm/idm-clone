(function installFacebookCapture() {
  if(window.__localDownloadStoryCapture)return;
  function mediaUrl(value) {
    try{const u=new URL(value);return u.protocol==='https:'&&!u.username&&!u.password&&['fbcdn.net','fbsbx.com','facebook.com'].some(host=>u.hostname===host||u.hostname.endsWith('.'+host))?u.href:null;}catch{return null;}
  }
  function scan(data) {
    const found=[];const stack=[{value:data,ids:[],depth:0}];let budget=100000;
    while(stack.length&&--budget>0){
      const {value,ids,depth}=stack.pop();if(!value||typeof value!=='object'||depth>70)continue;
      let scoped=ids;
      if(!Array.isArray(value)){
        const keys=['id','story_id','storyID','story_card_id'];const added=keys.map(k=>value[k]).filter(v=>typeof v==='string'&&v.length<256);
        scoped=[...ids,...added].slice(-20);
        const hd=mediaUrl(value.browser_native_hd_url||value.playable_url_quality_hd||value.hd_src||value.hd_url);
        const sd=mediaUrl(value.browser_native_sd_url||value.playable_url||value.sd_src||value.sd_url);
        const isVideo=value.__typename==='Video'||hd||sd;
        if(isVideo){const videoId=/^\d+$/.test(value.id||'')?value.id:null;if(hd||sd||videoId)found.push({ids:scoped,videoId,hd,sd,time:Date.now()});}
      }
      for(const child of Object.values(value))if(child&&typeof child==='object')stack.push({value:child,ids:scoped,depth:depth+1});
    }
    return found.slice(0,100);
  }
  const state={records:[],scan,mediaUrl};window.__localDownloadStoryCapture=state;
  function collect(text){
    if(!location.pathname.startsWith('/stories/')||text.length>4*1024*1024)return;
    for(let line of text.replace(/^for\s*\(;;\);\s*/,'').split('\n')){try{const records=scan(JSON.parse(line));state.records=[...records,...state.records].slice(0,100);}catch{}}
  }
  const original=window.fetch;
  window.fetch=function(...args){
    const promise=Reflect.apply(original,this,args);
    promise.then(async response=>{
      if(!location.pathname.startsWith('/stories/')||!response.url.includes('/api/graphql'))return;
      try{
        const reader=response.clone().body.getReader();const decoder=new TextDecoder();let text='';let bytes=0;
        while(true){const chunk=await reader.read();if(chunk.done)break;bytes+=chunk.value.byteLength;if(bytes>4*1024*1024){reader.cancel().catch(()=>{});return;}text+=decoder.decode(chunk.value,{stream:true});}
        collect(text+decoder.decode());
      }catch{}
    }).catch(()=>{});return promise;
  };
  const originalSend=XMLHttpRequest.prototype.send;
  XMLHttpRequest.prototype.send=function(...args){
    this.addEventListener('load',()=>{
      if(!location.pathname.startsWith('/stories/')||!this.responseURL.includes('/api/graphql'))return;
      try{if(this.responseType==='json')collect(JSON.stringify(this.response));else if(this.responseType===''||this.responseType==='text')collect(this.responseText);}catch{}
    },{once:true});return Reflect.apply(originalSend,this,args);
  };
})();
