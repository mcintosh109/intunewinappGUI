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

            Shown += (s, e) => ScanInputFolder();
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
            txtTool = AddPathRow(layout, 0, "IntuneWinAppUtil.exe:", BrowseTool,
                MakeButton("Download...", (s, e) => Process.Start(IntuneWinAppUtilRunner.DownloadUrl)));

            // Row 1: input folder
            txtInput = AddPathRow(layout, 1, "Input folder:", (s, e) => BrowseFolder(txtInput, "Select the folder that contains your application installers", true),
                MakeButton("Scan", (s, e) => ScanInputFolder()));
            txtInput.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ScanInputFolder(); } };

            // Row 2: output folder
            txtOutput = AddPathRow(layout, 2, "Output folder:", (s, e) => BrowseFolder(txtOutput, "Select where the .intunewin files should be saved", false),
                MakeButton("Open", (s, e) => OpenOutputFolder()));

            // Row 3: list options
            var options = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0), WrapContents = false };
            chkSubfolders = new CheckBox { Text = "Include subfolders", AutoSize = true, Margin = new Padding(3, 7, 12, 3) };
            lblCount = new Label { AutoSize = true, Margin = new Padding(12, 8, 3, 3) };
            options.Controls.Add(chkSubfolders);
            options.Controls.Add(MakeButton("Select all", (s, e) => SetAllChecked(true)));
            options.Controls.Add(MakeButton("Select none", (s, e) => SetAllChecked(false)));
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
            layout.Controls.Add(lstApps, 0, 4);
            layout.SetColumnSpan(lstApps, 4);
            lockWhileBusy.Add(lstApps);

            // Row 5: compile button and progress
            var actions = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0) };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            btnCompile = MakeButton("Compile selected", (s, e) => CompileSelected());
            btnCompile.Font = new Font(Font, FontStyle.Bold);
            btnCompile.Padding = new Padding(8, 2, 8, 2);
            progress = new ProgressBar { Dock = DockStyle.Fill, Margin = new Padding(8, 6, 3, 6) };
            actions.Controls.Add(btnCompile, 0, 0);
            actions.Controls.Add(progress, 1, 0);
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

        TextBox AddPathRow(TableLayoutPanel layout, int row, string label, EventHandler browse, Button extra)
        {
            var text = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(3, 5, 3, 3) };
            var browseButton = MakeButton("Browse...", browse);

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

        void BrowseFolder(TextBox target, string description, bool scanAfter)
        {
            using (var dialog = new FolderBrowserDialog { Description = description, ShowNewFolderButton = true })
            {
                if (Directory.Exists(target.Text))
                    dialog.SelectedPath = target.Text;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                target.Text = dialog.SelectedPath;
                if (scanAfter)
                {
                    // Suggest an output folder next to the input folder if none is set yet.
                    if (string.IsNullOrWhiteSpace(txtOutput.Text))
                        txtOutput.Text = Path.Combine(Path.GetDirectoryName(dialog.SelectedPath) ?? dialog.SelectedPath, "IntuneWin Output");
                    if (string.IsNullOrWhiteSpace(txtTool.Text))
                        txtTool.Text = IntuneWinAppUtilRunner.FindTool(dialog.SelectedPath) ?? string.Empty;
                    ScanInputFolder();
                }
            }
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
            lblCount.Text = string.Format("{0} found, {1} selected", lstApps.Items.Count, lstApps.CheckedItems.Count);
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
                MessageBox.Show(this, "Choose an output folder first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selected = lstApps.CheckedItems.Cast<ListViewItem>().Select(i => (SetupFile)i.Tag).ToList();
            if (selected.Count == 0)
                return;

            SaveSettings();
            SetBusy(true, selected.Count);

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

                    // Keep each app in its own output subfolder so files called e.g. setup.exe don't overwrite each other.
                    string appOutput = Path.Combine(output, setup.RelativeFolder);
                    string result = null;
                    try
                    {
                        result = await IntuneWinAppUtilRunner.PackageAsync(tool, setup, appOutput, LogFromAnyThread);
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
