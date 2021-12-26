using Microsoft.WindowsAPICodePack.Taskbar;
using Semver;
using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UXM
{
    public partial class FormMain : Form
    {
        private const string UPDATE_LINK = "https://www.nexusmods.com/sekiro/mods/26?tab=files";
        private static readonly Properties.Settings _settings = Properties.Settings.Default;

        private bool _closing;
        private CancellationTokenSource _cts;
        private readonly IProgress<(double value, string status)> _progress;

        public FormMain()
        {
            InitializeComponent();

            _closing = false;
            _cts = null;
            _progress = new Progress<(double value, string status)>(ReportProgress);
        }

        private async void FormMain_Load(object sender, EventArgs e)
        {
            Text = @"UXM " + Application.ProductVersion;
            EnableControls(true);

            Location = _settings.WindowLocation;
            if (_settings.WindowSize.Width >= MinimumSize.Width && _settings.WindowSize.Height >= MinimumSize.Height)
                Size = _settings.WindowSize;
            if (_settings.WindowMaximized)
                WindowState = FormWindowState.Maximized;

            txtExePath.Text = _settings.ExePath;

            Octokit.GitHubClient gitHubClient = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("UXM"));
            try
            {
                Octokit.Release release = await gitHubClient.Repository.Release.GetLatest("JKAnderson", "UXM");
                if (SemVersion.Parse(release.TagName) > Application.ProductVersion)
                {
                    lblUpdate.Visible = false;
                    var link = new LinkLabel.Link();
                    link.LinkData = UPDATE_LINK;
                    llbUpdate.Links.Add(link);
                    llbUpdate.Visible = true;
                }
                else
                {
                    lblUpdate.Text = @"App up to date";
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is Octokit.ApiException || ex is ArgumentException)
            {
                lblUpdate.Text = @"Update status unknown";
            }
        }

        private void llbUpdate_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(e.Link.LinkData.ToString());
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_cts != null)
            {
                txtStatus.Text = @"Aborting...";
                _closing = true;
                btnAbort.Enabled = false;
                _cts.Cancel();
                e.Cancel = true;
            }
            else
            {
                _settings.WindowMaximized = WindowState == FormWindowState.Maximized;
                if (WindowState == FormWindowState.Normal)
                {
                    _settings.WindowLocation = Location;
                    _settings.WindowSize = Size;
                }
                else
                {
                    _settings.WindowLocation = RestoreBounds.Location;
                    _settings.WindowSize = RestoreBounds.Size;
                }

                _settings.ExePath = txtExePath.Text;
            }
        }

        private void btnAbort_Click(object sender, EventArgs e)
        {
            txtStatus.Text = @"Aborting...";
            btnAbort.Enabled = false;
            _cts.Cancel();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            ofdExe.InitialDirectory = Path.GetDirectoryName(txtExePath.Text);
            if (ofdExe.ShowDialog() == DialogResult.OK)
                txtExePath.Text = ofdExe.FileName;
        }

        private void btnExplore_Click(object sender, EventArgs e)
        {
            var dir = Path.GetDirectoryName(txtExePath.Text);
            if (Directory.Exists(dir))
                Process.Start(dir);
            else
                SystemSounds.Hand.Play();
        }

        private async void btnPatch_Click(object sender, EventArgs e)
        {
            EnableControls(false);
            _cts = new CancellationTokenSource();
            var error = await Task.Run(() => ExePatcher.Patch(txtExePath.Text, _progress, _cts.Token));

            if (_cts.Token.IsCancellationRequested)
            {
                _progress.Report((0, "Patching aborted."));
            }
            else if (error != null)
            {
                _progress.Report((0, "Patching failed."));
                ShowError(error);
            }
            else
            {
                SystemSounds.Asterisk.Play();
            }

            _cts.Dispose();
            _cts = null;
            EnableControls(true);

            if (_closing)
                Close();
        }

        private async void btnRestore_Click(object sender, EventArgs e)
        {
            var choice = MessageBox.Show("Restoring the game will delete any modified files you have installed.\n" +
                                         "Do you want to proceed?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (choice == DialogResult.No)
                return;

            EnableControls(false);
            _cts = new CancellationTokenSource();
            var error = await Task.Run(() => GameRestorer.Restore(txtExePath.Text, _progress, _cts.Token));

            if (_cts.Token.IsCancellationRequested)
            {
                _progress.Report((0, "Restoration aborted."));
            }
            else if (error != null)
            {
                _progress.Report((0, "Restoration failed."));
                ShowError(error);
            }
            else
            {
                SystemSounds.Asterisk.Play();
            }

            _cts.Dispose();
            _cts = null;
            EnableControls(true);

            if (_closing)
                Close();
        }

        private async void btnUnpack_Click(object sender, EventArgs e)
        {
            EnableControls(false);
            _cts = new CancellationTokenSource();
            var error = await Task.Run(() => ArchiveUnpacker.Unpack(txtExePath.Text, _progress, _cts.Token));

            if (_cts.Token.IsCancellationRequested)
            {
                _progress.Report((0, "Unpacking aborted."));
            }
            else if (error != null)
            {
                _progress.Report((0, "Unpacking failed."));
                ShowError(error);
            }
            else
            {
                SystemSounds.Asterisk.Play();
            }

            _cts.Dispose();
            _cts = null;
            EnableControls(true);

            if (_closing)
                Close();
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void EnableControls(bool enable)
        {
            txtExePath.Enabled = enable;
            btnBrowse.Enabled = enable;
            btnAbort.Enabled = !enable;
            btnRestore.Enabled = enable;
            btnPatch.Enabled = enable;
            btnUnpack.Enabled = enable;
        }

        private void ReportProgress((double value, string status) report)
        {
            if (report.value < 0 || report.value > 1)
                throw new ArgumentOutOfRangeException("Progress value must be between 0 and 1, inclusive.");

            int percent = (int)Math.Floor(report.value * pbrProgress.Maximum);
            pbrProgress.Value = percent;
            txtStatus.Text = report.status;
            if (TaskbarManager.IsPlatformSupported)
            {
                TaskbarManager.Instance.SetProgressValue(percent, pbrProgress.Maximum);
                if (percent == pbrProgress.Maximum && ActiveForm == this)
                    TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.NoProgress);
            }
        }

        private void FormMain_Activated(object sender, EventArgs e)
        {
            if (TaskbarManager.IsPlatformSupported && pbrProgress.Value == pbrProgress.Maximum)
            {
                TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.NoProgress);
            }
        }
    }
}
