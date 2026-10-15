using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

[assembly: AssemblyTitle("ClearText")]
[assembly: AssemblyDescription("A focused Windows text workspace with a side-by-side tutorial")]
[assembly: AssemblyVersion("0.3.0.0")]
[assembly: AssemblyFileVersion("0.3.0.0")]

namespace ClearTextNative
{
    public sealed class Document
    {
        public string Path;
        public string Name;
        public TextBox Editor;
        public TextBlock Gutter;
        public TextBlock Label;
        public TabItem Tab;
        public Encoding Encoding = new UTF8Encoding(false, true);
        public bool Bom;
        public string Newline = "\r\n";
        public string OriginalHash;
        public bool Dirty;
        public bool Wrap = true;
        public Canvas Lens;
        public Border ShadeTop;
        public Border ShadeBottom;
        public readonly List<int> Bookmarks = new List<int>();
        public int Words;
    }

    public static class Files
    {
        public const int Limit = 20 * 1024 * 1024;
        public static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(bytes));
        }
        public static byte[] Read(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (MemoryStream result = new MemoryStream())
            {
                byte[] block = new byte[65536];
                int count;
                while ((count = stream.Read(block, 0, block.Length)) != 0)
                {
                    if (result.Length + count > Limit) throw new IOException("This preview supports files up to 20 MB.");
                    result.Write(block, 0, count);
                }
                return result.ToArray();
            }
        }
        public static string Decode(byte[] bytes, Document doc)
        {
            int skip = 0;
            if (bytes.Length >= 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191)
            { doc.Encoding = new UTF8Encoding(true, true); doc.Bom = true; skip = 3; }
            else if (bytes.Length >= 2 && bytes[0] == 255 && bytes[1] == 254)
            { doc.Encoding = new UnicodeEncoding(false, true, true); doc.Bom = true; skip = 2; }
            else if (bytes.Length >= 2 && bytes[0] == 254 && bytes[1] == 255)
            { doc.Encoding = new UnicodeEncoding(true, true, true); doc.Bom = true; skip = 2; }
            string text = doc.Encoding.GetString(bytes, skip, bytes.Length - skip);
            doc.Newline = text.Contains("\r\n") ? "\r\n" : text.Contains("\r") ? "\r" : "\n";
            return Normalize(text);
        }
        public static string Normalize(string text) { return text.Replace("\r\n", "\n").Replace("\r", "\n"); }
        public static byte[] Encode(Document doc)
        {
            byte[] content = doc.Encoding.GetBytes(Normalize(doc.Editor.Text).Replace("\n", doc.Newline));
            return doc.Bom ? doc.Encoding.GetPreamble().Concat(content).ToArray() : content;
        }
        public static void Write(string path, byte[] bytes)
        {
            string temporary = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)), ".cleartext-" + Guid.NewGuid().ToString("N"));
            try
            {
                using (FileStream output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
                { output.Write(bytes, 0, bytes.Length); output.Flush(true); }
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }

    public sealed class Workspace
    {
        public const string Version = "0.3.0";
        public Window Window;
        public readonly List<Document> Documents = new List<Document>();
        TabControl tabs;
        bool syncing;
        bool dark;
        bool loading;
        int fontSize = 15;
        int fontChoice;
        bool focusLens;
        readonly bool persistence;
        readonly string settingsFile;
        readonly Dictionary<string, XElement> savedPositions = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
        readonly DispatcherTimer readingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        readonly string[] fonts = { "Segoe UI", "Georgia", "Consolas" };
        readonly string[] fontLabels = { "Sans", "Book", "Code" };
        int step;
        string lastQuery = "";
        readonly string[][] tutorial = {
            new[] { "A clearer place\nto begin.", "Read, write, and find your focus. This guide stays beside your document while you explore.\n\nStart with a practice document. Nothing on your computer will be changed.", "A five-minute tour. No account needed." },
            new[] { "Make yourself\nat home.", "Open a text file with Ctrl+O, or create a fresh page with Ctrl+N.\n\nEach document has its own tab. Use the × on a tab to close it.", "You can select several files at once." },
            new[] { "Read your way.", "Focus lens fades the text around your current paragraph. Click another paragraph to move the spotlight.\n\nCycle Sans, Book, and Code to choose your reading font. Word wrap fits long lines to the page.", "Themes, fonts, and your reading position are remembered locally for saved files." },
            new[] { "Find the\nright words.", "Press Ctrl+F and type a word. Enter finds the next match and wraps back to the start.\n\nCtrl+G takes you straight to a line. Escape dismisses search.", "Search ignores case. Try “ClearText” in the practice document." },
            new[] { "Keep what\nmatters.", "Ctrl+S saves your document. Ctrl+Shift+S saves a copy with a new name.\n\nA dot on the tab marks unsaved changes. Closing a changed document always asks what to do.", "If a file changed outside ClearText, saving asks before replacing it." },
            new[] { "Leave a\nbreadcrumb.", "Reading map turns headings into a way through your document.\n\nPress Ctrl+B to bookmark a passage. Click a bookmark to return; press Ctrl+B again at the same position to remove it.\n\nSaved files remember your position next time you open them.", "Local files. Local preferences. No telemetry." }
        };

        T Find<T>(string name) where T : class { return Window.FindName(name) as T; }
        void Click(string name, Action action) { Find<Button>(name).Click += delegate { action(); }; }
        public Workspace(bool persist = true, string preferencesPath = null)
        {
            persistence = persist;
            settingsFile = preferencesPath ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClearText", "preferences.xml");
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MainWindow.xaml"))
                Window = (Window)XamlReader.Load(stream);
            tabs = Find<TabControl>("Documents");
            Click("NewButton", delegate { New("", "Untitled"); });
            Click("OpenButton", Open);
            Click("SaveButton", delegate { Save(Current, false); });
            Click("PracticeButton", Practice);
            Click("GuidePracticeButton", Practice);
            Click("ThemeButton", Theme);
            Click("MapButton", ToggleMap);
            Click("HideMapButton", ToggleMap);
            Click("BookmarkButton", Bookmark);
            Click("FontButton", delegate { fontChoice = (fontChoice + 1) % fonts.Length; Find<Button>("FontButton").Content = fontLabels[fontChoice]; foreach (Document doc in Documents) Apply(doc); });
            Find<ToggleButton>("FocusButton").Click += delegate { focusLens = Find<ToggleButton>("FocusButton").IsChecked == true; foreach (Document doc in Documents) Lens(doc); };
            Find<ListBox>("OutlineList").SelectionChanged += MapSelection;
            Find<ListBox>("BookmarkList").SelectionChanged += MapSelection;
            Click("TutorialButton", ToggleGuide);
            Click("HideGuideButton", ToggleGuide);
            Click("GuideBackButton", delegate { SetStep(step - 1); });
            Click("GuideNextButton", delegate { SetStep(step + 1); });
            Click("SmallerButton", delegate { Zoom(-1); });
            Click("LargerButton", delegate { Zoom(1); });
            Click("FindButton", ShowSearch);
            Click("FindNextButton", FindNext);
            Click("CloseSearchButton", HideSearch);
            Click("MoreButton", More);
            Find<ToggleButton>("WrapButton").Click += delegate { if (!syncing && Current != null) { Current.Wrap = Find<ToggleButton>("WrapButton").IsChecked == true; Apply(Current); } };
            Find<ToggleButton>("ReadOnlyButton").Click += delegate { if (!syncing && Current != null) { Current.Editor.IsReadOnly = Find<ToggleButton>("ReadOnlyButton").IsChecked == true; Status(); } };
            Find<TextBox>("SearchBox").KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { FindNext(); e.Handled = true; } };
            tabs.SelectionChanged += delegate { Sync(); };
            Window.PreviewKeyDown += Keys;
            Window.Closing += Closing;
            readingTimer.Tick += delegate { readingTimer.Stop(); if (Current != null) { Current.Words = CountWords(Current.Editor.Text); Status(); UpdateMap(); } };
            LoadPreferences();
            SetStep(0);
            New("A little space for your next idea.\r\n\r\nOpen a document to get started, or try the practice space.\r\n\r\nClearText keeps your words on your computer.\r\n", "Welcome");
        }
        public Document Current { get { return tabs.SelectedItem == null ? null : ((TabItem)tabs.SelectedItem).Tag as Document; } }
        public Document New(string text, string name)
        {
            Document doc = new Document();
            doc.Name = name;
            doc.Editor = new TextBox { Text = text, AcceptsReturn = true, AcceptsTab = true, BorderThickness = new Thickness(0), Padding = new Thickness(8, 6, 8, 12), FontFamily = new FontFamily(fonts[fontChoice]), FontSize = fontSize, UndoLimit = 200, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, IsUndoEnabled = true };
            doc.Editor.IsInactiveSelectionHighlightEnabled = true;
            System.Windows.Automation.AutomationProperties.SetName(doc.Editor, name + " document editor");
            doc.Editor.SetResourceReference(Control.BackgroundProperty, "Surface");
            doc.Editor.SetResourceReference(Control.ForegroundProperty, "Ink");
            doc.Editor.SetResourceReference(TextBox.CaretBrushProperty, "Ink");
            doc.Gutter = new TextBlock { FontFamily = new FontFamily("Consolas"), FontSize = fontSize, Margin = new Thickness(0, 6, 14, 0), TextAlignment = TextAlignment.Right, MinWidth = 28, VerticalAlignment = VerticalAlignment.Top };
            doc.Gutter.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
            DockPanel content = new DockPanel { Margin = new Thickness(20, 20, 16, 12), LastChildFill = true, ClipToBounds = true };
            DockPanel.SetDock(doc.Gutter, Dock.Left);
            Grid readingSurface = new Grid { ClipToBounds = true };
            doc.Lens = new Canvas { IsHitTestVisible = false, ClipToBounds = true };
            doc.ShadeTop = new Border { Opacity = 0.76 }; doc.ShadeBottom = new Border { Opacity = 0.76 };
            doc.ShadeTop.SetResourceReference(Border.BackgroundProperty, "Surface"); doc.ShadeBottom.SetResourceReference(Border.BackgroundProperty, "Surface");
            doc.Lens.Children.Add(doc.ShadeTop); doc.Lens.Children.Add(doc.ShadeBottom);
            readingSurface.Children.Add(doc.Editor); readingSurface.Children.Add(doc.Lens);
            content.Children.Add(doc.Gutter); content.Children.Add(readingSurface);
            StackPanel header = new StackPanel { Orientation = Orientation.Horizontal };
            doc.Label = new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 165, TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 12 };
            Button close = new Button { Content = "×", Padding = new Thickness(5, 0, 5, 0), Margin = new Thickness(12, 0, 0, 0), FontSize = 16, ToolTip = "Close document (Ctrl+W)" };
            close.Click += delegate { Close(doc); };
            header.Children.Add(doc.Label); header.Children.Add(close);
            doc.Tab = new TabItem { Header = header, Content = content, Tag = doc };
            Documents.Add(doc); tabs.Items.Add(doc.Tab); tabs.SelectedItem = doc.Tab;
            doc.Words = CountWords(text);
            doc.Editor.TextChanged += delegate(object sender, TextChangedEventArgs e) { AdjustBookmarks(doc, e); doc.Dirty = true; Title(doc); Status(); Gutter(doc); Lens(doc); readingTimer.Stop(); readingTimer.Start(); };
            doc.Editor.SelectionChanged += delegate { Status(); Lens(doc); };
            doc.Editor.SizeChanged += delegate { Lens(doc); };
            doc.Editor.Loaded += delegate { Lens(doc); Gutter(doc); };
            doc.Editor.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(delegate { Gutter(doc); Lens(doc); }));
            Apply(doc); Sync(); doc.Editor.Focus();
            return doc;
        }
        void Title(Document doc)
        {
            doc.Label.Text = (doc.Dirty ? "●  " : "") + doc.Name;
            doc.Tab.ToolTip = doc.Path ?? "Unsaved document";
        }
        void Apply(Document doc)
        {
            doc.Editor.TextWrapping = doc.Wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
            doc.Editor.HorizontalScrollBarVisibility = doc.Wrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
            doc.Editor.FontSize = fontSize; doc.Gutter.FontSize = fontSize;
            doc.Editor.FontFamily = new FontFamily(fonts[fontChoice]);
            doc.Gutter.FontFamily = new FontFamily(fonts[fontChoice]);
            doc.Gutter.Visibility = doc.Wrap ? Visibility.Collapsed : Visibility.Visible;
            Window.Dispatcher.BeginInvoke(new Action(delegate { Gutter(doc); Lens(doc); }), DispatcherPriority.Background);
        }
        void Gutter(Document doc)
        {
            if (doc.Wrap || doc.Editor.LineCount == 0) return;
            int first = Math.Max(0, doc.Editor.GetFirstVisibleLineIndex());
            int last = Math.Max(first, doc.Editor.GetLastVisibleLineIndex());
            doc.Gutter.Text = string.Join("\n", Enumerable.Range(first + 1, Math.Min(last - first + 1, 200)).Select(n => n.ToString()));
            double lineHeight = doc.Editor.FontFamily.LineSpacing * fontSize;
            doc.Gutter.LineHeight = lineHeight;
            doc.Gutter.Margin = new Thickness(0, 6 - (doc.Editor.VerticalOffset % lineHeight), 14, 0);
        }
        void Sync()
        {
            if (Current == null) return;
            syncing = true;
            Find<ToggleButton>("WrapButton").IsChecked = Current.Wrap;
            Find<ToggleButton>("ReadOnlyButton").IsChecked = Current.Editor.IsReadOnly;
            syncing = false;
            lastQuery = "";
            Status(); UpdateMap();
        }
        void Status()
        {
            Document doc = Current; if (doc == null) return;
            int line = Math.Max(0, doc.Editor.GetLineIndexFromCharacterIndex(doc.Editor.CaretIndex));
            int lineStart = doc.Editor.GetCharacterIndexFromLineIndex(line);
            int position = doc.Editor.Text.Length == 0 ? 0 : (int)(100.0 * doc.Editor.CaretIndex / doc.Editor.Text.Length);
            Find<TextBlock>("StatusLeft").Text = "Position " + position + "%    ·    " + doc.Words.ToString("N0") + " words    ·    ~" + Math.Max(1, (int)Math.Ceiling(doc.Words / 220.0)) + " min read";
            Find<TextBlock>("StatusLeft").ToolTip = "Ln " + (line + 1) + ", Col " + (doc.Editor.CaretIndex - Math.Max(0, lineStart) + 1) + ". Reading time assumes 220 words per minute.";
            Find<TextBlock>("StatusRight").Text = doc.Encoding.WebName.ToUpperInvariant() + "    ·    " + (doc.Newline == "\r\n" ? "CRLF" : doc.Newline == "\r" ? "CR" : "LF") + "    ·    " + (doc.Editor.IsReadOnly ? "Read only" : "Editing");
            Window.Title = doc.Name + (doc.Dirty ? " *" : "") + " — ClearText";
        }
        public void Practice()
        {
            New("# A little room to think\r\n\r\nSome documents deserve more than a quick glance. A thoughtful essay. A long set of notes. A chapter you want to come back to.\r\n\r\nClearText is a quieter place to spend time with your words.\r\n\r\n# Find your focus\r\n\r\nTurn on Focus lens, then click inside a paragraph. The surrounding text fades, leaving a little more room for the idea in front of you.\r\n\r\n# Make it feel like you\r\n\r\nTry Sans for everyday reading, Book for a chapter, or Code when spacing matters. The interface stays familiar as the document changes character.\r\n\r\n# Leave a breadcrumb\r\n\r\nOpen Reading map. Jump between headings, or press Ctrl+B to bookmark a passage. Save this file to remember those places next time.\r\n\r\n# Your next idea\r\n\r\nStart here. There is no rush.\r\n", "Practice.txt");
        }
        public static int CountWords(string text)
        {
            int count = 0; bool inWord = false;
            foreach (char value in text) { bool white = char.IsWhiteSpace(value); if (!white && !inWord) count++; inWord = !white; }
            return count;
        }
        public void SetFocusLens(bool enabled)
        {
            focusLens = enabled; Find<ToggleButton>("FocusButton").IsChecked = enabled;
            foreach (Document doc in Documents) Lens(doc);
        }
        void Lens(Document doc)
        {
            doc.Lens.Visibility = focusLens ? Visibility.Visible : Visibility.Collapsed;
            if (!focusLens || !doc.Editor.IsLoaded || doc.Editor.ActualHeight < 1) return;
            string text = doc.Editor.Text;
            int caret = Math.Min(doc.Editor.CaretIndex, text.Length);
            int lf = text.LastIndexOf("\n\n", Math.Max(0, caret - 1), StringComparison.Ordinal);
            int crlf = text.LastIndexOf("\r\n\r\n", Math.Max(0, caret - 1), StringComparison.Ordinal);
            int start = Math.Max(lf < 0 ? 0 : lf + 2, crlf < 0 ? 0 : crlf + 4);
            int endLf = text.IndexOf("\n\n", caret, StringComparison.Ordinal);
            int endCrLf = text.IndexOf("\r\n\r\n", caret, StringComparison.Ordinal);
            int end = Math.Min(endLf < 0 ? text.Length : endLf, endCrLf < 0 ? text.Length : endCrLf);
            Rect top = doc.Editor.GetRectFromCharacterIndex(Math.Min(start, text.Length));
            Rect bottom = doc.Editor.GetRectFromCharacterIndex(Math.Max(start, end - 1), true);
            double topHeight = top.IsEmpty ? 0 : Math.Max(0, top.Y - 8);
            double bottomStart = bottom.IsEmpty ? doc.Editor.ActualHeight : Math.Min(doc.Editor.ActualHeight, bottom.Bottom + 9);
            doc.ShadeTop.Width = doc.ShadeBottom.Width = Math.Max(0, doc.Editor.ActualWidth - 18);
            doc.ShadeTop.Height = Math.Min(doc.Editor.ActualHeight, topHeight);
            Canvas.SetTop(doc.ShadeTop, 0);
            Canvas.SetTop(doc.ShadeBottom, bottomStart);
            doc.ShadeBottom.Height = Math.Max(0, doc.Editor.ActualHeight - bottomStart);
        }
        public void ToggleMap()
        {
            bool show = Find<Border>("MapPanel").Visibility != Visibility.Visible;
            Find<Border>("MapPanel").Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            Find<Border>("GuidePanel").Visibility = Visibility.Collapsed;
            Find<ColumnDefinition>("GuideColumn").Width = new GridLength(show ? 300 : 0);
            UpdateMap();
        }
        void MapSelection(object sender, SelectionChangedEventArgs e)
        {
            ListBoxItem item = ((ListBox)sender).SelectedItem as ListBoxItem;
            if (item == null || Current == null) return;
            int index = Math.Min((int)item.Tag, Current.Editor.Text.Length);
            Current.Editor.CaretIndex = index;
            Current.Editor.ScrollToLine(Current.Editor.GetLineIndexFromCharacterIndex(index));
            Current.Editor.Focus();
        }
        public static List<KeyValuePair<int, string>> Headings(string text)
        {
            List<KeyValuePair<int, string>> headings = new List<KeyValuePair<int, string>>();
            int offset = 0;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length >= 3 && line.Length <= 100)
                {
                    bool markdown = Regex.IsMatch(line, @"^#{1,6}\s+\S");
                    bool chapter = Regex.IsMatch(line, @"^(chapter|part|section)\s+\w", RegexOptions.IgnoreCase);
                    bool capitals = line.Any(char.IsLetter) && line.ToUpperInvariant() == line && line.Length < 65;
                    if (markdown || chapter || capitals) headings.Add(new KeyValuePair<int, string>(offset, markdown ? line.TrimStart('#').Trim() : line));
                    if (headings.Count == 150) break;
                }
                offset += raw.Length + 1;
            }
            return headings;
        }
        ListBoxItem MapItem(string text, int position)
        {
            return new ListBoxItem { Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, LineHeight = 20 }, Tag = position, Padding = new Thickness(6, 7, 6, 7), Margin = new Thickness(0, 1, 0, 1), ToolTip = "Jump to this passage" };
        }
        public void UpdateMap()
        {
            if (Current == null || Find<Border>("MapPanel").Visibility != Visibility.Visible) return;
            ListBox outline = Find<ListBox>("OutlineList"); ListBox marks = Find<ListBox>("BookmarkList");
            outline.Items.Clear(); marks.Items.Clear();
            foreach (KeyValuePair<int, string> heading in Headings(Current.Editor.Text)) outline.Items.Add(MapItem(heading.Value, heading.Key));
            foreach (int position in Current.Bookmarks.OrderBy(n => n))
            {
                string snippet = Current.Editor.Text.Substring(Math.Min(position, Current.Editor.Text.Length));
                snippet = snippet.TrimStart().Split('\n')[0].Trim();
                if (snippet.Length > 45) snippet = snippet.Substring(0, 45) + "…";
                marks.Items.Add(MapItem("◇  " + (snippet.Length == 0 ? "End of document" : snippet), position));
            }
            Find<TextBlock>("OutlineHint").Visibility = outline.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            Find<TextBlock>("BookmarkHint").Visibility = marks.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        public void Bookmark()
        {
            if (Current == null) return;
            int position = Current.Editor.CaretIndex;
            if (Current.Bookmarks.Contains(position)) Current.Bookmarks.Remove(position);
            else if (Current.Bookmarks.Count < 100) Current.Bookmarks.Add(position);
            Find<Border>("MapPanel").Visibility = Visibility.Visible;
            Find<Border>("GuidePanel").Visibility = Visibility.Collapsed;
            Find<ColumnDefinition>("GuideColumn").Width = new GridLength(300);
            UpdateMap(); Remember(Current); SavePreferences();
        }
        void AdjustBookmarks(Document doc, TextChangedEventArgs e)
        {
            foreach (TextChange change in e.Changes.OrderByDescending(c => c.Offset))
                for (int i = 0; i < doc.Bookmarks.Count; i++)
                    if (doc.Bookmarks[i] >= change.Offset)
                        doc.Bookmarks[i] = Math.Max(change.Offset, doc.Bookmarks[i] - change.RemovedLength) + change.AddedLength;
        }
        string SettingsPath { get { return settingsFile; } }
        void LoadPreferences()
        {
            if (!persistence) return;
            try
            {
                if (!File.Exists(SettingsPath)) return;
                System.Xml.XmlReaderSettings options = new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 524288 };
                XElement root;
                using (System.Xml.XmlReader reader = System.Xml.XmlReader.Create(SettingsPath, options)) root = XDocument.Load(reader).Root;
                int value;
                if (int.TryParse((string)root.Attribute("size"), out value)) fontSize = Math.Max(10, Math.Min(30, value));
                if (int.TryParse((string)root.Attribute("font"), out value)) fontChoice = Math.Max(0, Math.Min(2, value));
                if ((string)root.Attribute("dark") == "true") Theme();
                focusLens = (string)root.Attribute("focus") == "true";
                Find<ToggleButton>("FocusButton").IsChecked = focusLens;
                Find<Button>("FontButton").Content = fontLabels[fontChoice]; Find<TextBlock>("FontLabel").Text = fontSize.ToString();
                foreach (XElement file in root.Elements("file").Take(100)) { string path = (string)file.Attribute("path"); if (!string.IsNullOrEmpty(path)) savedPositions[path] = new XElement(file); }
            }
            catch { Find<TextBlock>("StatusLeft").ToolTip = "Preferences could not be loaded. Default settings are active."; }
        }
        public void Remember(Document doc)
        {
            if (!persistence || doc.Path == null || doc.Dirty) return;
            savedPositions[doc.Path] = new XElement("file", new XAttribute("path", doc.Path), new XAttribute("hash", doc.OriginalHash ?? ""), new XAttribute("caret", doc.Editor.CaretIndex), new XAttribute("marks", string.Join(",", doc.Bookmarks)));
        }
        public void RestorePosition(Document doc)
        {
            XElement stored;
            if (!persistence || doc.Path == null || !savedPositions.TryGetValue(doc.Path, out stored) || (string)stored.Attribute("hash") != doc.OriginalHash) return;
            int position;
            if (int.TryParse((string)stored.Attribute("caret"), out position)) doc.Editor.CaretIndex = Math.Max(0, Math.Min(doc.Editor.Text.Length, position));
            foreach (string mark in ((string)stored.Attribute("marks") ?? "").Split(',').Take(100))
                if (int.TryParse(mark, out position) && position >= 0 && position <= doc.Editor.Text.Length && !doc.Bookmarks.Contains(position)) doc.Bookmarks.Add(position);
            Window.Dispatcher.BeginInvoke(new Action(delegate { doc.Editor.ScrollToLine(doc.Editor.GetLineIndexFromCharacterIndex(doc.Editor.CaretIndex)); }), DispatcherPriority.Loaded);
        }
        public void SavePreferences()
        {
            if (!persistence) return;
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SettingsPath));
                XElement settings = new XElement("cleartext", new XAttribute("size", fontSize), new XAttribute("font", fontChoice), new XAttribute("dark", dark ? "true" : "false"), new XAttribute("focus", focusLens ? "true" : "false"), savedPositions.Values.Reverse().Take(100).Select(x => new XElement(x)));
                Files.Write(SettingsPath, Encoding.UTF8.GetBytes(settings.ToString()));
            }
            catch { Find<TextBlock>("StatusLeft").ToolTip = "Reading preferences could not be saved on this computer."; }
        }
        async void Open()
        {
            if (loading) return;
            OpenFileDialog dialog = new OpenFileDialog { Multiselect = true, Filter = "Text documents|*.txt;*.log;*.md;*.csv|All files|*.*" };
            if (dialog.ShowDialog(Window) != true) return;
            loading = true; Find<Button>("OpenButton").IsEnabled = false;
            try
            {
                foreach (string path in dialog.FileNames)
                {
                    try
                    {
                        Document existing = Documents.FirstOrDefault(d => string.Equals(d.Path, path, StringComparison.OrdinalIgnoreCase));
                        if (existing != null) { tabs.SelectedItem = existing.Tab; continue; }
                        Find<TextBlock>("StatusLeft").Text = "Opening " + System.IO.Path.GetFileName(path) + "…";
                        byte[] bytes = await Task.Run(() => Files.Read(path));
                        Document metadata = new Document(); string text;
                        try { text = Files.Decode(bytes, metadata); }
                        catch (DecoderFallbackException)
                        {
                            string encoding = Prompt("Choose an encoding", "This file is not valid UTF-8 or BOM-marked UTF-16.\nEnter its encoding name, for example windows-1252.", "windows-1252");
                            if (encoding == null) continue;
                            metadata.Encoding = Encoding.GetEncoding(encoding, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                            metadata.Bom = false; text = Files.Decode(bytes, metadata);
                        }
                        Document doc = New(text, System.IO.Path.GetFileName(path));
                        doc.Path = path; doc.Encoding = metadata.Encoding; doc.Bom = metadata.Bom; doc.Newline = metadata.Newline; doc.OriginalHash = Files.Hash(bytes); RestorePosition(doc); Title(doc); Status(); UpdateMap();
                    }
                    catch (Exception error) { Error("Unable to open file", error); }
                }
            }
            finally { loading = false; Find<Button>("OpenButton").IsEnabled = true; Status(); }
        }
        public bool Save(Document doc, bool saveAs)
        {
            if (doc == null) return false;
            string path = doc.Path;
            if (saveAs || path == null)
            {
                SaveFileDialog dialog = new SaveFileDialog { FileName = doc.Name == "Untitled" || doc.Name == "Welcome" ? "Untitled.txt" : doc.Name, DefaultExt = ".txt", Filter = "Text documents|*.txt|All files|*.*" };
                if (dialog.ShowDialog(Window) != true) return false;
                path = dialog.FileName;
            }
            try
            {
                if (string.Equals(path, doc.Path, StringComparison.OrdinalIgnoreCase) && doc.OriginalHash != null)
                {
                    bool changed = !File.Exists(path) || Files.Hash(Files.Read(path)) != doc.OriginalHash;
                    if (changed && MessageBox.Show(Window, "This file changed or was removed outside ClearText. Replace it with this document?\n\nChoose No to keep your edits and save a copy instead.", "External changes detected", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return false;
                }
                byte[] bytes = Files.Encode(doc);
                Files.Write(path, bytes);
                doc.Path = path; doc.Name = System.IO.Path.GetFileName(path); doc.Dirty = false; doc.OriginalHash = Files.Hash(bytes); Title(doc); Status(); Remember(doc); SavePreferences(); return true;
            }
            catch (Exception error) { Error("Unable to save file", error); return false; }
        }
        bool CanClose(Document doc)
        {
            if (!doc.Dirty) return true;
            MessageBoxResult answer = MessageBox.Show(Window, "Save changes to “" + doc.Name + "” before closing?", "Unsaved changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            return answer == MessageBoxResult.No || (answer == MessageBoxResult.Yes && Save(doc, false));
        }
        public void Close(Document doc)
        {
            if (doc == null || !CanClose(doc)) return;
            Remember(doc); SavePreferences(); tabs.Items.Remove(doc.Tab); Documents.Remove(doc);
            if (Documents.Count == 0) New("", "Untitled");
            Sync();
        }
        void Closing(object sender, CancelEventArgs e)
        {
            if (loading) { MessageBox.Show(Window, "Please wait for the selected files to finish opening.", "Opening files"); e.Cancel = true; return; }
            foreach (Document doc in Documents.ToArray()) if (!CanClose(doc)) { e.Cancel = true; return; }
            foreach (Document doc in Documents) Remember(doc);
            SavePreferences();
        }
        void Zoom(int amount)
        {
            fontSize = Math.Max(10, Math.Min(30, fontSize + amount));
            Find<TextBlock>("FontLabel").Text = fontSize.ToString();
            foreach (Document doc in Documents) Apply(doc);
        }
        public void Theme()
        {
            dark = !dark;
            string[] keys = { "Canvas", "Surface", "Ink", "Muted", "Line", "Hover", "Accent", "AccentText" };
            string[] values = dark ? new[] { "#111B2A", "#1A273A", "#E7EDF8", "#A5B4CC", "#2B3B53", "#263B5C", "#3866E9", "#9BB8FF" } : new[] { "#F3F5FA", "#FFFFFF", "#202B40", "#68758B", "#E4E9F2", "#E9EFF9", "#3866E9", "#3866E9" };
            for (int i = 0; i < keys.Length; i++) Window.Resources[keys[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(values[i]));
        }
        public void ToggleGuide()
        {
            Border panel = Find<Border>("GuidePanel"); bool show = panel.Visibility != Visibility.Visible;
            Find<Border>("MapPanel").Visibility = Visibility.Collapsed;
            panel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            Find<ColumnDefinition>("GuideColumn").Width = new GridLength(show ? 300 : 0);
        }
        public void SetStep(int value)
        {
            step = Math.Max(0, Math.Min(tutorial.Length - 1, value));
            Find<TextBlock>("StepLabel").Text = "STEP " + (step + 1).ToString("00") + " OF 06";
            Find<TextBlock>("GuideTitle").Text = tutorial[step][0];
            Find<TextBlock>("GuideCopy").Text = tutorial[step][1];
            Find<TextBlock>("GuideTip").Text = tutorial[step][2];
            Find<ProgressBar>("GuideProgress").Value = step + 1;
            Find<Button>("GuideBackButton").IsEnabled = step > 0;
            Find<Button>("GuideNextButton").IsEnabled = step < tutorial.Length - 1;
        }
        void ShowSearch() { Find<Border>("SearchPanel").Visibility = Visibility.Visible; Find<TextBox>("SearchBox").Focus(); Find<TextBox>("SearchBox").SelectAll(); }
        void HideSearch() { Find<Border>("SearchPanel").Visibility = Visibility.Collapsed; if (Current != null) Current.Editor.Focus(); }
        public bool Search(string query)
        {
            if (Current == null || query.Length == 0) return false;
            TextBox editor = Current.Editor;
            int start = lastQuery == query ? editor.SelectionStart + editor.SelectionLength : editor.CaretIndex;
            int index = editor.Text.IndexOf(query, Math.Min(start, editor.Text.Length), StringComparison.OrdinalIgnoreCase);
            if (index < 0) index = editor.Text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            lastQuery = query;
            if (index < 0) return false;
            editor.Select(index, query.Length);
            editor.ScrollToLine(editor.GetLineIndexFromCharacterIndex(index));
            if (Find<Border>("SearchPanel").Visibility != Visibility.Visible) editor.Focus();
            return true;
        }
        void FindNext() { Find<TextBlock>("SearchStatus").Text = Search(Find<TextBox>("SearchBox").Text) ? "Match found" : "No match"; }
        void GoToLine()
        {
            if (Current == null) return;
            string value = Prompt("Go to line", "Enter a logical line number.", "1"); int line;
            if (value == null) return;
            if (!int.TryParse(value, out line) || line < 1) { MessageBox.Show(Window, "Enter a positive line number.", "Go to line"); return; }
            string text = Current.Editor.Text; int index = 0;
            for (int n = 1; n < line; n++) { int next = text.IndexOf('\n', index); if (next < 0) { MessageBox.Show(Window, "That line is beyond the end of this document.", "Go to line"); return; } index = next + 1; }
            Current.Editor.CaretIndex = index; Current.Editor.ScrollToLine(Current.Editor.GetLineIndexFromCharacterIndex(index)); Current.Editor.Focus();
        }
        string Prompt(string title, string message, string value)
        {
            Window prompt = new Window { Owner = Window, Title = title, Width = 460, Height = 240, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = (Brush)Window.Resources["Surface"], Foreground = (Brush)Window.Resources["Ink"] };
            StackPanel panel = new StackPanel { Margin = new Thickness(24) };
            panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) });
            TextBox input = new TextBox { Text = value, Padding = new Thickness(8), FontSize = 14 };
            panel.Children.Add(input);
            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            Button cancel = new Button { Content = "Cancel", IsCancel = true, Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(0, 0, 8, 0), Style = (Style)Window.FindResource(typeof(Button)) };
            Button confirm = new Button { Content = "Continue", IsDefault = true, Style = (Style)Window.Resources["Primary"] };
            confirm.Click += delegate { prompt.DialogResult = true; };
            actions.Children.Add(cancel); actions.Children.Add(confirm); panel.Children.Add(actions); prompt.Content = panel;
            prompt.Loaded += delegate { input.Focus(); input.SelectAll(); };
            return prompt.ShowDialog() == true ? input.Text : null;
        }
        void More()
        {
            ContextMenu menu = new ContextMenu();
            MenuItem saveAs = new MenuItem { Header = "Save as…    Ctrl+Shift+S" }; saveAs.Click += delegate { Save(Current, true); };
            MenuItem go = new MenuItem { Header = "Go to line…    Ctrl+G" }; go.Click += delegate { GoToLine(); };
            MenuItem close = new MenuItem { Header = "Close document    Ctrl+W" }; close.Click += delegate { Close(Current); };
            menu.Items.Add(saveAs); menu.Items.Add(go); menu.Items.Add(close); menu.PlacementTarget = Find<Button>("MoreButton"); menu.IsOpen = true;
        }
        void Keys(object sender, KeyEventArgs e)
        {
            bool control = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            if (e.Key == Key.F1) { ToggleGuide(); e.Handled = true; }
            else if (e.Key == Key.Escape) { HideSearch(); e.Handled = true; }
            else if (control)
            {
                switch (e.Key)
                {
                    case Key.N: New("", "Untitled"); break;
                    case Key.O: Open(); break;
                    case Key.S: Save(Current, (Keyboard.Modifiers & ModifierKeys.Shift) != 0); break;
                    case Key.F: ShowSearch(); break;
                    case Key.G: GoToLine(); break;
                    case Key.W: Close(Current); break;
                    case Key.B: Bookmark(); break;
                    default: return;
                }
                e.Handled = true;
            }
        }
        void Error(string title, Exception error) { MessageBox.Show(Window, error.Message, title, MessageBoxButton.OK, MessageBoxImage.Error); }

        public void Render(string path)
        {
            Window.Show(); Window.UpdateLayout();
            Window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate { }));
            if (Current != null) Lens(Current);
            Window.UpdateLayout();
            FrameworkElement content = (FrameworkElement)Window.Content;
            RenderTargetBitmap image = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            image.Render(content);
            PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using (FileStream stream = File.Create(path)) encoder.Save(stream);
        }
    }

    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            bool testing = args.Contains("--self-test");
            string report = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "self-test-results.txt");
            try
            {
                Application app = new Application();
                Workspace workspace = new Workspace(!testing && !args.Contains("--preview"));
                if (testing) { NativeTests.Run(workspace); File.WriteAllText(report, "PASS: native desktop and file regression tests\r\n"); return 0; }
                int preview = Array.IndexOf(args, "--preview");
                if (preview >= 0 && preview + 1 < args.Length)
                {
                    if (args.Contains("--dark")) workspace.Theme();
                    if (args.Contains("--compact")) { workspace.Window.Width = 1000; workspace.Window.Height = 640; workspace.SetStep(5); }
                    workspace.Practice();
                    if (args.Contains("--map")) workspace.ToggleMap();
                    if (args.Contains("--focus")) { workspace.SetFocusLens(true); workspace.Current.Editor.CaretIndex = workspace.Current.Editor.Text.IndexOf("Turn on Focus", StringComparison.Ordinal); }
                    workspace.Render(args[preview + 1]); return 0;
                }
                app.Run(workspace.Window); return 0;
            }
            catch (Exception error)
            {
                if (testing) File.WriteAllText(report, error.ToString());
                else MessageBox.Show(error.Message, "ClearText could not start", MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }
        }
    }
}
