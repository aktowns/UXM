﻿using Semver;
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
        private const string UPDATE_LINK = "https://www.nexusmods.com/darksouls3/mods/286?tab=files";
        private static Properties.Settings settings = Properties.Settings.Default;

        private bool closing;
        private CancellationTokenSource cts;
        private Progress<(double value, string status)> progress;

        public FormMain()
        {
            InitializeComponent();

            closing = false;
            cts = null;
            progress = new Progress<(double value, string status)>(ReportProgress);
        }

        private async void FormMain_Load(object sender, EventArgs e)
        {
            Text = "UXM " + Application.ProductVersion;
            EnableControls(true);

            Location = settings.WindowLocation;
            if (settings.WindowSize.Width >= MinimumSize.Width && settings.WindowSize.Height >= MinimumSize.Height)
                Size = settings.WindowSize;
            if (settings.WindowMaximized)
                WindowState = FormWindowState.Maximized;

            txtExePath.Text = settings.ExePath;

            Octokit.GitHubClient gitHubClient = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("UXM"));
            try
            {
                Octokit.Release release = await gitHubClient.Repository.Release.GetLatest("JKAnderson", "UXM");
                if (SemVersion.Parse(release.TagName) > Application.ProductVersion)
                {
                    lblUpdate.Visible = false;
                    LinkLabel.Link link = new LinkLabel.Link();
                    link.LinkData = UPDATE_LINK;
                    llbUpdate.Links.Add(link);
                    llbUpdate.Visible = true;
                }
                else
                {
                    lblUpdate.Text = "App up to date";
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is Octokit.ApiException || ex is ArgumentException)
            {
                lblUpdate.Text = "Update status unknown";
            }
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (cts != null)
            {
                txtStatus.Text = "Aborting...";
                closing = true;
                btnAbort.Enabled = false;
                cts.Cancel();
                e.Cancel = true;
            }
            else
            {
                settings.WindowMaximized = WindowState == FormWindowState.Maximized;
                if (WindowState == FormWindowState.Normal)
                {
                    settings.WindowLocation = Location;
                    settings.WindowSize = Size;
                }
                else
                {
                    settings.WindowLocation = RestoreBounds.Location;
                    settings.WindowSize = RestoreBounds.Size;
                }

                settings.ExePath = txtExePath.Text;
            }
        }

        private void btnAbort_Click(object sender, EventArgs e)
        {
            txtStatus.Text = "Aborting...";
            btnAbort.Enabled = false;
            cts.Cancel();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            ofdExe.InitialDirectory = Path.GetDirectoryName(txtExePath.Text);
            if (ofdExe.ShowDialog() == DialogResult.OK)
                txtExePath.Text = ofdExe.FileName;
        }

        private void btnExplore_Click(object sender, EventArgs e)
        {
            string dir = Path.GetDirectoryName(txtExePath.Text);
            if (Directory.Exists(dir))
                Process.Start(dir);
            else
                SystemSounds.Hand.Play();
        }

        private async void btnPatch_Click(object sender, EventArgs e)
        {
            EnableControls(false);
            cts = new CancellationTokenSource();
            string error = await Task.Run(() => ExePatcher.Patch(txtExePath.Text, progress, cts.Token));

            if (cts.Token.IsCancellationRequested)
            {
                txtStatus.Text = "Patching aborted.";
                pbrProgress.Value = 0;
            }
            else if (error != null)
            {
                txtStatus.Text = "Patching failed.";
                pbrProgress.Value = 0;
                ShowError(error);
            }
            else
            {
                SystemSounds.Asterisk.Play();
            }

            cts.Dispose();
            cts = null;
            EnableControls(true);

            if (closing)
                Close();
        }

        private async void btnRestore_Click(object sender, EventArgs e)
        {
            EnableControls(false);
            cts = new CancellationTokenSource();
            string error = await Task.Run(() => GameRestorer.Restore(txtExePath.Text, progress, cts.Token));

            if (cts.Token.IsCancellationRequested)
            {
                txtStatus.Text = "Restoration aborted.";
                pbrProgress.Value = 0;
            }
            else if (error != null)
            {
                txtStatus.Text = "Restoration failed.";
                pbrProgress.Value = 0;
                ShowError(error);
            }
            else
            {
                SystemSounds.Asterisk.Play();
            }

            cts.Dispose();
            cts = null;
            EnableControls(true);

            if (closing)
                Close();
        }

        private async void btnUnpack_Click(object sender, EventArgs e)
        {
            EnableControls(false);
            cts = new CancellationTokenSource();
            string error = await Task.Run(() => ArchiveUnpacker.Unpack(txtExePath.Text, progress, cts.Token));

            if (cts.Token.IsCancellationRequested)
            {
                txtStatus.Text = "Unpacking aborted.";
                pbrProgress.Value = 0;
            }
            else if (error != null)
            {
                txtStatus.Text = "Unpacking failed.";
                pbrProgress.Value = 0;
                ShowError(error);
            }
            else
            {
                SystemSounds.Asterisk.Play();
            }

            cts.Dispose();
            cts = null;
            EnableControls(true);

            if (closing)
                Close();
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

            pbrProgress.Value = (int)Math.Floor(report.value * pbrProgress.Maximum);
            txtStatus.Text = report.status;
        }
    }
}
