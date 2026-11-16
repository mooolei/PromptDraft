# Changelog

## 1.3.0

- Fix stale border fragments around title, source and search inputs during window resizing; center single-line editors inside their frames.
- Add custom categories with expandable sidebar groups and persistent collapse state.
- Create, move, rename and remove categories while preserving all records, including archived and deleted items.
- Search category names and find entries inside collapsed groups without discarding saved collapse preferences.
- Widen the sidebar to 340 pixels, show two-line titles and expose full titles in tooltips.
- Preserve compatibility with existing unclassified records and add category, layout and paint regression tests.

## 1.2.2

- Preserve LF, CRLF and CR line endings when pasting into prompts and AI collections.
- Display existing LF-based records correctly without rewriting their stored content on load.
- Keep selection replacement and one-step Undo for keyboard and context-menu paste.
- Preserve ordinary line breaks in Markdown preview, while keeping code blocks, lists and tables intact.
- Add native Windows editor and persistence regression tests without touching the system clipboard.

## 1.2.1

- Fix sidebar repainting that erased neighboring list items.
- Add regression coverage for multi-row painting, scrolling, filtering and pin refreshes.

## 1.2.0

- Pin and unpin frequently used prompts and saved answers.
- Restore window bounds, maximized state, editor selection, scroll position and per-document view mode.
- Restore Markdown reading position and split-pane ratio.

## 1.1.0

- Add application and taskbar icons and update the interface.
- Support reading, editing and side-by-side Markdown views.
- Fix selection copying in Markdown preview; add formatted HTML and plain-text copying.

## 1.0.0

- Local prompt drafts, separate AI collections, search, autosave, archive and recycle bin.
- Offline Markdown preview and raw-text copying.
