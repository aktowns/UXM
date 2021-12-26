using System;
using System.IO;

namespace UXM
{
    static class Util
    {
        public static Game GetExeVersion(string exePath)
        {
            if (!File.Exists(exePath))
            {
                throw new FileNotFoundException($"Executable not found at path: {exePath}\r\n"
                    + "Please browse to an existing executable.");
            }

            var filename = Path.GetFileName(exePath);
            switch (filename)
            {
                case "DarkSoulsII.exe":
                {
                    using (var fs = File.OpenRead(exePath))
                    using (var br = new BinaryReader(fs))
                    {
                        fs.Position = 0x3C;
                        var peOffset = br.ReadUInt32();
                        fs.Position = peOffset + 4;
                        var architecture = br.ReadUInt16();

                        switch (architecture)
                        {
                            case 0x014C:
                                return Game.DarkSouls2;
                            case 0x8664:
                                return Game.Scholar;
                            default:
                                throw new InvalidDataException("Could not determine version of DarkSoulsII.exe.\r\n"
                                                               + $"Unknown architecture found: 0x{architecture:X4}");
                        }
                    }
                }
                case "DarkSoulsIII.exe":
                    return Game.DarkSouls3;
                case "sekiro.exe":
                    return Game.Sekiro;
                case "DigitalArtwork_MiniSoundtrack.exe":
                    return Game.SekiroBonus;
                default:
                    throw new ArgumentException($"Invalid executable name given: {filename}\r\n"
                                                + "Executable file name is expected to be DarkSoulsII.exe, DarkSoulsIII.exe, sekiro.exe, or DigitalArtwork_MiniSoundtrack.exe.");
            }
        }

        public enum Game
        {
            DarkSouls2,
            Scholar,
            DarkSouls3,
            Sekiro,
            SekiroBonus,
        }
    }
}
