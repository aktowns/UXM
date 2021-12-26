using SoulsFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UXM
{
    internal static class ArchiveUnpacker
    {
        private const int WRITE_LIMIT = 1024 * 1024 * 100;

        public static string Unpack(string exePath, IProgress<(double value, string status)> progress, CancellationToken ct)
        {
            progress.Report((0, "Preparing to unpack..."));
            var gameDir = Path.GetDirectoryName(exePath) ?? throw new ArgumentNullException("exePath");

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

            Dictionary<string, string> keys = null;
            switch (game)
            {
                case Util.Game.DarkSouls2:
                case Util.Game.Scholar:
                    try
                    {
                        keys = new Dictionary<string, string>();
                        foreach (var archive in gameInfo.Archives)
                        {
                            var pemPath = Path.Combine(gameDir, $@"{archive.Replace("Ebl", "KeyCode")}.pem");
                            keys[archive] = File.ReadAllText(pemPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        return $"Failed to load Dark Souls 2 archive keys.\r\n\r\n{ex}";
                    }

                    break;
                case Util.Game.DarkSouls3:
                    keys = ArchiveKeys.DarkSouls3Keys;
                    break;
                case Util.Game.Sekiro:
                    keys = ArchiveKeys.SekiroKeys;
                    break;
                case Util.Game.SekiroBonus:
                    keys = ArchiveKeys.SekiroBonusKeys;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var drive = Path.GetPathRoot(Path.GetFullPath(gameDir));
            var driveInfo = new DriveInfo(drive);

            if (driveInfo.AvailableFreeSpace < gameInfo.RequiredGB * 1024 * 1024 * 1024)
            {
                var choice = MessageBox.Show(
                    $"{gameInfo.RequiredGB} GB of free space is required to fully unpack this game; " +
                    $"only {driveInfo.AvailableFreeSpace / (1024f * 1024 * 1024):F1} GB available.\r\n" +
                    "If you're only doing a partial unpack to restore some files you may ignore this warning, " +
                    "otherwise it will most likely fail.\r\n\r\n" +
                    "Do you want to continue?",
                    "Space Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (choice == DialogResult.No)
                    return null;
            }

            if (ct.IsCancellationRequested)
                return null;

            try
            {
                for (var i = 0; i < gameInfo.BackupDirs.Count; i++)
                {
                    var backup = gameInfo.BackupDirs[i];
                    progress.Report(((1.0 + (double)i / gameInfo.BackupDirs.Count) / (gameInfo.Archives.Count + 2.0),
                        $"Backing up directory \"{backup}\" ({i + 1}/{gameInfo.BackupDirs.Count})..."));
                    Console.WriteLine($@"Backing up directory ""{backup}"" ({i + 1}/{gameInfo.BackupDirs.Count})...");

                    var backupSource = Path.Combine(gameDir, backup);
                    var backupTarget = Path.Combine(gameDir, "_backup", backup);

                    if (!Directory.Exists(backupSource) || Directory.Exists(backupTarget)) continue;
                    foreach (var file in Directory.GetFiles(backupSource, "*", SearchOption.AllDirectories))
                    {
                        var relative = file.Substring(backupSource.Length + 1);
                        var target = Path.Combine(backupTarget, relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        File.Copy(file, target);
                    }
                }
            }
            catch (Exception ex)
            {
                return $"Failed to back up directories.\r\n\r\n{ex}";
            }

            for (var i = 0; i < gameInfo.Archives.Count; i++)
            {
                if (ct.IsCancellationRequested)
                    return null;

                var archive = gameInfo.Archives[i];
                var error = UnpackArchive(gameDir, archive, keys[archive], i,
                    gameInfo.Archives.Count, gameInfo.BHD5Game, gameInfo.Dictionary, progress, ct).Result;
                if (error != null)
                    return error;
            }

            progress.Report((1, "Unpacking complete!"));
            Console.WriteLine(@"Unpacking complete!");
            return null;
        }

        private static async Task<string> UnpackArchive(string gameDir, string archive, string key, int index, int total,
            BHD5.Game gameVersion, ArchiveDictionary archiveDictionary,
            IProgress<(double value, string status)> progress, CancellationToken ct)
        {
            Console.WriteLine($@"UnpackArchive({gameDir}, {archive}, {index}, {total})");
            progress.Report(((index + 2.0) / (total + 2.0), $"Loading {archive}..."));
            Console.WriteLine($@"Loading {archive}...");
            var bhdPath = Path.Combine(gameDir, $@"{archive}.bhd");
            var bdtPath = Path.Combine(gameDir, $@"{archive}.bdt");

            if (!File.Exists(bhdPath) || !File.Exists(bdtPath)) return null;
            Console.WriteLine(@"Decrypting bhd");
            BHD5 bhd;
            try
            {
                var encrypted = true;
                using (var fs = File.OpenRead(bhdPath))
                {
                    var magic = new byte[4];
                    fs.Read(magic, 0, 4);
                    encrypted = Encoding.ASCII.GetString(magic) != "BHD5";
                }

                if (encrypted)
                {
                    using (var bhdStream = CryptographyUtility.DecryptRsa(bhdPath, key))
                    {
                        bhd = BHD5.Read(bhdStream, gameVersion);
                    }
                }
                else
                {
                    using (var bhdStream = File.OpenRead(bhdPath))
                    {
                        bhd = BHD5.Read(bhdStream, gameVersion);
                    }
                }
            }
            catch (OverflowException ex)
            {
                return $"Failed to open BHD:\n{bhdPath}\n\n{ex}";
            }

            var fileCount = bhd.Buckets.Sum(b => b.Count);

            try
            {
                var asyncFileWriters = new List<Task<long>>();
                using (var bdtStream = File.OpenRead(bdtPath))
                {
                    var currentFile = -1;
                    long writingSize = 0;

                    foreach (var bucket in bhd.Buckets.TakeWhile(bucket => !ct.IsCancellationRequested))
                    {
                        foreach (var header in bucket.TakeWhile(header => !ct.IsCancellationRequested))
                        {
                            currentFile++;

                            string path;
                            bool unknown;
                            if (archiveDictionary.GetPath(header.FileNameHash, out path))
                            {
                                unknown = false;
                                path = Path.Combine(gameDir, path.Replace('/', Path.DirectorySeparatorChar).TrimStart('/'));
                                if (File.Exists(path))
                                    continue;
                            }
                            else
                            {
                                unknown = true;
                                var filename = $"{archive}_{header.FileNameHash:D10}";
                                var directory = Path.Combine(gameDir, "_unknown");
                                path = Path.Combine(directory, filename);
                                if (File.Exists(path) || Directory.Exists(directory) && Directory.GetFiles(directory, $"{filename}.*").Length > 0)
                                    continue;
                            }

                            progress.Report(((index + 2.0 + currentFile / (double)fileCount) / (total + 2.0),
                                $"Unpacking {archive} ({currentFile + 1}/{fileCount})..."));
                            Console.WriteLine($@"Unpacking {archive} ({currentFile + 1}/{fileCount})...");

                            while (asyncFileWriters.Count > 0 && writingSize + header.PaddedFileSize > WRITE_LIMIT)
                            {
                                for (var i = 0; i < asyncFileWriters.Count; i++)
                                {
                                    if (!asyncFileWriters[i].IsCompleted) continue;
                                    writingSize -= await asyncFileWriters[i];
                                    asyncFileWriters.RemoveAt(i);
                                }

                                if (asyncFileWriters.Count > 0 && writingSize + header.PaddedFileSize > WRITE_LIMIT)
                                    Thread.Sleep(10);
                            }

                            byte[] bytes;
                            try
                            {
                                bytes = header.ReadFile(bdtStream);
                                if (unknown)
                                {
                                    var br = new BinaryReaderEx(false, bytes);
                                    if (bytes.Length >= 3 && br.GetASCII(0, 3) == "GFX")
                                        path += ".gfx";
                                    else if (bytes.Length >= 4 && br.GetASCII(0, 4) == "FSB5")
                                        path += ".fsb";
                                    else if (bytes.Length >= 0x19 && br.GetASCII(0xC, 0xE) == "ITLIMITER_INFO")
                                        path += ".itl";
                                    else if (bytes.Length >= 0x10 && br.GetASCII(8, 8) == "FEV FMT ")
                                        path += ".fev";
                                    else if (bytes.Length >= 4 && br.GetASCII(1, 3) == "Lua")
                                        path += ".lua";
                                    else if (bytes.Length >= 4 && br.GetASCII(0, 4) == "DDS ")
                                        path += ".dds";
                                    else if (bytes.Length >= 4 && br.GetASCII(0, 4) == "#BOM")
                                        path += ".txt";
                                    else if (bytes.Length >= 4 && br.GetASCII(0, 4) == "BHF4")
                                        path += ".bhd";
                                    else if (bytes.Length >= 4 && br.GetASCII(0, 4) == "BDF4")
                                        path += ".bdt";
                                    else if (bytes.Length >= 4 && br.GetASCII(0, 4) == "ENFL")
                                        path += ".entryfilelist";
                                    else if (bytes.Length >= 4 && br.GetASCII(0, 4) == "DCX\0")
                                        path += ".dcx";
                                    br.Stream.Close();
                                }
                            }
                            catch (Exception ex)
                            {
                                return $"Failed to read file:\r\n{path}\r\n\r\n{ex}";
                            }

                            try
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(path));
                                writingSize += bytes.Length;
                                asyncFileWriters.Add(WriteFileAsync(path, bytes));
                            }
                            catch (Exception ex)
                            {
                                return $"Failed to write file:\r\n{path}\r\n\r\n{ex}";
                            }
                        }
                    }
                }

                foreach (var task in asyncFileWriters)
                    await task;
            }
            catch (Exception ex)
            {
                return $"Failed to unpack BDT:\r\n{bdtPath}\r\n\r\n{ex}";
            }

            return null;
        }

        private static async Task<long> WriteFileAsync(string path, byte[] bytes)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            {
                await fs.WriteAsync(bytes, 0, bytes.Length);
            }
            return bytes.Length;
        }
    }
}
