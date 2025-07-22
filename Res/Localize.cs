using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TyranoScriptMemoryUnlocker.Res
{
    internal class LocalizedArgsSentenceBuild : SentenceBuilder
    {
        public override Func<string> RequiredWord => () => LocalizedString.HelpTextRequired;

        public override Func<string> OptionGroupWord => () => string.Empty;

        public override Func<string> ErrorsHeadingText => () => string.Empty;

        public override Func<string> UsageHeadingText => () => string.Empty;

        public override Func<bool, string> HelpCommandText => (ishelp) => ishelp ? LocalizedString.HelpTextHelp : string.Empty;

        public override Func<bool, string> VersionCommandText => (a) => string.Empty;

        public override Func<Error, string> FormatError => (a) => string.Empty;

        public override Func<IEnumerable<MutuallyExclusiveSetError>, string> FormatMutuallyExclusiveSetErrors => (a) => string.Empty;
    }
}
