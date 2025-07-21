/* 
 * Copyright (C) 2025 Tekat, ha-ves
 * 
 * This program is licensed under the GNU Affero General Public License v3 or later.
 * See <https://www.gnu.org/licenses/>.
*/
using CommandLine;
using CommandLine.Text;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TyranoScriptMemoryUnlocker.Asar;
using TyranoScriptMemoryUnlocker.Res;
using static TyranoScriptMemoryUnlocker.TyranoScript.TyranoScript;

namespace TyranoScriptMemoryUnlocker
{
    public class TSMU
    {
        public class TSMUArgs()
        {
            [Option('v', "verbose", HelpText = "Increase verbosity. Can be stacked -vv, up to 2 levels.", FlagCounter = true)]
            public int Verbosity { get; set; }

            [Option('a', "asar", Required = true, HelpText = "Path to the app.asar file containing the game scripts. (typically in 'resources/')")]
            public string AsarPath { get; set; } = string.Empty;

            [Option('s', "sav", Required = true, HelpText = "Path to the sav(e) file to modify. (typically in game top folder)")]
            public string SavPath { get; set; } = string.Empty;

            [Option("dry", HelpText = "Dry run mode. Only show what would be done, without modifying the save file.")]
            public bool DryRun { get; set; } = false;
        }

        internal const string SearchTopPath = "data/scenario";
        internal const string ScriptExt = "ks";
        internal const string CGKsPath = "data/scenario/cg.ks";
        internal const string ReplayKsPath = "data/scenario/replay.ks";
        internal const string CGViewKey = "cg_view";
        internal const string ReplayViewKey = "replay_view";
        internal const string ViewStorageKey = "storage";
        internal const string ViewTargetKey = "target";
        internal const string CGEnableValue = "on";

        private static ILoggerFactory? logger;
        private static ILogger? log;

        static TSMUArgs? Args;

#pragma warning disable IDE0079 // Remove unnecessary suppression
        [SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", 
            Justification = Suppressions.JsonTrimmingJustification)]
#pragma warning restore IDE0079 // Remove unnecessary suppression
        public static void Main(string[] args)
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            LocalizedString.Culture = System.Globalization.CultureInfo.CurrentCulture;

            var cmd = Assembly.GetExecutingAssembly()
                .GetName().Name;
            var title = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyTitleAttribute>()?
                .Title;
            var desc = string.Format(LocalizedString.Desc, title);
            var build = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            if (build?.IndexOf('+') is int idx && idx > 0)
                // only get up to the short commit hash if present
                build = build[..(idx + 14)];
            var copr = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyCopyrightAttribute>()?
                .Copyright;
            var lic = LocalizedString.AppLic;
            var dsclmr = LocalizedString.AppDisclaimer;

            var parser = new Parser(with =>
            {
                with.HelpWriter = null;
                with.AutoVersion = false;
                with.AllowMultiInstance = true;
            });
            var parsed = parser.ParseArguments(() => new TSMUArgs()
            {
                AsarPath = string.Empty,
                SavPath = string.Empty,
                Verbosity = 0,
                DryRun = false
            }, args);
            parsed.WithParsed(args =>
            {
                if (!Path.Exists(args.SavPath = Path.GetFullPath(args.SavPath)))
                {
                    ExitArgsError(string.Format(LocalizedString.ErrorSavNotFound, args.SavPath),
                        cmd, title, build, copr, lic, dsclmr);
                    return;
                }
                if (!Path.Exists(args.AsarPath = Path.GetFullPath(args.AsarPath)))
                {
                    ExitArgsError(string.Format(LocalizedString.ErrorAsarNotFound, args.AsarPath), 
                        cmd, title, build, copr, lic, dsclmr);
                    return;
                }
            })
            .WithNotParsed(errors =>
            {
                if (errors.IsHelp())
                {
                    var helpText = HelpText.AutoBuild(parsed, h =>
                    {
                        h.AddEnumValuesToHelpText = true;
                        h.AutoVersion = false;

                        h.Heading = $"{title} {build}. {copr}.";
                        h.Copyright = $"{lic} {dsclmr} {Environment.NewLine}";
                        h.AddPreOptionsText(string.Format(LocalizedString.HelpTextDesc, desc, Environment.NewLine));
                        return h;
                    }, e => e);

                    Console.WriteLine(helpText);
                    Environment.Exit(0);
                }
                else
                {
                    var err = errors.FirstOrDefault() switch
                    {
                        MissingRequiredOptionError m => string.Format(LocalizedString.ArgMissing, $"  '-{m.NameInfo.ShortName}' / '--{m.NameInfo.LongName}'"),
                        UnknownOptionError u => string.Format(LocalizedString.ArgUnknown, $"  '{(u.Token.Length > 1 ? "--" : '-')}{u.Token}'"),
                        NamedError n => string.Format(LocalizedString.ArgInvalid, $"  '{n.NameInfo.NameText}'  "),
                        CommandLine.Error e => $"{LocalizedString.ArgErrorUnknown} ({e.Tag}:{e.GetType()})",
                        _ => LocalizedString.ArgErrorUnknown
                    };

                    ExitArgsError(err, cmd, title, build, copr, lic, dsclmr);
                }
            });
            Args = parsed.Value;

            logger = LoggerFactory.Create(cfg =>
            {
                cfg.ClearProviders().AddSimpleConsole(opt =>
                {
                    opt.IncludeScopes = opt.SingleLine = true;
                    opt.TimestampFormat = "[HH:mm:ss.ffffff]";
                });
                cfg.SetMinimumLevel((LogLevel)Math.Max((int)(LogLevel.Information - Args.Verbosity), 0));
            });
            log = logger.CreateLogger(nameof(TSMU));
            var tablelog = LoggerFactory.Create(cfg =>
            {
                cfg.ClearProviders().AddSimpleConsole(opt =>
                {
                    opt.IncludeScopes = true;
                    opt.TimestampFormat = "[HH:mm:ss.ffffff]";
                    opt.SingleLine = false;
                });
                cfg.SetMinimumLevel((LogLevel)Math.Max((int)(LogLevel.Information - Args.Verbosity), 0));
            })
            .CreateLogger(nameof(TSMU));

            log.LogInformation("{title} {build} {copr}. {lic} {dsclmr}", title, build, copr, lic, dsclmr);

            if (Args.DryRun)
                log.LogInformation("{Dry}", LocalizedString.DryModeNotice);

            try
            {
                log.LogDebug("{asar}", string.Format(LocalizedString.OpenAsar, Args.AsarPath));
                using var asar = AsarFile.Open(Args.AsarPath);
                var scripts = FindFilesByExt(asar, ScriptExt, SearchTopPath).ToList();

                if (log.IsEnabled(LogLevel.Trace))
                    tablelog.PrintTable(LogLevel.Trace, scripts.Select(s => s.Name), LocalizedString.FoundScripts, widthOfTable: 76);

                log.LogDebug("{count}", string.Format(LocalizedString.FoundAsarScripts, scripts.Count));

                var cgks = asar.Files[CGKsPath] as AsarFileEntry ?? throw new FileNotFoundException("File not found in archive.", CGKsPath);
                var cggallery = GetUnlockableCG(new StreamReader(cgks.ReadAsFileStream(true), Encoding.UTF8)).ToList();

                if (log.IsEnabled(LogLevel.Trace))
                    tablelog.PrintTable(LogLevel.Trace, cggallery, LocalizedString.UnlockableCGs, widthOfTable: 76);

                log.LogInformation("{count}", string.Format(LocalizedString.FoundCGs, cggallery.Count));

                var replayks = asar.Files[ReplayKsPath] as AsarFileEntry ?? throw new FileNotFoundException("File not found in archive.", ReplayKsPath);
                var replaygallery = GetReplayButton(new StreamReader(replayks.ReadAsFileStream(true), Encoding.UTF8)).ToList();

                if (log.IsEnabled(LogLevel.Trace))
                    tablelog.PrintTable(LogLevel.Trace, replaygallery, LocalizedString.UnlockableReplays, widthOfTable: 76);

                log.LogInformation("{count}", string.Format(LocalizedString.FoundReplays, replaygallery.Count));

                FileAccess accs = FileAccess.ReadWrite;
                if (Args.DryRun)
                    accs = FileAccess.Read; // Read-only access for dry run

                log.LogInformation("{sav}", string.Format(LocalizedString.OpenSav, Args.SavPath));
                using var savfs = new FileStream(Args.SavPath, FileMode.Open, accs);
                string bakfile;
                int iter = 0;
                do
                {
                    bakfile = Path.ChangeExtension(Args.SavPath, $"bak{DateTime.Now:yyyyMMddHHmmss}.{iter++}{Path.GetExtension(Args.SavPath)}");
                }
                while (File.Exists(bakfile));

                if (!Args.DryRun)
                {
                    log.LogDebug("{bak}", string.Format(LocalizedString.SavBackup, Args.SavPath, bakfile));

                    using (var savbak = new FileStream(bakfile, FileMode.CreateNew, FileAccess.Write))
                        savfs.CopyTo(savbak);
                    savfs.Position = 0;
                }
                else
                {
                    log.LogDebug("{bak}", LocalizedString.SavBackupCancel);
                    log.LogDebug("{bak}", string.Format(LocalizedString.SavBackupDry, Args.SavPath, bakfile));
                }

                log.LogDebug("{json}", LocalizedString.SavJsonParse);

                using var savread = new StreamReader(savfs, Encoding.UTF8, leaveOpen: true);
                var savun = Uri.UnescapeDataString(savread.ReadToEnd());

                JsonNode savjson;
                List<string> savcgs;
                List<KeyValuePair<string, string>> savrply;

                try
                {
                    savjson = JsonNode.Parse(savun) ?? throw new JsonException(LocalizedString.ExcInvalidSav);

                    savcgs = [..(savjson[CGViewKey] as JsonObject)?.Select(p => p.Key)
                        ?? throw new JsonException(string.Format(LocalizedString.ExcInvalidView, CGViewKey))];

                    if (log.IsEnabled(LogLevel.Trace))
                        tablelog.PrintTable(LogLevel.Trace, savcgs, LocalizedString.UnlockedCGsAlr, widthOfTable: 76);

                    savrply = [..(savjson[ReplayViewKey] as JsonObject)?.Select(p => 
                                new KeyValuePair<string, string>(p.Key, p.Value![ViewStorageKey]!.GetValue<string>()))
                        ?? throw new JsonException(string.Format(LocalizedString.ExcInvalidView, ReplayViewKey))];

                    if (log.IsEnabled(LogLevel.Trace))
                        tablelog.PrintTable(LogLevel.Trace, savrply.Select(kvp => $"{kvp.Key}={kvp.Value}"),
                            LocalizedString.UnlockedReplaysAlr, widthOfTable: 76);
                }
                catch (JsonException exc)
                {
                    log.LogError(exc, "{exc}", LocalizedString.JsonExc);
                    Environment.Exit(1);
                    return; // Unreachable, but required to satisfy compiler
                }

                var cgremain = cggallery.Except(savcgs);

                if (cgremain.Any())
                    log.LogInformation("{count}", string.Format(LocalizedString.UnlockedCGs, cgremain.Count()));

                savcgs = [.. savcgs, .. cgremain];

                var savcgsdict = savcgs.GroupBy(k => k).ToDictionary(g => g.Key, k => CGEnableValue);

                // check if there are replays to add
                Dictionary<string, Dictionary<string, string>>? savrplydict = null;
                var replayremain = replaygallery.Except(savrply.Select(s => s.Key));
                if (replayremain.Any())
                {
                    /// only get the <see cref="ViewStorageKey"/> but maybe some replays use the <see cref="ViewTargetKey"/> too...
                    /// TODO: check if the <see cref="ViewTargetKey"/> is used in the replays.
                    var replayable = scripts.SelectMany(fl => GetUnlockableReplay(new StreamReader(fl.ReadAsFileStream(true), Encoding.UTF8)));

                    log.LogInformation("{count}", string.Format(LocalizedString.UnlockedReplays, replayremain.Count()));

                    savrply = [.. savrply, .. replayable];

                    savrplydict = savrply.GroupBy(k => k.Key).ToDictionary(g => g.Key,
                                                    g => new Dictionary<string, string>
                                                    {
                                                        [ViewStorageKey] = g.Last().Value,
                                                        [ViewTargetKey] = string.Empty
                                                    });
                }

                log.LogDebug("{json}", LocalizedString.SavJsonSerialize);

                savjson[CGViewKey] = JsonSerializer.SerializeToNode(savcgsdict,JsonDictSerializeContext.Default.CGViewKey);
                savjson[ReplayViewKey] = JsonSerializer.SerializeToNode(savrplydict, JsonDictSerializeContext.Default.ReplayViewKey);

                savfs.Position = 0;
                var savjsoned = savjson.ToJsonString();
                var savjsonen = Uri.EscapeDataString(savjsoned);

                if (Args.DryRun)
                {
                    log.LogInformation("{dry}", LocalizedString.DryModeNotice);
                    log.LogDebug("{sav}", string.Format(LocalizedString.SavingSavDry, Args.SavPath));
                }
                else
                {
                    log.LogInformation("{sav}", string.Format(LocalizedString.SavingSav, Args.SavPath));
                    using var savwrite = new StreamWriter(savfs, new UTF8Encoding(false));
                    savwrite.Write(savjsonen);
                    savwrite.Flush();
                }

                log.LogInformation("{sym}", LocalizedString.LogLineDone);
                log.LogInformation("{msg}", LocalizedString.AppSuccess_Line1);
                log.LogInformation("{msg}", LocalizedString.AppSuccess_Line2);
                log.LogInformation("{msg}", string.Format(LocalizedString.AppSuccess_Line3, savcgsdict.Count));
                if (savrplydict != null)
                    log.LogInformation("{msg}", string.Format(LocalizedString.AppSuccess_Line4, savrplydict.Count));
                else
                    log.LogInformation("{msg}", string.Format(LocalizedString.AppSuccess_Line4, savrply.Count));
                log.LogInformation("{sym}", LocalizedString.LogLine);
                log.LogInformation("{sym}", LocalizedString.LogLineTY);
                log.LogInformation("{title} {ver}", title, build);
                log.LogInformation("{copr}. {lic}", copr, lic);
                log.LogInformation("{sym}", LocalizedString.LogLine);
            }
            catch (Exception exc)
            {
                log?.LogError(exc, "{exc}", LocalizedString.ErrorAsarSav);
                Environment.Exit(1);
            }
        }

        private static void ExitArgsError(string? err, string? cmd, string? title, string? build, string? copr, string? lic, string? dsclmr)
        {
            var errText = new HelpText
            {
                Heading = $"{title} {build}. {copr}.",
                Copyright = $"{lic}. {dsclmr}." + Environment.NewLine,
            };
            errText.AddPreOptionsLine(err);
            errText.AddPreOptionsLine(string.Empty);
            errText.AddPreOptionsLine(string.Format(LocalizedString.ErrorHelpCmd, cmd));

            Console.WriteLine(errText);
            Environment.Exit(1);
        }
    }

    public static class LogExtensions
    {
        public static void PrintTable(this ILogger logger, LogLevel level, IEnumerable<string?>? items, string? title = null, int widthOfTable = 80, int forceCols = -1)
        {
            if (items == null || !items.Any())
                return;

            // Helper function to calculate display width (counts kana and fullwidth characters as 2)
            int GetDisplayWidth(string text)
            {
                int width = 0;
                foreach (var c in text)
                {
                    if ((c >= '\u3040' && c <= '\u309F') || // Hiragana
                        (c >= '\u30A0' && c <= '\u30FF') || // Katakana
                        (c >= '\uFF01' && c <= '\uFF60') || // Fullwidth punctuation and symbols
                        (c >= '\u4E00' && c <= '\u9FFF'))   // CJK Unified Ideographs (Kanji)
                    {
                        width += 2;
                    }
                    else
                    {
                        width += 1;
                    }
                }
                return width;
            }

            // Helper function to truncate a string by display width
            string TruncateToDisplayWidth(string text, int maxWidth)
            {
                int width = 0;
                var sb = new StringBuilder();
                foreach (var c in text)
                {
                    int charWidth = ((c >= '\u3040' && c <= '\u309F') ||
                                     (c >= '\u30A0' && c <= '\u30FF') ||
                                     (c >= '\uFF01' && c <= '\uFF60')) ? 2 : 1;
                    if (width + charWidth > maxWidth)
                        break;
                    sb.Append(c);
                    width += charWidth;
                }
                return sb.ToString();
            }

            // Define padding to add extra spaces between columns.
            int padding = 2;
            // In PrintTable, ensure items are not null before using .Average(s => s.Length).
            double avgLen = items!.Average(s => s!.Length);
            int tentativeCellWidth = (int)Math.Ceiling(avgLen);
            // Determine effective width using the provided widthOfTable.
            int availableWidth = Math.Min(Console.WindowWidth, widthOfTable);
            // Compute the number of columns based on tentative cell width.
            int computedCols = forceCols > 0 ? forceCols : Math.Max(1, availableWidth / (tentativeCellWidth + padding + 3));
            int columns = computedCols;
            // Recalculate cellWidth so that the table always maximizes to the availableWidth.
            // TotalWidth = columns * (cellWidth + 2) + (columns + 1) should equal availableWidth.
            int cellWidth = (availableWidth - (3 * columns + 1)) / columns;

            // Group items into rows based on the computed column count.
            var rowsList = items
                .Select((val, idx) => new { val, idx })
                .GroupBy(x => x.idx / columns)
                .Select(g => g.Select(x => x.val).ToList())
                .ToList();

            // Local word-wrap function: splits a string into lines of maximum width (cellWidth).
            static List<string> WordWrap(string text, int maxWidth)
            {
                var wrapped = new List<string>();
                if (string.IsNullOrEmpty(text))
                {
                    wrapped.Add(string.Empty);
                    return wrapped;
                }
                var words = text.Split(' ');
                var line = new StringBuilder();
                foreach (var word in words)
                {
                    if (line.Length + word.Length + 1 > maxWidth)
                    {
                        if (line.Length > 0)
                        {
                            wrapped.Add(line.ToString());
                            line.Clear();
                        }
                        // If the single word is longer than maxWidth, split it.
                        string tempWord = word;
                        while (tempWord.Length > maxWidth)
                        {
                            wrapped.Add(tempWord[..maxWidth]);
                            tempWord = tempWord[maxWidth..];
                        }
                        line.Append(tempWord);
                    }
                    else
                    {
                        if (line.Length > 0)
                            line.Append(' ');
                        line.Append(word);
                    }
                }
                if (line.Length > 0)
                    wrapped.Add(line.ToString());
                return wrapped;
            }

            // Calculate total width: each cell has vertical borders.
            int totalWidth = columns * (cellWidth + 2) + (columns + 1);
            string horizontalLine = new('-', totalWidth);

            var sb = new StringBuilder();
            sb.AppendLine(horizontalLine);

            if (!string.IsNullOrEmpty(title))
            {
                // Create a single row for the title spanning all columns.
                int spanWidth = totalWidth - 2; // subtracting borders
                string formattedTitle = GetDisplayWidth(title) > spanWidth ? TruncateToDisplayWidth(title, spanWidth) : title;
                int titleDisplayWidth = GetDisplayWidth(formattedTitle);
                int padTotal = spanWidth - titleDisplayWidth;
                int padLeft = padTotal / 2;
                int padRight = padTotal - padLeft;
                formattedTitle = new string(' ', padLeft) + formattedTitle + new string(' ', padRight);
                sb.AppendLine("|" + formattedTitle + "|");
                sb.AppendLine(horizontalLine);
            }

            for (int i = 0; i < rowsList.Count; i++)
            {
                var row = rowsList[i];
                // Fix for CS8604: Ensure 'cell' is not null before passing to WordWrap
                var wrappedCells = row.Select(cell => WordWrap(cell ?? string.Empty, cellWidth)).ToList();
                int rowHeight = wrappedCells.Max(wrap => wrap.Count);

                for (int lineIdx = 0; lineIdx < rowHeight; lineIdx++)
                {
                    sb.Append('|');
                    foreach (var cellWrap in wrappedCells)
                    {
                        string cellLine = lineIdx < cellWrap.Count ? cellWrap[lineIdx] : string.Empty;
                        sb.Append(' ');
                        sb.Append(cellLine.PadRight(cellWidth));
                        sb.Append(" |");
                    }
                    sb.AppendLine();
                }
                sb.AppendLine(horizontalLine);
            }
            logger.Log(level, "{table}", sb.ToString());
        }
    }

    [JsonSourceGenerationOptions(WriteIndented = false)]
    [JsonSerializable(typeof(Dictionary<string, string>), 
        GenerationMode = JsonSourceGenerationMode.Serialization, 
        TypeInfoPropertyName = nameof(TSMU.CGViewKey))]
    [JsonSerializable(typeof(Dictionary<string, Dictionary<string, string>>), 
        GenerationMode = JsonSourceGenerationMode.Serialization,
        TypeInfoPropertyName = nameof(TSMU.ReplayViewKey))]
    internal partial class JsonDictSerializeContext : JsonSerializerContext { }
}
