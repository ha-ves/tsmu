/* 
 * Copyright (C) 2025 Tekat, ha-ves
 * 
 * This program is licensed under the GNU Affero General Public License v3 or later.
 * See <https://www.gnu.org/licenses/>.
*/
using CommandLine;
using CommandLine.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TyranoScriptMemoryUnlocker.Asar;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static TyranoScriptMemoryUnlocker.TyranoScript.TyranoScript;

namespace TyranoScriptMemoryUnlocker
{
    public class TSMU
    {
        public class TSMUArgs
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

        private const string SearchTopPath = "data/scenario";
        private const string ScriptExt = "ks";
        private const string CGKsPath = "data/scenario/cg.ks";
        private const string ReplayKsPath = "data/scenario/replay.ks";
        private const string CGViewKey = "cg_view";
        private const string ReplayViewKey = "replay_view";

        private static ILoggerFactory? logger;
        private static ILogger? log;

        static TSMUArgs? Args;

        public static void Main(string[] args)
        {
            Console.OutputEncoding = new UTF8Encoding(false);

            var cmd = Assembly.GetExecutingAssembly()
                .GetName().Name;
            var title = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyTitleAttribute>()?
                .Title;
            var desc = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyDescriptionAttribute>()?
                .Description.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim('\r', '\n', '\t'))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Aggregate((a, b) => a + Environment.NewLine + b);
            var version = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            var build = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            var copr = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyCopyrightAttribute>()?
                .Copyright;
            var trdmk = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyTrademarkAttribute>()?
                .Trademark;
            var lic = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyLicenseAttribute>()?
                .Value;
            var dsclmr = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyDisclaimerAttribute>()?
                .Value;

            var parser = new Parser(with =>
            {
                with.HelpWriter = null;
                with.AutoVersion = false;
                with.AllowMultiInstance = true;
            });
            var parsed = parser.ParseArguments<TSMUArgs>(args);
            parsed.WithParsed(args =>
            {
                if (!Path.Exists(args.SavPath = Path.GetFullPath(args.SavPath)))
                {
                    ExitArgsError($"Error: .sav file path '{args.SavPath}' does not exist.",
                        cmd, title, build, trdmk, lic, dsclmr);
                    return;
                }
                if (!Path.Exists(args.AsarPath = Path.GetFullPath(args.AsarPath)))
                {
                    ExitArgsError($"Error: .asar file path '{args.AsarPath}' does not exist.", 
                        cmd, title, build, trdmk, lic, dsclmr);
                    return;
                }
            })
            .WithNotParsed(errors =>
            {
                if (errors.IsHelp())
                {
                    var helpText = HelpText.AutoBuild(parsed, h =>
                    {
                        h.Heading = $"{title} {build}.";
                        h.Copyright = $"  {copr}. {lic}.{Environment.NewLine}" +
                                      $"  {dsclmr}.{Environment.NewLine}";
                        h.AddEnumValuesToHelpText = true;
                        h.AutoVersion = false;
                        h.AddPreOptionsText($"""
                        Description:
                            {desc}

                        Usage:
                            tsmu [options] -a <path> -s <path>

                        Options:
                        """);
                        return h;
                    }, e => e);

                    Console.WriteLine(helpText);
                    Environment.Exit(0);
                }
                else
                {
                    var err = errors.FirstOrDefault() switch
                    {
                        MissingRequiredOptionError m => $"Missing required option: '-{m.NameInfo.ShortName}' / '--{m.NameInfo.LongName}'",
                        UnknownOptionError u => $"Unknown option: '{(u.Token.Length > 1 ? "--" : '-')}{u.Token}'",
                        CommandLine.Error e => $"Unknown error. ({e.Tag}:{e.GetType()})",
                        _ => "Unknown error."
                    };

                    ExitArgsError(err, cmd, title, build, trdmk, lic, dsclmr);
                }
            });
            Args = parsed.Value;

            logger =
            LoggerFactory.Create(cfg =>
            {
                cfg.ClearProviders().
                AddSimpleConsole(opt =>
                {
                    opt.IncludeScopes = true;
                    opt.SingleLine = true;
                    opt.TimestampFormat = "[HH:mm:ss.ffffff] ";
                });
                cfg.SetMinimumLevel((LogLevel)Math.Max((int)(LogLevel.Information - Args.Verbosity), 0));
            });
            log = logger.CreateLogger<TSMU>();

            if (!File.Exists(Args.AsarPath))
            {
                log.LogError("Error: Can't find asar file ({asar})", Args.AsarPath);
                Environment.Exit(1);
            }
            if (!File.Exists(Args.SavPath))
            {
                log.LogError("Error: Can't find sav file ({sav}).", Args.SavPath);
                Environment.Exit(1);
            }

            if (Args.DryRun)
                log.LogInformation("Dry run mode enabled. No changes will be made to the save file.");

            try
            {
                log.LogDebug("Opening asar file: {asar}", Args.AsarPath);
                using var asar = AsarFile.Open(Args.AsarPath);
                var scripts = FindFilesByExt(asar, ScriptExt, SearchTopPath);

                if (log.IsEnabled(LogLevel.Trace))
                    log.PrintTable(LogLevel.Trace, scripts.Select(s => s.Name), "Scenario scripts: ");

                log.LogDebug("Found {count} script files in asar.", scripts.Count());

                var cgks = asar.Files[CGKsPath] as AsarFileEntry ?? throw new FileNotFoundException("File not found in archive.", CGKsPath);
                var cggallery = GetUnlockableCG(new StreamReader(cgks.ReadAsFileStream(true), Encoding.UTF8)).ToList();

                if (log.IsEnabled(LogLevel.Trace))
                    log.PrintTable(LogLevel.Trace, cggallery, "Unlockable CGs:");

                log.LogInformation("Found {count} Unlockable CGs.", cggallery.Count);

                var replayks = asar.Files[ReplayKsPath] as AsarFileEntry ?? throw new FileNotFoundException("File not found in archive.", ReplayKsPath);
                var replaygallery = GetReplayButton(new StreamReader(replayks.ReadAsFileStream(true), Encoding.UTF8)).ToList();

                if (log.IsEnabled(LogLevel.Trace))
                    log.PrintTable(LogLevel.Trace, replaygallery, "Unlockable Replays:");

                log.LogInformation("Found {count} Unlockable Replays.", replaygallery.Count);

                FileAccess accs = FileAccess.ReadWrite;
                if (Args.DryRun)
                {
                    log.LogDebug("Backup cancelled because of dry run.");
                    accs = FileAccess.Read; // Read-only access for dry run
                }

                log.LogDebug("Opening sav file: {sav}", Args.SavPath);
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
                    log.LogDebug("Backing up save file '{sav}'->'{bak}'", Args.SavPath, bakfile);

                    using (var savbak = new FileStream(bakfile, FileMode.CreateNew, FileAccess.Write))
                        savfs.CopyTo(savbak);
                    savfs.Position = 0;
                }

                log.LogDebug("Parsing list from JSON.");

                using var savread = new StreamReader(savfs, Encoding.UTF8, leaveOpen: true);
                var savun = Uri.UnescapeDataString(savread.ReadToEnd());

                JsonNode savjson;
                Dictionary<string, string> savcgs;
                Dictionary<string, Dictionary<string, string>> savrply;

                try
                {
                    savjson = JsonNode.Parse(savun) ?? throw new JsonException("Invalid .sav file.");

                    savcgs = savjson[CGViewKey].Deserialize<Dictionary<string, string>>() 
                        ?? throw new JsonException($"Invalid ['{CGViewKey}'] in .sav file.");

                    if (log.IsEnabled(LogLevel.Trace))
                        log.PrintTable(LogLevel.Trace, savcgs.Select(kvp => $"{kvp.Key}={kvp.Value}"),
                                "Already Unlocked CGs in save:");

                    savrply = savjson[ReplayViewKey]!.Deserialize<Dictionary<string, Dictionary<string, string>>>() 
                        ?? throw new JsonException($"Invalid ['{ReplayViewKey}'] in .sav file.");

                    if (log.IsEnabled(LogLevel.Trace))
                        log.PrintTable(LogLevel.Trace, savrply.Select(kvp => $"{kvp.Key}={kvp.Value["storage"]}"),
                            "Already Unlocked Replays in save:");
                }
                catch (JsonException exc)
                {
                    log?.LogError(exc, "Corrupted? Is the game running fine? (!!)Delete and re-run the game(!!) ONLY IF YOU ARE SURE (!!)");
                    Environment.Exit(1);
                    return; // Unreachable, but required to satisfy compiler
                }

                savcgs = savcgs.Concat(cggallery.Select(s => new KeyValuePair<string, string>(s, "on")))
                    .GroupBy(kvp => kvp.Key).ToDictionary(g => g.Key, g => g.First().Value);

                //if (log.IsEnabled(LogLevel.Trace))
                //    log.LogTrace("Updated CGs in save: {cgs}", string.Join(", ", savcgs.Select(kvp => $"{kvp.Key}={kvp.Value}")));

                log.LogInformation("Now Unlocked {count} CGs.", savcgs.Count);

                // check if there are replays to add
                if (replaygallery.Except(savrply.Select(s => s.Key)).Any())
                {
                    var replayable = scripts.SelectMany(fl => GetUnlockableReplay(new StreamReader(fl.ReadAsFileStream(true), Encoding.UTF8)));

                    //if (log.IsEnabled(LogLevel.Trace))
                    //    log.LogTrace("Replayable scripts: {replays}", string.Join(", ", replayable.Select(s => $"{s.Key}={s.Value}")));

                    savrply = savrply
                        .Concat(replayable.Select(s => new KeyValuePair<string, Dictionary<string, string>>(s.Key, new Dictionary<string, string> { { "storage", s.Value }, { "target", string.Empty } })))
                        .GroupBy(kvp => kvp.Key).ToDictionary(g => g.Key, g => g.First().Value);

                    //if (log.IsEnabled(LogLevel.Trace))
                    //    log.LogTrace("Updated Replays in save: {replays}", string.Join(", ", savrply.Select(kvp => $"{kvp.Key}={kvp.Value["storage"]}")));

                    log.LogInformation("Now Unlocked {count} Replays.", savrply.Count);
                }

                log.LogDebug("Serializing changes to JSON.");

                savjson[CGViewKey] = JsonSerializer.SerializeToNode(savcgs);
                savjson[ReplayViewKey] = JsonSerializer.SerializeToNode(savrply);

                savfs.Position = 0;
                var savjsoned = JsonSerializer.Serialize(savjson);
                var savjsonen = Uri.EscapeDataString(savjsoned);

                log.LogInformation("Saving changes to the sav file: {sav}", Args.SavPath);

                if (Args.DryRun)
                {
                    log.LogInformation("Dry run mode enabled. No changes will be made to the save file.");
                    log.LogDebug("Would write to {sav}", Args.SavPath);
                }
                else
                {
                    using var savwrite = new StreamWriter(savfs, new UTF8Encoding(false));
                    savwrite.Write(savjsonen);
                    savwrite.Flush();
                }

                log.LogInformation("==================================================");
                log.LogInformation("Done! The save file has been updated successfully.");
                log.LogInformation("Enjoy the unlocked contents in CG or Memory mode!");
                log.LogInformation("Unlocked CGs: {count}", savcgs.Count);
                log.LogInformation("Unlocked Replays: {count}", savrply.Count);
                log.LogInformation("==================================================");
                log.LogInformation("{title} {ver}.", title, build);
                log.LogInformation("{copr}.", copr);
                log.LogInformation("{lic}.", lic);
                log.LogInformation("==================================================");
            }
            catch (Exception exc)
            {
                log?.LogError(exc, "An error occurred while processing the asar or save file.");
                Environment.Exit(1);
            }
        }

        private static void ExitArgsError(string? err, string? cmd, string? title, string? build, string? trdmk, string? lic, string? dsclmr)
        {
            var errText = new HelpText
            {
                Heading = $"{title} {build} {trdmk}.",
                Copyright = $"{lic}. {dsclmr}.{Environment.NewLine}",
            };
            errText.AddPreOptionsLine("ERROR:  " + err);
            errText.AddPreOptionsLine(string.Empty);
            errText.AddPreOptionsLine($"Try '{cmd} --help' for more information.");

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

            // enumerate
            items = [.. items];

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
            List<string> WordWrap(string text, int maxWidth)
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
                string formattedTitle = title.Length > spanWidth ? title.Substring(0, spanWidth) : title;
                int padTotal = spanWidth - formattedTitle.Length;
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
            logger.Log(level, "Table: {title}", title ?? string.Empty);
            if (logger.IsEnabled(level))
            {
                Console.Write(sb.ToString());
            }
        }
    }

    [AttributeUsage(AttributeTargets.Assembly)]
    internal class AssemblyTSMUAttribute(params string?[]? strs) : Attribute
    {
        public string? Value => strs?.Aggregate((a, b) => a + Environment.NewLine + b);

        public string?[]? Values => strs;
    }
    internal class AssemblyLicenseAttribute(params string[]? strs) : AssemblyTSMUAttribute(strs) { };

    internal class AssemblyDisclaimerAttribute(params string[]? strs) : AssemblyTSMUAttribute(strs) { };
}
