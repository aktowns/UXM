using SoulsFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace UXM
{
    class GameInfo
    {
        public long RequiredGB;
        public BHD5.Game BHD5Game;
        public List<string> Archives;
        public ArchiveDictionary Dictionary;
        public List<string> BackupDirs;
        public List<string> DeleteDirs;
        public List<string> Replacements;

        public GameInfo(string xmlStr, string dictionaryStr)
        {
            Dictionary = new ArchiveDictionary(dictionaryStr);

            XDocument xml = XDocument.Parse(xmlStr);
            RequiredGB = long.Parse(xml.Root.Element("required_gb").Value);
            BHD5Game = (BHD5.Game)Enum.Parse(typeof(BHD5.Game), xml.Root.Element("bhd5_game").Value);
            Archives = xml.Root.Element("archives").Elements().Select(element => element.Value).ToList();
            BackupDirs = xml.Root.Element("backup_dirs").Elements().Select(element => element.Value).ToList();
            DeleteDirs = xml.Root.Element("delete_dirs").Elements().Select(element => element.Value).ToList();
            Replacements = xml.Root.Element("replacements").Elements().Select(element => element.Value).ToList();
        }

        public static GameInfo GetGameInfo(Util.Game game)
        {
            string prefix;
            switch (game)
            {
                case Util.Game.DarkSouls2:
                    prefix = "DarkSouls2";
                    break;
                case Util.Game.Scholar:
                    prefix = "Scholar";
                    break;
                case Util.Game.DarkSouls3:
                    prefix = "DarkSouls3";
                    break;
                case Util.Game.Sekiro:
                    prefix = "Sekiro";
                    break;
                case Util.Game.SekiroBonus:
                    prefix = "SekiroBonus";
                    break;
                default:
                    throw new ArgumentException("Invalid game type.");
            }
            
#if DEBUG
            var basePath = Path.Combine("..", "..", "dist", "res");
            var gameInfo = File.ReadAllText(Path.Combine(basePath, $@"{prefix}GameInfo.xml"));
            var dictionary = File.ReadAllText(Path.Combine(basePath, $@"{prefix}Dictionary.txt"));
#else
            string gameInfo = File.ReadAllText(Path.Combine("res", $@"{prefix}GameInfo.xml"));
            string dictionary = File.ReadAllText(Path.Combine("res", $@"{prefix}Dictionary.txt"));
#endif
            return new GameInfo(gameInfo, dictionary);
        }
    }
}
