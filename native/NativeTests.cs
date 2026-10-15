using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace ClearTextNative
{
    public static class NativeTests
    {
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        public static void Run(Workspace app)
        {
            string text = "Hello नमस्ते 🌟\r\nSecond line";
            foreach (Encoding encoding in new Encoding[] { new UTF8Encoding(false, true), new UTF8Encoding(true, true), new UnicodeEncoding(false, true, true), new UnicodeEncoding(true, true, true) })
            {
                Document metadata = new Document();
                byte[] bytes = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
                Check(Files.Decode(bytes, metadata) == Files.Normalize(text), "Unicode decoding failed");
                Check(metadata.Encoding.CodePage == encoding.CodePage, "Encoding byte order changed");
            }
            bool invalid = false;
            try { Files.Decode(new byte[] { 255, 3, 255 }, new Document()); } catch (DecoderFallbackException) { invalid = true; }
            Check(invalid, "Invalid UTF-8 silently replaced");
            app.Practice();
            Check(app.Documents.Count == 2, "Tab creation failed");
            Check(Workspace.CountWords("one  two\r\nthree") == 3, "Word count failed");
            string mapText = "# First heading\r\n\r\nordinary prose here\r\nCHAPTER TWO\r\n";
            var headings = Workspace.Headings(mapText);
            Check(headings.Count == 2 && headings[1].Key == mapText.IndexOf("CHAPTER TWO", StringComparison.Ordinal), "Document map positions failed");
            Document doc = app.New("ClearText 🌟match then 🌟match", "Regression.txt");
            doc.Editor.CaretIndex = 0;
            Check(app.Search("🌟match") && doc.Editor.SelectedText == "🌟match", "Unicode search selection failed");
            int first = doc.Editor.SelectionStart;
            Check(app.Search("🌟match") && doc.Editor.SelectionStart > first, "Find next did not advance");
            Check(app.Search("🌟match") && doc.Editor.SelectionStart == first, "Search did not wrap");
            doc.Editor.CaretIndex = 2; app.Bookmark();
            Check(doc.Bookmarks.Contains(2), "Bookmark creation failed");
            app.Bookmark(); Check(doc.Bookmarks.Count == 0, "Bookmark toggle failed");
            doc.Bookmarks.Add(2); doc.Editor.Select(0, 0); doc.Editor.SelectedText = "abc";
            Check(doc.Bookmarks[0] == 5, "Bookmark did not follow inserted text");
            Document focusDoc = app.New("First paragraph.\r\n\r\nMiddle paragraph.\r\n\r\nLast paragraph.", "Lens test");
            app.Window.ShowInTaskbar = false; app.Window.Opacity = 0; app.Window.Show(); app.Window.UpdateLayout();
            app.Window.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(delegate { }));
            focusDoc.Editor.CaretIndex = focusDoc.Editor.Text.IndexOf("Middle", StringComparison.Ordinal);
            app.SetFocusLens(true); app.Window.UpdateLayout();
            Check(focusDoc.ShadeTop.Height > 0 && focusDoc.ShadeBottom.Height > 0, "Focus Lens did not frame the current paragraph");
            app.SetFocusLens(true); app.SetFocusLens(false);
            doc.Editor.AppendText(" changed"); Check(doc.Dirty, "Dirty marker failed");
            doc.Editor.IsReadOnly = true;
            Check(app.New("", "Other").Editor.IsReadOnly == false, "Read-only leaked across tabs");
            app.Theme(); app.Theme(); app.ToggleGuide(); app.ToggleGuide();
            for (int i = 0; i < 6; i++) app.SetStep(i);
            app.SetStep(0);
            string folder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string path = System.IO.Path.Combine(folder, "roundtrip.txt");
                Files.Write(path, Encoding.UTF8.GetBytes("original"));
                Files.Write(path, Encoding.UTF8.GetBytes(text));
                Check(Encoding.UTF8.GetString(Files.Read(path)) == text, "File replacement failed");
                using (FileStream locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    bool failed = false;
                    try { Files.Write(path, Encoding.UTF8.GetBytes("lost")); } catch (IOException) { failed = true; }
                    Check(failed, "Locked-file write should fail");
                }
                Check(Encoding.UTF8.GetString(Files.Read(path)) == text, "Failed save changed original");
                Check(Directory.GetFiles(folder).Length == 1, "Temporary save file leaked");
                string large = System.IO.Path.Combine(folder, "large.txt");
                using (FileStream stream = File.Create(large)) stream.SetLength(Files.Limit + 1);
                bool limited = false; try { Files.Read(large); } catch (IOException) { limited = true; }
                Check(limited, "File-size admission failed");
                string preferences = System.IO.Path.Combine(folder, "preferences.xml");
                Workspace remembered = new Workspace(true, preferences);
                Document rememberedDoc = remembered.New("saved reading position", "saved.txt");
                rememberedDoc.Path = path; rememberedDoc.OriginalHash = "matching-content"; rememberedDoc.Editor.CaretIndex = 7; rememberedDoc.Bookmarks.Add(3);
                remembered.Remember(rememberedDoc); remembered.SavePreferences();
                Workspace restored = new Workspace(true, preferences);
                Document restoredDoc = restored.New("saved reading position", "saved.txt");
                restoredDoc.Path = path; restoredDoc.OriginalHash = "matching-content";
                restored.RestorePosition(restoredDoc);
                Check(restoredDoc.Editor.CaretIndex == 7 && restoredDoc.Bookmarks.Contains(3), "Reading position/bookmarks did not persist");
                Document changedDoc = restored.New("externally changed text", "saved.txt");
                changedDoc.Path = path; changedDoc.OriginalHash = "different-content";
                restored.RestorePosition(changedDoc);
                Check(changedDoc.Editor.CaretIndex == 0 && changedDoc.Bookmarks.Count == 0, "Stale reading marks applied to changed content");
            }
            finally { foreach (string file in Directory.GetFiles(folder)) File.Delete(file); Directory.Delete(folder); }
        }
    }
}
