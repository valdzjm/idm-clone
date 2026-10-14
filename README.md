# Local Download Manager

**Current version: 0.4.** Follow the source checkout setup below, then reload the Chrome extension to 0.4.0. An existing workspace may also have `bin\LocalDownload.v04.exe`. See [UPDATE-0.4.md](UPDATE-0.4.md) for dialog focus/auto-close, history redownload and browser-assisted Facebook stories. The later 0.3 sections document the earlier rollout; the setup below and 0.4 guide take precedence.

## Set up from this repository

The [v0.4.0 release](https://github.com/valdzjm/idm-clone/releases/tag/v0.4.0) provides the compiled Windows app and Chrome extension. Extract the package, run `setup-video-tools.ps1` and `register-host.ps1`, then load its `extension` folder in Chrome. To build from source, use the commands below.

Use Windows with .NET Framework 4.x, Node.js, Python, and RTK installed. Social-page downloads also require FFmpeg and Node on PATH.

```powershell
git clone https://github.com/valdzjm/idm-clone.git
cd idm-clone
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .\setup-video-tools.ps1
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .\register-host.ps1
```

Open `bin\LocalDownload.exe`. In Chrome, go to `chrome://extensions`, enable **Developer mode**, choose **Load unpacked**, and select this checkout's `extension` folder. Refresh your video tabs and use **Local Download Integration** to send a download to the desktop app.

Generated executables, local native-host paths, downloaded tools, download history, browser profiles, test media, and raw machine inspection output are excluded from Git. The build generates `bin\host.json` for your checkout; rerun `register-host.ps1` if you move it. The scripts and app source are included.

A runnable personal downloader for Windows and Chrome, built after read-only inspection of the installed IDM 6.43.15.2. Version 0.3 adds social-video/reel routes, generic website/stream extraction and optional same-site Chrome session forwarding. See [SOCIAL-SETUP.md](SOCIAL-SETUP.md) for the current setup guide and actual results from the supplied sample links. This is **not an exact IDM clone**. It uses C#/.NET Framework WinForms and native Windows controls. IDM's `Afx` marker suggests MFC; the exact framework/version has not been established by decompilation.

## Update to 0.3

Close the old desktop app. In the workspace copy, run `bin\LocalDownload.v03.exe`; the title should say **Local Download Manager 0.3**. A separate executable was built because the original was open and locked during the update. The release ZIP uses the standard `LocalDownload.exe` name for the new version.

Open `chrome://extensions` and click the circular Reload arrow on **Local Download Integration**. Refresh the YouTube video tab. Open the extension popup and use its site-specific download button, or paste a video/reel share link. The green player button offers the same action. Do not choose a raw audio or video track if you want the complete video.

YouTube tools have already been installed and checksum-verified for the workspace copy, using the official yt-dlp release and this machine's existing FFmpeg and Node. For another folder or machine, run `setup-video-tools.ps1` and `register-host.ps1`. Page downloading requires FFmpeg and Node on PATH. The optional session checkbox transfers only selected-site cookies for that request. It does not bypass sign-in, DRM, paywalls or geographic restrictions.

Verified against the user-supplied public video `JIIBErjcyHU`: downloaded MP4, 103,685,527 bytes, 239.258 seconds, VP9 video and AAC audio. Evidence is in `tests/output/youtube-verification.json`; the downloaded test video is `tests/output/FlipTop-test.mp4`.

## Start using it

1. Run `bin\LocalDownload.v03.exe` if present, otherwise `bin\LocalDownload.exe`.
2. In Chrome open `chrome://extensions`, enable **Developer mode**, choose **Load unpacked**, and select this project's `extension` folder. The stable extension ID is in `extension-id.txt`.
3. Register the desktop bridge by running `register-host.ps1` in PowerShell. It writes only the current-user Chrome native messaging registration for `com.localdownload.manager`. The bridge has already been registered for the current workspace copy.
4. Pin **Local Download Integration** to Chrome's toolbar. Reload any tabs opened before loading the extension.
5. Play a video. Click **Download this video**, then select a detected direct media URL. Alternatively use the extension popup or a video's right-click menu.
6. In **Download File Info**, choose the save path and click **Start Download**. **Download Later** adds it to the list. Double-click a row for progress; use **Stop** and **Resume** as needed.

The default save folder is `bin\Downloads`. Keep the app in a writable folder. The desktop app can also download direct URLs using **Add URL**, without Chrome.

If you move or unpack the app into another folder, rerun `register-host.ps1`; it updates the host executable path. Remove the extension through Chrome and run `unregister-host.ps1` to remove the bridge. No IDM files or settings are changed.

## What works

- HTTP/HTTPS direct files, including MP4, WebM, audio and other file downloads.
- YouTube watch/shorts/live page URLs resolved through yt-dlp, with available MP4 video and M4A audio downloaded and combined using FFmpeg. MP4 container output does not guarantee H.264 codec; the best available MP4-compatible video may use VP9 or AV1.
- YouTube's `open.mp3`, `failure.mp3`, `no_input.mp3` and `success.mp3` interface sounds are excluded from the media list. Raw Googlevideo adaptive endpoints are labeled as tracks and directed to the complete-video action.
- A native toolbar, category tree, download list, save dialog and progress windows following IDM's main interaction pattern.
- Cancellation, byte-range resume, validator checks and safe restart when a server ignores Range.
- Detection of direct video elements and observed media response headers in Chrome.
- Selected-URL cookies and page referer passed to the desktop engine for session-dependent direct URLs. Cookies are collected only when you explicitly request a download. Browser request packets are encrypted with Windows current-user DPAPI and deleted after the desktop reads them; cookies are not saved in download history.
- Persistent download list and partial files. After restarting, authenticated downloads may require capturing a fresh URL/session in Chrome.

## Known gaps

- Generic pages and detected HLS/DASH streams are now attempted through yt-dlp. Some websites, private/expired stories and protected streams remain unsupported. Rerun `setup-video-tools.ps1` to update the maintained extraction engine.
- DRM/decryption, access-control bypass and license handling are not implemented.
- Parallel segmentation, IDM's native network drivers, scheduler and queue engine are not implemented.
- It is not a pixel-exact UI reproduction and does not use verified IDM source code or its confirmed framework. Native dialogs follow Windows' theme and DPI settings.
- The current build was verified against the supplied public YouTube Short, Instagram reel, Facebook reel and Facebook video. Both story samples required login and remain unverified with your account. On a page with several players, the media list may contain several candidates rather than an exact player-to-stream association. Fullscreen overlay placement is not implemented; use the extension popup if needed.
- Chrome developer-mode installation requires the one-time **Load unpacked** action. The test profile successfully loaded the extension; it has not been silently installed in your everyday Chrome profile.

## Build and verify

Windows with .NET Framework 4.x and its C# compiler, Node.js, Python and RTK:

```powershell
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
rtk proxy python .\tests\verify.py
rtk proxy node .\tests\media.mjs
rtk proxy node .\tests\browser.mjs
```

The browser test requires Playwright and its Chromium binary from the parent workspace. Direct-file downloads need no Node or Python installation; page/stream mode needs yt-dlp, FFmpeg and Node. The main engine test uses a local HTTP fixture: integrity, cancellation, Range resume, ignored Range, changed ETag, malformed Content-Range, redirect cookie isolation, HTML rejection, native messaging framing/origin validation, and unsupported URL rejection. The media test covers URL classification, sound filtering and safe YouTube URL normalization. The browser test verifies extension loading, media capture, the video overlay and a real Chrome-to-native-host ping. The YouTube integration test downloads the user-supplied video through the compiled engine and verifies both tracks using ffprobe. A completed download driven through the everyday Chrome profile's save dialog still needs a user trial.

See [INSPECTION.md](INSPECTION.md) for evidence and the deeper reverse-engineering path.
