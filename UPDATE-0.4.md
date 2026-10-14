# Dialog, history and Facebook story fixes

1. Close older Local Download Manager windows.
2. Open `bin\LocalDownload.v04.exe` in the workspace. The release ZIP uses `bin\LocalDownload.exe` for the updated version.
3. Reload **Local Download Integration** in `chrome://extensions`. Its version should be **0.4.0**.
4. Refresh Facebook story tabs as well: the new story capture script needs to load before Facebook fetches its story data. Play the story, then use **Download Facebook Story (MP4)**. Enable the signed-in session checkbox when required.

## Changes

- The Chrome native helper grants foreground activation to the desktop process. Save and progress dialogs restore a minimized app and briefly come to the front. They return to normal window behavior rather than staying always on top. Windows can still restrict keyboard focus in unusual desktop/window configurations; visibility and minimize restoration were exercised in the UI harness, not in your everyday Chrome session.
- Progress windows close after successful completion and after the transfer task finishes. They remain open on errors or when stopped. Viewing an already-completed row's progress explicitly keeps that window open. Error details now have a scrolling text box.
- Right-click a history row and choose **Redownload**. Double-click a completed row whose downloaded file is missing to start a fresh download. The engine resets its previous validator/progress and uses a fresh stream-assembly working directory. If the existing file is still present, a numbered filename preserves it. The original source page is retained for newly captured browser downloads, with **Open source in Chrome** available from the row's menu.
- Facebook stories use the active story's player/HTML/GraphQL media metadata rather than sending the unsupported story page to the generic extractor. The browser capture observes fetch/XHR responses without changing them, retains only bounded media URLs and video IDs, and matches the active story. It prefers Facebook's playable HD/SD URL; otherwise it tries the matching video ID through the existing page downloader. Facebook session cookies are not forwarded to unrelated/CDN domains. Photo-only, expired, protected or unrecognized stories return an actionable error rather than selecting another cached story's video.

## Verification and limits

`tests/ui-behavior.py` passed 11 checks covering visible dialogs, owner restoration, temporary foreground elevation, close-on-completion, retained error/stopped dialogs, fresh redownload requests, file identity and protection of an existing copy. `tests/facebook-story.mjs` passed controlled Chrome fixtures for active-story matching, fetch and XHR capture, video-ID fallback, same-site session handling, response integrity, rejection of unrelated media origins, and exclusion of raw/private response fields. Existing download-integrity, native framing/origin, media routing, popup consent and Chrome-to-host tests also passed.

The Facebook story path has not been verified against your live signed-in Facebook account. Refresh and retry the actual story after updating. Facebook can change its metadata schema, and unavailable/photo/protected stories remain unsupported.

Redownload uses the saved URL and any session credentials still held in the running app. Credentials are deliberately absent from persisted history. If a session or signed CDN URL has expired, use **Open source in Chrome** and send a fresh download with the appropriate session checkbox. History alone cannot restore an expired authenticated URL or a deleted/expired story.
