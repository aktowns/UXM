using System;
using System.IO;
using System.Threading;

namespace UXM
{
    internal static class GameRestorer
    {
        public static string Restore(string exePath, IProgress<(double value, string status)> progress,
            CancellationToken ct)
        {
            progress.Report((0, "Restoring executable..."));
            var gameDir = Path.GetDirectoryName(exePath) ?? throw new ArgumentNullException("exePath");
            var exeName = Path.GetFileName(exePath);

            Util.Game game;
            GameInfo gameInfo;
            try
            {
                game = Util.GetExeVersion(exePath);
                gameInfo = GameInfo.GetGameInfo(game);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }

            if (File.Exists(Path.Combine(gameDir, "_backup", exeName)))
            {
                try
                {
                    File.Delete(exePath);
                    File.Move(Path.Combine(gameDir, "_backup", exeName), exePath);
                }
                catch (Exception ex)
                {
                    return $"Failed to restore executable.\r\n\r\n{ex}";
                }
            }

            if (ct.IsCancellationRequested)
                return null;

            double totalSteps = gameInfo.BackupDirs.Count + gameInfo.DeleteDirs.Count + 1;

            for (var i = 0; i < gameInfo.BackupDirs.Count; i++)
            {
                var restore = gameInfo.BackupDirs[i];
                progress.Report(((i + 1.0) / totalSteps,
                    $"Restoring directory \"{restore}\" ({i + 1}/{gameInfo.BackupDirs.Count})..."));

                var restoreSource = Path.Combine(gameDir, "_backup", restore);
                var restoreTarget = Path.Combine(gameDir, restore);

                if (!Directory.Exists(restoreSource)) continue;
                try
                {
                    if (Directory.Exists(restoreTarget))
                        Directory.Delete(restoreTarget, true);
                    Directory.Move(restoreSource, restoreTarget);
                }
                catch (Exception ex)
                {
                    return $"Failed to restore sounds.\r\n\r\n{ex}";
                }
            }

            try
            {
                for (var i = 0; i < gameInfo.DeleteDirs.Count; i++)
                {
                    var dir = gameInfo.DeleteDirs[i];

                    progress.Report(((i + 1.0 + gameInfo.BackupDirs.Count) / totalSteps,
                        $"Deleting directory \"{dir}\" ({i + 1}/{gameInfo.DeleteDirs.Count})..."));

                    if (ct.IsCancellationRequested)
                        return null;

                    if (Directory.Exists(Path.Combine(gameDir, dir)))
                        Directory.Delete(Path.Combine(gameDir, dir), true);
                }
            }
            catch (Exception ex)
            {
                return $"Failed to delete directory.\r\n\r\n{ex}";
            }

            try
            {
                if (Directory.Exists(gameDir + "\\_backup") && Directory.GetFiles(gameDir + "\\_backup").Length == 0)
                    Directory.Delete(gameDir + "\\_backup");
            }
            catch (Exception ex)
            {
                return $"Failed to delete backup directory.\r\n\r\n{ex}";
            }

            progress.Report((1, "Restoration complete!"));
            return null;
        }
    }
}