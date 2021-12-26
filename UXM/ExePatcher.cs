using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace UXM
{
    internal static class ExePatcher
    {
        private static readonly Encoding UTF16 = Encoding.Unicode;

        public static string Patch(string exePath, IProgress<(double value, string status)> progress, CancellationToken ct)
        {
            progress.Report((0, "Preparing to patch..."));
            Console.WriteLine(@"Preparing to patch...");
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

            if (game == Util.Game.Sekiro)
            {
                var choice = MessageBox.Show("For Sekiro, most users should use Mod Engine instead of patching with UXM. Patching a vanilla exe will cause the game to crash on startup.\n" +
                                             "Are you sure you want to patch?", "Caution", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (choice == DialogResult.No)
                {
                    progress.Report((1, "Patching cancelled."));
                    return null;
                }
            }

            if (!File.Exists(Path.Combine(gameDir, "_backup", exeName)))
            {
                try
                {
                    Directory.CreateDirectory(Path.Combine(gameDir, "_backup"));
                    File.Copy(exePath, Path.Combine(gameDir, "_backup", exeName));
                }
                catch (Exception ex)
                {
                    return $"Failed to backup file:\r\n{exePath}\r\n\r\n{ex}";
                }
            }

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(exePath);
            }
            catch (Exception ex)
            {
                return $"Failed to read file:\r\n{exePath}\r\n\r\n{ex}";
            }

            try
            {
                for (var i = 0; i < gameInfo.Replacements.Count; i++)
                {
                    if (ct.IsCancellationRequested)
                        return null;

                    var target = gameInfo.Replacements[i];
                    var replacement = "." + new string('/', target.Length - 1);

                    // Add 1.0 for preparation step
                    progress.Report(((i + 1.0) / (gameInfo.Replacements.Count + 1.0), $"Patching alias \"{target}\" ({i + 1}/{gameInfo.Replacements.Count})..."));
                    Console.WriteLine($@"Patching alias ""{target}"" ({i + 1}/{gameInfo.Replacements.Count})...");

                    Console.WriteLine($@"replacing {target} with {replacement}");
                    Replace(bytes, target, replacement);
                }
            }
            catch (Exception ex)
            {
                return $"Failed to patch file:\r\n{exePath}\r\n\r\n{ex}";
            }

            try
            {
                File.WriteAllBytes(exePath, bytes);
            }
            catch (Exception ex)
            {
                return $"Failed to write file:\r\n{exePath}\r\n\r\n{ex}";
            }

            progress.Report((1, "Patching complete!"));
            Console.WriteLine(@"Patching complete!");
            return null;
        }

        private static void Replace(byte[] bytes, string target, string replacement)
        {
            var targetBytes = UTF16.GetBytes(target);
            var replacementBytes = UTF16.GetBytes(replacement);
            if (targetBytes.Length != replacementBytes.Length)
                throw new ArgumentException($"Target length: {targetBytes.Length} | Replacement length: {replacementBytes.Length}");

            var offsets = FindBytes(bytes, targetBytes);
            foreach (var offset in offsets)
                Array.Copy(replacementBytes, 0, bytes, offset, replacementBytes.Length);
        }

        private static List<int> FindBytes(IReadOnlyList<byte> bytes, IReadOnlyCollection<byte> find)
        {
            var offsets = new List<int>();
            for (var i = 0; i < bytes.Count - find.Count; i++)
            {
                var found = !find.Where((t, j) => t != bytes[i + j]).Any();

                if (found)
                    offsets.Add(i);
            }
            return offsets;
        }
    }
}
