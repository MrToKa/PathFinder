using System;
using System.Text.RegularExpressions;

namespace AddinRibbon.Routing
{
    public static class RouteCodeParser
    {
        private static readonly Regex KnownCode = new Regex(
            @"(?<![A-Za-z0-9])/?(?<code>(?:DFBC|DFB|DFC|DFV|VFD|BC|A|B|C)\d+(?:-\d+)?)(?![A-Za-z0-9-])",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex SlashCode = new Regex(
            @"/(?<code>[A-Za-z]{1,12}\d+(?:-\d+)?)(?![A-Za-z0-9-])",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex LastToken = new Regex(@"[A-Za-z0-9][A-Za-z0-9-]*",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>Preserves complete codes including BC/DFBC and numeric piece suffixes.</summary>
        public static string Parse(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "/UNNAMED";
            string code;
            if (TryParseNamedCode(name, out code)) return code;
            var matches = LastToken.Matches(name);
            return matches.Count > 0 ? "/" + matches[matches.Count - 1].Value.ToUpperInvariant() : "/UNNAMED";
        }

        /// <summary>Recognizes a complete code without the display-name fallback.</summary>
        public static bool TryParseNamedCode(string name, out string code)
        {
            code = null;
            if (string.IsNullOrWhiteSpace(name)) return false;
            var matches = KnownCode.Matches(name);
            if (matches.Count == 0) matches = SlashCode.Matches(name);
            if (matches.Count == 0) return false;
            code = "/" + matches[matches.Count - 1].Groups["code"].Value.ToUpperInvariant();
            return true;
        }
    }
}
