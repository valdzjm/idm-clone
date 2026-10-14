import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
// Import as a data module because the parent monorepo does not own this extension.
const {classify,validPage,youtubePage,pageInfo}=await import('data:text/javascript;base64,'+Buffer.from(readFileSync(new URL('../extension/media.js',import.meta.url))).toString('base64'));
assert.equal(classify('https://cdn.example/video.mp4?token=a'),'file');
assert.equal(classify('https://cdn.example/videoplayback','video/webm'),'file');
assert.equal(classify('https://cdn.example/stream.m3u8'),'manifest');
assert.equal(classify('https://cdn.example/stream','application/dash+xml'),'manifest');
assert.equal(classify('https://cdn.example/segment.ts','video/mp2t'),null);
assert.equal(classify('file:///C:/secret.mp4'),null);
assert.equal(classify('https://user:password@example.com/video.mp4'),null);
assert.equal(validPage('javascript:alert(1)'),false);
assert.equal(classify('https://www.youtube.com/sounds/open.mp3','audio/mpeg'),null);
assert.equal(classify('https://www.youtube.com/sounds/failure.mp3','audio/mpeg'),null);
assert.equal(classify('https://rr1.googlevideo.com/videoplayback?mime=video%2Fmp4','application/vnd.yt-ump'),'stream');
assert.equal(youtubePage('https://www.youtube.com/watch?v=JIIBErjcyHU&list=anything'),'https://www.youtube.com/watch?v=JIIBErjcyHU');
assert.equal(youtubePage('https://youtu.be/JIIBErjcyHU'),'https://www.youtube.com/watch?v=JIIBErjcyHU');
assert.equal(youtubePage('https://www.youtube.com.evil.example/watch?v=JIIBErjcyHU'),null);
assert.equal(youtubePage('https://www.youtube.com/watch?v=bad'),null);
const samples=[
 ['https://www.youtube.com/shorts/TlRlDNHdmvk','YouTube','https://www.youtube.com/watch?v=TlRlDNHdmvk'],
 ['https://www.instagram.com/reels/DdgUlmZikAU/','Instagram','https://www.instagram.com/reel/DdgUlmZikAU/'],
 ['https://www.instagram.com/stories/jdwnsllz/','Instagram','https://www.instagram.com/stories/jdwnsllz/'],
 ['https://www.facebook.com/stories/1986321208045496/UzpfSVNDOjE0MDU5MzU1NDE3NDk2NzQ=/?bucket_count=9','Facebook',null],
 ['https://www.facebook.com/reel/1822430045852437','Facebook','https://www.facebook.com/reel/1822430045852437/'],
 ['https://www.facebook.com/61590365856864/videos/pcb.122137551111345528/1471882738092739','Facebook',null],
 ['https://m.facebook.com/watch/?v=123456&tracking=yes','Facebook','https://www.facebook.com/watch/?v=123456'],
 ['https://www.instagram.com/p/ABC_123/','Instagram','https://www.instagram.com/p/ABC_123/'],
 ['https://example.com/video/article','Website','https://example.com/video/article']
];
for(const [url,site,canonical] of samples){const info=pageInfo(url);assert.equal(info?.site,site,url);if(canonical)assert.equal(info.url,canonical,url);}
for(const bad of ['file:///C:/Windows/win.ini','javascript:alert(1)','https://user:pass@example.com/video','https://www.instagram.com/','https://www.facebook.com/','https://www.youtube.com/','https://www.instagram.com.evil.example/reel/ABC/'])assert.equal(pageInfo(bad),null,bad);
console.log('PASS media classification, URL safety, and all 6 supplied social link routes');
