using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace IntuneWinPackager
{
    public class MainForm : Form
    {
        readonly AppSettings settings = AppSettings.Load();

        TextBox txtTool;
        TextBox txtInput;
        TextBox txtOutput;
        CheckBox chkSubfolders;
        CheckBox chkSelectAll;
        CheckBox chkFlatOutput;
        CheckBox chkDarkMode;
        Label lblCount;
        ListView lstApps;
        Button btnCompile;
        ProgressBar progress;
        TextBox txtLog;
        readonly List<Control> lockWhileBusy = new List<Control>();
        bool busy;

        public MainForm()
        {
            Text = "IntuneWin Packager";
            ClientSize = new Size(900, 640);
            MinimumSize = new Size(700, 500);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);

            BuildLayout();

            txtTool.Text = File.Exists(settings.ToolPath)
                ? settings.ToolPath
                : IntuneWinAppUtilRunner.FindTool(settings.InputFolder) ?? string.Empty;
            txtInput.Text = settings.InputFolder;
            txtOutput.Text = settings.OutputFolder;
            chkSubfolders.Checked = settings.IncludeSubfolders;
            chkSubfolders.CheckedChanged += (s, e) => ScanInputFolder();
            chkFlatOutput.Checked = settings.FlatOutput;
            chkDarkMode.Checked = settings.DarkMode;
            chkDarkMode.CheckedChanged += (s, e) => ApplyTheme();
            Load += (s, e) => ApplyTheme();

            Shown += (s, e) =>
            {
                Log("1. Input folder: click \"Select folder...\" and pick the folder that holds your installers (or drag it here from Explorer).");
                Log("2. Output folder: click \"Select folder...\" and pick where the .intunewin files should be saved.");
                Log("3. Tick the apps you want and click \"Compile selected\".");
                Log("");
                ScanInputFolder();
            };
            FormClosing += OnFormClosing;
        }

        void BuildLayout()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                ColumnCount = 4,
                RowCount = 7
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            for (int i = 0; i < 4; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));

            // Row 0: IntuneWinAppUtil.exe
            txtTool = AddPathRow(layout, 0, "IntuneWinAppUtil.exe:", "Browse...", BrowseTool,
                MakeButton("Download...", (s, e) => Process.Start(IntuneWinAppUtilRunner.DownloadUrl)));

            // Row 1: input folder
            txtInput = AddPathRow(layout, 1, "Input folder:", "Select folder...", (s, e) => PickInputFolder(),
                MakeButton("Scan", (s, e) => ScanInputFolder()));
            txtInput.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ScanInputFolder(); } };

            // Row 2: output folder
            txtOutput = AddPathRow(layout, 2, "Output folder:", "Select folder...", (s, e) => PickOutputFolder(),
                MakeButton("Open", (s, e) => OpenOutputFolder()));

            // Folders can also be dragged in from Explorer.
            EnableFolderDrop(txtInput, SetInputFolder);
            EnableFolderDrop(txtOutput, SetOutputFolder);

            // Row 3: list options
            var options = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0), WrapContents = false };
            chkSubfolders = new CheckBox { Text = "Include subfolders", AutoSize = true, Margin = new Padding(3, 7, 12, 3) };
            lblCount = new Label { AutoSize = true, Margin = new Padding(12, 8, 3, 3), Tag = "muted" };
            // AutoCheck is off so a click always means "select all" unless everything is already selected.
            chkSelectAll = new CheckBox { Text = "Select all", AutoSize = true, AutoCheck = false, Margin = new Padding(3, 7, 12, 3) };
            chkSelectAll.Click += (s, e) => SetAllChecked(lstApps.CheckedItems.Count < lstApps.Items.Count);
            chkFlatOutput = new CheckBox { Text = "Save all packages directly in the output folder", AutoSize = true, Margin = new Padding(3, 7, 12, 3) };
            options.Controls.Add(chkSelectAll);
            options.Controls.Add(chkSubfolders);
            options.Controls.Add(chkFlatOutput);
            options.Controls.Add(lblCount);
            layout.Controls.Add(options, 0, 3);
            layout.SetColumnSpan(options, 4);
            lockWhileBusy.Add(options);

            // Row 4: applications found in the input folder
            lstApps = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                CheckBoxes = true,
                FullRowSelect = true,
                GridLines = true,
                HideSelection = false
            };
            lstApps.Columns.Add("Setup file", 220);
            lstApps.Columns.Add("Type", 50);
            lstApps.Columns.Add("Folder (gets packaged)", 380);
            lstApps.Columns.Add("Size", 80, HorizontalAlignment.Right);
            lstApps.ItemChecked += (s, e) => UpdateCount();
            // Stretch the last column to the edge so there's no unthemed strip of header after it.
            lstApps.Resize += (s, e) => lstApps.Columns[lstApps.Columns.Count - 1].Width = -2;
            layout.Controls.Add(lstApps, 0, 4);
            layout.SetColumnSpan(lstApps, 4);
            EnableFolderDrop(lstApps, SetInputFolder);
            lockWhileBusy.Add(lstApps);

            // Row 5: compile button and progress
            var actions = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 3, Margin = new Padding(0) };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            btnCompile = MakeButton("Compile selected", (s, e) => CompileSelected());
            btnCompile.Font = new Font(Font, FontStyle.Bold);
            btnCompile.Padding = new Padding(8, 2, 8, 2);
            btnCompile.EnabledChanged += (s, e) => Theme.StyleAccent(btnCompile);
            progress = new ProgressBar { Dock = DockStyle.Fill, Margin = new Padding(8, 6, 3, 6) };
            actions.Controls.Add(btnCompile, 0, 0);
            actions.Controls.Add(progress, 1, 0);
            chkDarkMode = new CheckBox { Text = "Dark mode", AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(12, 6, 3, 3) };
            actions.Controls.Add(chkDarkMode, 2, 0);
            layout.Controls.Add(actions, 0, 5);
            layout.SetColumnSpan(actions, 4);

            // Row 6: log
            txtLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = new Font("Consolas", 9F),
                BackColor = SystemColors.Window
            };
            layout.Controls.Add(txtLog, 0, 6);
            layout.SetColumnSpan(txtLog, 4);

            Controls.Add(layout);
        }

        TextBox AddPathRow(TableLayoutPanel layout, int row, string label, string browseText, EventHandler browse, Button extra)
        {
            var text = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(3, 5, 3, 3) };
            var browseButton = MakeButton(browseText, browse);

            layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 3) }, 0, row);
            layout.Controls.Add(text, 1, row);
            layout.Controls.Add(browseButton, 2, row);
            layout.Controls.Add(extra, 3, row);
            lockWhileBusy.AddRange(new Control[] { text, browseButton, extra });
            return text;
        }

        static Button MakeButton(string text, EventHandler click)
        {
            var button = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(4, 1, 4, 1) };
            button.Click += click;
            return button;
        }

        void BrowseTool(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog
            {
                Title = "Locate IntuneWinAppUtil.exe",
                Filter = "IntuneWinAppUtil.exe|IntuneWinAppUtil.exe|Programs (*.exe)|*.exe",
                FileName = IntuneWinAppUtilRunner.ToolFileName
            })
            {
                if (File.Exists(txtTool.Text))
                    dialog.InitialDirectory = Path.GetDirectoryName(txtTool.Text);
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    txtTool.Text = dialog.FileName;
            }
        }

        bool PickInputFolder()
        {
            string folder = FolderPicker.Show(this, "Select the folder that contains your application installers", txtInput.Text.Trim());
            if (folder == null)
                return false;
            SetInputFolder(folder);
            return true;
        }

        bool PickOutputFolder()
        {
            string current = txtOutput.Text.Trim();
            if (!Directory.Exists(current))
                current = txtInput.Text.Trim();
            string folder = FolderPicker.Show(this, "Select where the .intunewin files should be saved", current);
            if (folder == null)
                return false;
            SetOutputFolder(folder);
            return true;
        }

        void SetInputFolder(string folder)
        {
            txtInput.Text = folder;
            if (string.IsNullOrWhiteSpace(txtTool.Text))
                txtTool.Text = IntuneWinAppUtilRunner.FindTool(folder) ?? string.Empty;
            ScanInputFolder();
        }

        void SetOutputFolder(string folder)
        {
            txtOutput.Text = folder;
            Log("Output folder: " + folder);
        }

        /// <summary>
        /// Lets a folder be dragged from Explorer onto the control. Dropping a file uses the folder it is in.
        /// </summary>
        void EnableFolderDrop(Control control, Action<string> onFolder)
        {
            control.AllowDrop = true;
            control.DragEnter += (s, e) =>
            {
                e.Effect = !busy && GetDroppedFolder(e.Data) != null ? DragDropEffects.Link : DragDropEffects.None;
            };
            control.DragDrop += (s, e) =>
            {
                string folder = GetDroppedFolder(e.Data);
                if (!busy && folder != null)
                    onFolder(folder);
            };
        }

        static string GetDroppedFolder(IDataObject data)
        {
            var paths = data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || paths.Length == 0)
                return null;
            if (Directory.Exists(paths[0]))
                return paths[0];
            if (File.Exists(paths[0]))
                return Path.GetDirectoryName(paths[0]);
            return null;
        }

        void ScanInputFolder()
        {
            if (busy)
                return;

            lstApps.BeginUpdate();
            lstApps.Items.Clear();
            try
            {
                string input = txtInput.Text.Trim();
                if (input.Length == 0)
                    return;
                if (!Directory.Exists(input))
                {
                    Log("Input folder not found: " + input);
                    return;
                }

                List<SetupFile> found;
                try
                {
                    found = SetupFileScanner.Scan(input, chkSubfolders.Checked);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    Log("Could not read the input folder: " + ex.Message);
                    return;
                }

                foreach (SetupFile setup in found)
                {
                    var item = new ListViewItem(setup.FileName) { Tag = setup, ToolTipText = setup.FullPath };
                    item.SubItems.Add(setup.Extension);
                    item.SubItems.Add(setup.RelativeFolder.Length == 0 ? "(input folder root)" : setup.RelativeFolder);
                    item.SubItems.Add(SetupFileScanner.FormatSize(setup.SizeBytes));
                    lstApps.Items.Add(item);
                }
                Log(string.Format("Found {0} setup file(s) in {1}", found.Count, input));
            }
            finally
            {
                lstApps.EndUpdate();
                UpdateCount();
            }
        }

        void SetAllChecked(bool isChecked)
        {
            lstApps.BeginUpdate();
            foreach (ListViewItem item in lstApps.Items)
                item.Checked = isChecked;
            lstApps.EndUpdate();
        }

        void UpdateCount()
        {
            int found = lstApps.Items.Count;
            int selected = lstApps.CheckedItems.Count;
            lblCount.Text = string.Format("{0} found, {1} selected", found, selected);
            chkSelectAll.CheckState = selected == 0 ? CheckState.Unchecked
                : selected == found ? CheckState.Checked
                : CheckState.Indeterminate;
            btnCompile.Enabled = !busy && lstApps.CheckedItems.Count > 0;
        }

        async void CompileSelected()
        {
            string tool = txtTool.Text.Trim();
            string output = txtOutput.Text.Trim();

            if (!File.Exists(tool))
            {
                var answer = MessageBox.Show(this,
                    "IntuneWinAppUtil.exe was not found.\n\nDownload it from Microsoft's GitHub page, then use Browse... to select it.\n\nOpen the download page now?",
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (answer == DialogResult.Yes)
                    Process.Start(IntuneWinAppUtilRunner.DownloadUrl);
                return;
            }
            if (output.Length == 0)
            {
                // No output folder yet, so ask for one now instead of failing.
                if (!PickOutputFolder())
                    return;
                output = txtOutput.Text.Trim();
            }

            var selected = lstApps.CheckedItems.Cast<ListViewItem>().Select(i => (SetupFile)i.Tag).ToList();
            if (selected.Count == 0)
                return;

            SaveSettings();
            SetBusy(true, selected.Count);

            bool flat = chkFlatOutput.Checked;
            var flatNames = flat ? GetFlatOutputNames(selected) : null;
            string workFolder = Path.Combine(output, ".packaging");

            var created = new List<string>();
            var failed = new List<string>();
            try
            {
                foreach (SetupFile setup in selected)
                {
                    Log("");
                    Log(string.Format("=== [{0}/{1}] {2} ===", progress.Value + 1, selected.Count, setup.RelativePath));
                    if (setup.RelativeFolder.Length == 0 && chkSubfolders.Checked)
                        Log("  Note: this file is in the input folder root, so the WHOLE input folder (including subfolders) will be packaged with it.");

                    // By default each app keeps its own output subfolder so files called e.g. setup.exe don't overwrite
                    // each other. In flat mode clashing names are built in a work folder and then renamed.
                    string defaultName = Path.GetFileNameWithoutExtension(setup.FileName) + ".intunewin";
                    string flatName = flat ? flatNames[setup] : null;
                    bool rename = flat && flatName != defaultName;
                    string appOutput = !flat ? Path.Combine(output, setup.RelativeFolder)
                        : rename ? workFolder
                        : output;
                    string result = null;
                    try
                    {
                        string packaged = await IntuneWinAppUtilRunner.PackageAsync(tool, setup, appOutput, LogFromAnyThread);
                        if (packaged != null && rename)
                        {
                            string target = Path.Combine(output, flatName);
                            if (File.Exists(target))
                                File.Delete(target);
                            File.Move(packaged, target);
                            packaged = target;
                        }
                        result = packaged;
                    }
                    catch (Exception ex)
                    {
                        Log("  FAILED: " + ex.Message);
                    }

                    if (result != null)
                    {
                        Log("  Created: " + result);
                        created.Add(result);
                    }
                    else
                    {
                        failed.Add(setup.RelativePath);
                    }
                    progress.Value++;
                }
            }
            finally
            {
                if (Directory.Exists(workFolder))
                {
                    try { Directory.Delete(workFolder, true); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
                SetBusy(false, 0);
            }

            Log("");
            Log(string.Format("Done. {0} package(s) created, {1} failed.", created.Count, failed.Count));
            foreach (string f in failed)
                Log("  Failed: " + f);

            var icon = failed.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning;
            var open = MessageBox.Show(this,
                string.Format("{0} package(s) created, {1} failed.\n\nOpen the output folder?", created.Count, failed.Count),
                Text, MessageBoxButtons.YesNo, icon);
            if (open == DialogResult.Yes)
                OpenOutputFolder();
        }

        /// <summary>
        /// Package names for "save all directly in the output folder". Names are normally the setup file's name;
        /// when two selected apps would produce the same name, the app's folder is put in front,
        /// e.g. "Chrome - setup.intunewin" and "Zoom - setup.intunewin".
        /// </summary>
        Dictionary<SetupFile, string> GetFlatOutputNames(List<SetupFile> selected)
        {
            string rootName = Path.GetFileName(txtInput.Text.Trim().TrimEnd('\\'));
            var names = new Dictionary<SetupFile, string>();
            foreach (var group in selected.GroupBy(f => Path.GetFileNameWithoutExtension(f.FileName), StringComparer.OrdinalIgnoreCase))
            {
                bool clash = group.Count() > 1;
                foreach (SetupFile setup in group)
                {
                    string name = Path.GetFileNameWithoutExtension(setup.FileName);
                    if (clash)
                    {
                        string folder = setup.RelativeFolder.Length > 0 ? setup.RelativeFolder.Replace("\\", " - ") : rootName;
                        name = folder + " - " + name;
                    }
                    // Same name in the same folder, e.g. setup.exe and setup.msi.
                    if (names.ContainsValue(name + ".intunewin"))
                        name += " (" + setup.Extension + ")";
                    names[setup] = name + ".intunewin";
                }
            }
            return names;
        }

        void ApplyTheme()
        {
            Theme.Apply(this, chkDarkMode.Checked);
            Theme.StyleAccent(btnCompile);
        }

        void SetBusy(bool isBusy, int total)
        {
            busy = isBusy;
            foreach (Control c in lockWhileBusy)
                c.Enabled = !isBusy;
            UseWaitCursor = isBusy;
            if (isBusy)
            {
                progress.Maximum = total;
                progress.Value = 0;
            }
            UpdateCount();
        }

        void OpenOutputFolder()
        {
            string output = txtOutput.Text.Trim();
            if (Directory.Exists(output))
                Process.Start("explorer.exe", "\"" + output + "\"");
            else
                MessageBox.Show(this, "The output folder doesn't exist yet. It is created when you compile.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void Log(string message)
        {
            txtLog.AppendText(message + Environment.NewLine);
        }

        void LogFromAnyThread(string message)
        {
            if (InvokeRequired)
                BeginInvoke(new Action<string>(Log), message);
            else
                Log(message);
        }

        void SaveSettings()
        {
            settings.ToolPath = txtTool.Text.Trim();
            settings.InputFolder = txtInput.Text.Trim();
            settings.OutputFolder = txtOutput.Text.Trim();
            settings.IncludeSubfolders = chkSubfolders.Checked;
            settings.FlatOutput = chkFlatOutput.Checked;
            settings.DarkMode = chkDarkMode.Checked;
            settings.Save();
        }

        void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (busy && e.CloseReason == CloseReason.UserClosing)
            {
                MessageBox.Show(this, "Packaging is still running. Please wait for it to finish.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                e.Cancel = true;
                return;
            }
            SaveSettings();
        }
    }
}
