# Your first five minutes with ClearText

ClearText is a native Windows text reader and editor. A tutorial stays beside
your document, so you can learn while you work. Press **F1** to hide or restore
it, and **Back / Next** to move through its six steps.

## 1. Start the Windows app

Download the ZIP from [Releases](https://github.com/Anaghh/ClearText/releases),
extract it, and double-click **ClearText.exe**. You need Windows x64 with
.NET Framework 4.8. No Python or browser is involved. This preview is unsigned;
check its source and release checksum before deciding whether to run it.

## 2. Try a practice document

Choose **Practice space** in the left rail, or **Open practice document** in the
guide. A sample opens in a new tab without touching any existing file.

**Ctrl+N** creates a blank document. **Ctrl+O** opens one or several files using
the Windows file picker. An already-open file is selected instead of duplicated.

## 3. Make reading comfortable

- **Word wrap** fits long lines to the document width. Turn it off for line numbers
  and horizontal scrolling.
- **A− / A＋** adjust text size.
- **Switch theme** in the left rail changes light/dark appearance.
- **Read only** blocks typing in the selected document. Other tabs keep their own
  setting. This does not change Windows file permissions.

## Your reading desk

Turn on **Focus lens**, then click within a paragraph. Surrounding text fades
while that passage remains clear. It is a visual aid; all text remains accessible
for selection and editing.

Click **Sans** in the toolbar to cycle through **Book** and **Code**. Sans uses
Segoe UI, Book uses Georgia, and Code uses Consolas. Font size and theme choices
are remembered. The interface keeps a consistent Segoe UI typeface.

Choose **Reading map** in the left rail. It recognizes Markdown headings such as
`# Chapter one`, chapter/part/section titles, and short ALL CAPS headings. Click a
heading to jump there. Detection is heuristic and limited to 150 headings.

Press **Ctrl+B** to bookmark your current position. Click a bookmark to return,
then press Ctrl+B at that exact position to remove it. Up to 100 bookmarks are
supported per document. Save the file to persist bookmarks and reading position.
Unsaved documents and discarded edits do not persist reading marks.

Reopen a saved file manually to restore its position. If its bytes changed
outside the app, prior reading marks are ignored rather than applied to the
wrong passage. At the bottom, Position is the cursor percentage; reading time
is an estimate based on 220 whitespace-separated words per minute.

## 4. Search and navigate

Press **Ctrl+F**, type `ClearText`, and press Enter. While the search input has
focus, Enter advances to the next match and wraps at the end. You can also click
**Find next**. Search ignores case. Escape dismisses the search bar.

**Ctrl+G** opens a line-number prompt. Line numbers count logical document lines;
with wrap enabled, the bottom caret position can refer to a wrapped visual row.

## 5. Save your work

Press **Ctrl+S** to save. **Ctrl+Shift+S** opens Save as. A dot in the tab and an
asterisk in the window title mark changes. A closing prompt offers **Yes** to
save, **No** to discard, or **Cancel** to keep working. Failed saves retain edits.

If ClearText detects that the file changed outside the app, it asks before
replacing it. Choose No to retain your work, then use Save as to create a copy.
The check cannot eliminate every race with another application; avoid editing
the same file in two apps at once.

## 6. Encodings and limits

UTF-8 and BOM-marked UTF-16 are detected. UTF-16 byte order and BOM are preserved.
For other text encodings, you are asked for a name such as `windows-1252`.
New documents use UTF-8 without BOM and Windows CRLF line endings. Existing
files retain their detected newline style; mixed newline styles are normalized.

Files are limited to 20 MB. File reads run in the background; rendering, search,
and saving can still pause for dense documents. There is no crash recovery,
automatic tab reopening in this preview. Reading positions for saved files
are retained locally, but unsaved document contents are not backed up.

## Troubleshooting

If the app will not start, check the x64 / .NET Framework 4.8 requirements.
If saving fails, check folder permissions, available disk space, and file locks.
For a bug report, include the version, Windows version, steps, and error text.
Use a small non-sensitive sample. Do not post private documents.

## Local reading preferences

Preferences and reading marks are saved in
`%LOCALAPPDATA%/ClearText/preferences.xml`. It contains full file paths and hashes,
not document text. Close the app and delete that file to reset local history.
No files or preferences are uploaded.
