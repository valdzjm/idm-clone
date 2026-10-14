# Social videos and website downloads — version 0.3

1. Close every older Local Download Manager window.
2. In the existing workspace, open `bin\LocalDownload.v03.exe`. The title should say **Local Download Manager 0.3**. The release ZIP names the updated executable `bin\LocalDownload.exe`.
3. Open `chrome://extensions` and click Reload on **Local Download Integration**. Its version should be **0.3.0**. Refresh your video tabs.
4. Open a video, reel or Short in its own page. Click the extension icon, then its site-specific download button. The green **Download this video** button offers the same action. If the clip is inside a feed, open its permalink or paste its copied share link into the extension popup.
5. If it needs login, sign in on that site in the same Chrome tab and check **Use this site's signed-in Chrome session** before downloading. This option is off by default and resets when you change the URL. It transfers only cookies for the selected site and tab's cookie store. It does not log you in or collect account passwords. Cookies stay out of download history; the temporary current-user-only cookie file is deleted when downloading stops or finishes.
6. Choose a save location in the desktop dialog and click **Start Download**. Wait for **Complete**. Choose **Open Folder** to find the MP4.

For phone/social apps, copy the video's share link, then paste it into the Chrome extension or the desktop app's **Add URL** dialog. This Windows app does not integrate directly into Android or iOS apps. Desktop Add URL runs without the Chrome session checkbox; use the extension for signed-in videos.

## Available routes

- YouTube watch, Shorts, live-page links and youtu.be share links.
- Facebook reels, watch/video URLs, `fb.watch` links and video/reel share links.
- Instagram reels (`/reel` and `/reels`), posts with video, IGTV links and stories.
- Other HTTP/HTTPS pages supported by yt-dlp, plus direct detected MP4/WebM files and detected HLS/DASH manifests.

The downloader processes the first video when a link contains a collection/carousel. An Instagram story username URL without a story ID selects the first available story. Use the individual story URL when possible.

## Actual sample results, 2026-10-12

| User's sample | Result through compiled desktop engine |
|---|---|
| YouTube Short `TlRlDNHdmvk` | MP4 downloaded; AV1 video + AAC audio; 24.17 seconds |
| Instagram reel `DdgUlmZikAU` | MP4 downloaded; VP9 video + AAC audio; 20.99 seconds |
| Facebook reel `1822430045852437` | MP4 downloaded; AV1 video + AAC audio; 32.23 seconds |
| Facebook video `1471882738092739` | MP4 downloaded; AV1 video + AAC audio; 25.17 seconds |
| Instagram stories for `jdwnsllz` | Public-session extraction rejected with login required. Signed-in download was not verified with your account. |
| Facebook story for `1986321208045496` | Redirected to login in the public test session. The URL is accepted, but successful extraction was not verified; Facebook story pages may still be unsupported after login. Try a detected direct media link if page extraction fails. |

Test evidence is in `tests/output/social-probes.json` and `tests/output/social-download-verification.json`. Downloaded test videos are in `tests/output/`. The generic extraction/session test passed against an owned local fixture, including byte identity, cookie forwarding, deletion and exclusion from history. The actual Chrome popup test verified site labels, URL normalization, explicit cookie opt-in, cross-site cookie isolation and feed rejection. These tests do not establish coverage of every video on a platform.

Playability alone does not guarantee downloading: DRM, site restrictions, expired/unavailable stories, missing session access, unsupported extractors or changes to a website can prevent it. The maintained engine lists supported [Facebook and Instagram extractors and other sites](https://github.com/yt-dlp/yt-dlp/blob/master/supportedsites.md), while noting that actual support must be checked with a live URL. Update it using `setup-video-tools.ps1` when needed.

The output is an MP4 container; selected video codecs can be AV1 or VP9. If Windows' player lacks that codec, use a player that supports it. Downloads can be stopped and resumed; after restarting the desktop app, send a fresh request from Chrome if the download requires login, since session cookies are not saved.
