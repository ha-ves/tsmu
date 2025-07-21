/* 
 * Copyright (C) 2025 Tekat, ha-ves
 * 
 * This program is licensed under the GNU Affero General Public License v3 or later.
 * See <https://www.gnu.org/licenses/>.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TyranoScriptMemoryUnlocker.Asar;

#pragma warning disable IDE0130
namespace TyranoScriptMemoryUnlocker.TyranoScript
{
    public static class TyranoScript
    {
        internal static IEnumerable<string> GetUnlockableCG(StreamReader reader)
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length < 4 || line[0] != '[' || line[^1] != ']') continue;
                var inner = line[1..^1].Trim();
                if (inner.StartsWith("cg_image_button "))
                {
                    int storageIdx = inner.IndexOf("graphic=");
                    if (storageIdx != -1)
                    {
                        int valStart = storageIdx + 9;
                        int valEnd = inner.IndexOf('"', valStart);
                        if (valEnd > valStart)
                        {
                            var val = inner[valStart..valEnd]
                                .Split(',', StringSplitOptions.RemoveEmptyEntries);

                            foreach (var v in val)
                                yield return v;
                        }
                    }
                }
            }
        }

        internal static IEnumerable<KeyValuePair<string, string>> GetUnlockableReplay(StreamReader reader)
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length < 4 || line[0] != '[' || line[^1] != ']') continue;
                var inner = line[1..^1].Trim();
                if (inner.StartsWith("setreplay "))
                {
                    int nameIdx = inner.IndexOf("name=");
                    int storageIdx = inner.IndexOf("storage=");

                    if (nameIdx != -1 && storageIdx != -1)
                    {
                        int valStart = nameIdx + 6;
                        int valEnd = inner.IndexOf('"', valStart);
                        if (valEnd > valStart)
                        {
                            var val = inner[valStart..valEnd];

                            int valsStart = storageIdx + 9;
                            int valsEnd = inner.IndexOf('"', valsStart);
                            if (valsEnd > valsStart)
                            {
                                var vals = inner[valsStart..valsEnd];

                                if (val.Length > 0 && vals.Length > 0)
                                {
                                    yield return new KeyValuePair<string, string>(val, vals);
                                }
                            }
                        }
                    }
                }
            }
        }

        internal static IEnumerable<string> GetReplayButton(StreamReader reader)
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length < 4 || line[0] != '[' || line[^1] != ']') continue;
                var inner = line[1..^1].Trim();
                if (inner.StartsWith("replay_image_button "))
                {
                    int storageIdx = inner.IndexOf("name=");
                    if (storageIdx != -1)
                    {
                        int valStart = storageIdx + 6;
                        int valEnd = inner.IndexOf('"', valStart);
                        if (valEnd > valStart)
                        {
                            var val = inner[valStart..valEnd];
                            yield return val;
                        }
                    }
                }
            }
        }

        internal class BaseTag_CG
        {
            public string? Storage { get; set; }
        }

        internal class SetReplayTag : BaseTag_CG
        {
            public string? Name { get; set; }
        }

        internal static IEnumerable<BaseTag_CG> GetCGAndSetreplayTags(StreamReader reader)
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length < 4 || line[0] != '[' || line[^1] != ']') continue;
                var inner = line[1..^1].Trim();
                if (inner.StartsWith("cg "))
                {
                    int storageIdx = inner.IndexOf("storage=");
                    if (storageIdx != -1)
                    {
                        int valStart = storageIdx + 9;
                        int valEnd = inner.IndexOf('"', valStart);
                        if (valEnd > valStart)
                        {
                            var val = inner[valStart..valEnd];
                            yield return new BaseTag_CG { Storage = val };
                        }
                    }
                }
                else if (inner.StartsWith("setreplay "))
                {
                    int nameIdx = inner.IndexOf("name=");
                    int storageIdx = inner.IndexOf("storage=");

                    if (nameIdx != -1 && storageIdx != -1)
                    {
                        int valStart = nameIdx + 6;
                        int valEnd = inner.IndexOf('"', valStart);
                        if (valEnd > valStart)
                        {
                            var val = inner[valStart..valEnd];

                            int valsStart = storageIdx + 9;
                            int valsEnd = inner.IndexOf('"', valsStart);
                            if (valsEnd > valsStart)
                            {
                                var vals = inner[valsStart..valsEnd];

                                if (val.Length > 0 && vals.Length > 0)
                                {
                                    yield return new SetReplayTag
                                    {
                                        Name = val,
                                        Storage = vals
                                    };
                                }
                            }
                        }
                    }
                }
            }
        }

        internal static IEnumerable<AsarFileEntry> FindFilesByExt(AsarArchive asar, string ext, string? searchTopPath = null)
        {
            ext = ext.StartsWith('.') ? ext : '.' + ext;
            var searchdir = searchTopPath is null ? asar.Files : asar.Files[searchTopPath];
            if (searchdir is AsarFileEntry)
            {
                throw new ArgumentException("The provided argument is not a directory in the Asar archive.", nameof(searchTopPath));
            }

            return FindFilesByExt((AsarDirectoryEntry?)searchdir, ext);
        }

        internal static IEnumerable<AsarFileEntry> FindFilesByExt(AsarDirectoryEntry? files, string ext)
        {
            var currdirfiles = files?.EnumerateFiles()?.Where(fl => Path.GetExtension(fl.Name) == ext);
            var nextdirfiles = files?.EnumerateDirectories()?.SelectMany(dir => FindFilesByExt(dir, ext));

            return currdirfiles?.Concat(nextdirfiles ?? []) ?? nextdirfiles?.Concat(currdirfiles ?? []) ?? [];
        }
    }
}
