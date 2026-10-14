export function classify(url, mime = '') {
  try {
    const parsed = new URL(url);
    if (!['http:', 'https:'].includes(parsed.protocol) || parsed.username || parsed.password) return null;
    const path = parsed.pathname.toLowerCase();
    if ((parsed.hostname === 'youtube.com' || parsed.hostname.endsWith('.youtube.com')) && /\/(open|failure|no_input|success)\.mp3$/.test(path)) return null;
    if (parsed.hostname.endsWith('.googlevideo.com') && path === '/videoplayback') return 'stream';
    mime = mime.toLowerCase().split(';')[0].trim();
    if (/\.(m3u8|mpd)$/.test(path) || /mpegurl|dash\+xml/.test(mime)) return 'manifest';
    if (/\.(mp4|webm|mkv|mov|m4v|mp3|m4a|ogg|wav)$/.test(path) || (/^(video|audio)\//.test(mime) && !/mp2t/.test(mime))) return 'file';
  } catch {}
  return null;
}
export function youtubePage(url) {
  try {
    const parsed=new URL(url);
    if(!['http:','https:'].includes(parsed.protocol)||parsed.username||parsed.password)return null;
    let id;
    if(['youtube.com','www.youtube.com','m.youtube.com','music.youtube.com'].includes(parsed.hostname)) {
      id=parsed.pathname==='/watch'?parsed.searchParams.get('v'):parsed.pathname.match(/^\/(?:shorts|live)\/([\w-]{11})(?:\/|$)/)?.[1];
    } else if(parsed.hostname==='youtu.be') id=parsed.pathname.slice(1);
    return /^[\w-]{11}$/.test(id||'')?`https://www.youtube.com/watch?v=${id}`:null;
  }catch{return null;}
}
export function validPage(url) {
  try { const u=new URL(url);return ['http:', 'https:'].includes(u.protocol)&&!u.username&&!u.password; } catch { return false; }
}
export function pageInfo(url) {
  if(!validPage(url))return null;
  const u=new URL(url);const youtube=youtubePage(url);
  if(youtube)return {url:youtube,site:'YouTube',domain:'youtube.com',label:/\/shorts\//.test(u.pathname)?'Download YouTube Short (MP4)':'Download YouTube video (MP4)'};
  const host=u.hostname.toLowerCase();const path=u.pathname;
  if(['youtube.com','www.youtube.com','m.youtube.com','music.youtube.com','youtu.be'].includes(host))return null;
  if(['facebook.com','www.facebook.com','m.facebook.com','mbasic.facebook.com','web.facebook.com','fb.watch'].includes(host)) {
    if(host==='fb.watch'&&/^\/[\w-]+\/?$/.test(path))return {url:`https://fb.watch${path}`,site:'Facebook',domain:'facebook.com',label:'Download Facebook video (MP4)'};
    let match=path.match(/^\/reel\/(\d+)\/?$/);if(match)return {url:`https://www.facebook.com/reel/${match[1]}/`,site:'Facebook',domain:'facebook.com',label:'Download Facebook Reel (MP4)'};
    match=path.match(/^\/share\/(?:v|r)\/([\w-]+)\/?$/);if(match)return {url:`https://www.facebook.com${path}`,site:'Facebook',domain:'facebook.com',label:'Download Facebook video/reel (MP4)'};
    const id=u.searchParams.get('v')||u.searchParams.get('video_id');
    if(['/watch','/watch/','/video.php'].includes(path)&&/^\d+$/.test(id||''))return {url:`https://www.facebook.com/watch/?v=${id}`,site:'Facebook',domain:'facebook.com',label:'Download Facebook video (MP4)'};
    if(/^\/(?:[^/]+\/)?videos\/(?:[^/]+\/)?\d+\/?$/.test(path))return {url:`https://www.facebook.com${path}`,site:'Facebook',domain:'facebook.com',label:'Download Facebook video (MP4)'};
    if(/^\/stories\/\d+\/[A-Za-z0-9_=%-]+\/?$/.test(path))return {url:`https://www.facebook.com${path}${u.search}`,site:'Facebook',domain:'facebook.com',label:'Download Facebook Story (MP4)'};
    return null;
  }
  if(['instagram.com','www.instagram.com','m.instagram.com'].includes(host)) {
    if(/^\/stories\/[A-Za-z0-9_.]+\/(?:\d+\/?)?$/.test(path))return {url:`https://www.instagram.com${path}`,site:'Instagram',domain:'instagram.com',label:/\/\d+\/?$/.test(path)?'Download Instagram Story (MP4)':'Download first Instagram Story (MP4)'};
    const match=path.match(/^\/(p|reel|reels|tv)\/([\w-]+)\/?$/);
    if(!match)return null;
    const type=match[1]==='reels'?'reel':match[1];
    return {url:`https://www.instagram.com/${type}/${match[2]}/`,site:'Instagram',domain:'instagram.com',label:type==='reel'?'Download Instagram Reel (MP4)':'Download Instagram video (MP4)'};
  }
  if(/(?:^|\.)(?:youtube\.com|facebook\.com|instagram\.com)\./.test(host))return null;
  u.hash='';return {url:u.href,site:'Website',domain:host,label:'Download video from this page (MP4)'};
}
