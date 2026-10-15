# Contributing to ClearText

The current app is C# / WPF targeting Windows x64 and .NET Framework 4.8.
Run `./build_windows.ps1` in a graphical Windows session. It compiles the app,
runs native regression tests, and packages a release.

Keep file handling conservative: failed or canceled saves must retain edits.
Tests cover Unicode/BOM decoding, byte order, search wrapping, tabs, dirty state,
file replacement, locked-file failure, temporary cleanup, and the size limit.
For UI changes, inspect both themes, minimum window size, keyboard navigation,
wrapping, and the tutorial shown/hidden.

Do not commit generated builds, credentials, local caches, or private documents.
Report bugs with a small non-sensitive reproduction. Priorities include streaming
large-file viewing, reading-map refinement, and accessibility validation.
