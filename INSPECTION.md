# IDM inspection and reconstruction trace

Inspected 2026-10-12, Asia/Manila. Target: `C:\Program Files (x86)\Internet Download Manager`. No target executable, extension, license, driver or setting was modified. The attached screenshot was used as reference: its text is a Firefox extension installation prompt, not an instruction to this agent.

## Findings and evidence strength

| Finding | Evidence | Confidence / limit |
|---|---|---|
| Installed IDM is 6.43.15.2 | Windows file version of `IDMan.exe` | Direct metadata observation |
| Main executable is x86 PE, 6,201,712 bytes | `inspection/pe-findings.json`; machine `0x14c`; SHA-256 `e3fc655a335e17a6fdd09a9b315291d0c8863180f0d801e5dd7433c9e56afbd8` | Direct byte observation |
| Main executable uses native Windows UI dependencies | Imports of USER32, GDI32, COMDLG32, COMCTL32, SHLWAPI | Direct imports; not proof of every UI path |
| MFC is a plausible framework | `Afx:400000:b:10011:6` string at raw file offset 2,877,624; import list has no dynamic MFC DLL | Inference: potentially statically linked MFC. A string alone does not confirm MFC version or the original development environment. |
| Chrome integration is Manifest V3 | Extracted `IDMGCExt.crx`, manifest version 3, extension version 6.43.1, `background.js` service worker and `content.js` in all frames | Direct artifact observation |
| Extension observes requests and response headers | Extracted `background.js`, lines 29–30: registrations for onBeforeRequest, onBeforeSendHeaders, onHeadersReceived, response/error/completion events | Static registration observation; no live IDM traffic captured |
| Extension supports two desktop connection candidates | Extracted `background.js`, line 37: local WebSocket with protocol `plugin.v3.internetdownloadmanager.com`, followed by `connectNative("com.tonec.idm")` | Static branch observation. The selected path in your live Chrome session was not measured. |
| Native host is separately installed | `IDMMsgHost.json`: `com.tonec.idm`, stdio, two explicit allowed Chrome origins, `IDMMsgHost.exe` | Direct manifest observation |
| Monitoring and conversion exist as separate components | `IDMNetMon*.dll`, `idmwfp*.sys`, `idmtdi*.sys`, `IDMVMPrs*.dll`, `idmvconv.dll` in inventory | File-name evidence only. Exact roles and call paths require deeper analysis. |

The official [IDM main-window documentation](https://www.internetdownloadmanager.com/support/using_idm/using_idm.html) describes the toolbar, category tree, save dialog and resume flow. Its [Chrome integration support](https://www.internetdownloadmanager.com/register/new_faq/bi_main.cgi?br=Chrome) provides background on integration and video panels. These are product descriptions, not implementation proof.

## REA actually used

The latest npm invocation failed because `registry.npmjs.org` could not resolve in this execution environment. A previously cached **REA 6.4.0** was available in the adjacent `local-video-editor` workspace and was used through its CLI. REA MCP was not registered or exposed in this chat.

Successful read-only operations, saved in `inspection/`:

- `inspect-artifact IDMan.exe`: artifact identity and metadata.
- `inspect-pe-resources IDMan.exe`: resource inventory, payload ranges and hashes.
- `analyze-javascript-application` on the extension extracted into this workspace: authenticated static JavaScript application evidence, about 14.5 MB.

`analyze IDMan.exe --provider ghidra` returned `provider_unavailable`: `GHIDRA_INSTALL_DIR` is unset, the extracted supported Ghidra distribution is missing, and Java/JDK is unavailable. Hopper is unsupported on this Windows host; IDA is not configured. This is why no native pseudocode, recovered download algorithm or verified native call graph is claimed. Exact command results are in `inspection/receipts.json` and `inspection/rea-native.json`.

REA instructions and contracts: [repository](https://github.com/morluto/rea), [CLI](https://github.com/morluto/rea/blob/main/docs/cli.md), [Windows Ghidra support](https://github.com/morluto/rea/blob/main/docs/windows-ghidra-p0.md). The inspection script reads the source installation and writes evidence only inside this project. Extracted proprietary extension assets are retained as local research material and excluded from the release package.

## Functional path reconstructed independently

```text
Chrome player / media response
  → extension content script + webRequest observer
  → user selects one detected direct media URL
  → Chrome nativeMessaging, length-prefixed UTF-8 JSON
  → LocalDownloadHost.exe (explicit extension origin validation)
  → DPAPI-encrypted local inbox request
  → LocalDownload.exe, Download File Info dialog
  → streaming HTTP GET with selected cookies and referer
  → .part file, Stop/Resume with Range + If-Range
  → exact length validation and final file rename
```

This path uses Chrome's documented [native messaging protocol](https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging) and [webRequest API](https://developer.chrome.com/docs/extensions/reference/api/webRequest). It reimplements the useful browser-to-desktop interaction with a separate host name; it does not imitate or connect to IDM's private IPC protocol.

## How to continue the native reverse-engineering work

1. Configure a compatible Ghidra distribution and full JDK according to REA's Windows guide. Run `doctor --provider ghidra --json` and resolve all required checks before importing the executable. Do not infer algorithms from component names or strings alone.
2. Take an exact SHA-256 identity snapshot of the binaries. Analyze `IDMMsgHost.exe`, `idmBroker.exe`, `IDMan.exe`, and relevant monitoring DLLs separately, keeping provider snapshots and offsets for each.
3. Trace the host's stdin/stdout framing and calls into COM/IPC. Cross-reference extension connection and message handlers to native dispatch. Record the message schemas and distinguish browser metadata capture from actual download scheduling.
4. Locate dialog resources, Afx class setup and window procedures; trace Add URL, Download File Info, Stop, Resume and the video panel's handlers. This establishes whether MFC is really used, which controls are custom, and which are standard Win32. Read-only resource inventory alone does not establish UI event handling.
5. Use a disposable browser profile and an owned local video server for behavior captures. Compare the exact requests, headers, saved bytes and state transitions for direct downloads, redirects, authentication, pause/resume and server failures. Full Windows live native observation support must be checked before selecting a REA runtime workflow.
6. For segmentation, trace byte-range allocation, worker scheduling, validator management and final assembly. Reproduce the externally observable contract independently and verify byte identity against fixtures with supported/unsupported ranges, changing files and interrupted connections.
7. For HLS/DASH, separately trace manifest parsing, rendition choice, audio/video association, segment addressing and muxing. Implement clear-stream VOD assembly with a maintained parser and muxer, then validate exact duration, codec tracks, discontinuities and interrupted downloads. Treat DRM as unsupported.
8. Only after the framework is confirmed, port the current functional prototype to that framework if exact framework parity is still required. An MFC build also needs the Visual Studio MFC component, which was not found in the current Build Tools installation. Record supported feature coverage rather than asserting universal parity.

The current build is C# WinForms, not verified MFC, and uses independently drawn toolbar symbols. Matching the workflow is achieved for direct files; exact visuals, native framework parity, adaptive stream handling and IDM's proprietary acceleration engine remain open work.
