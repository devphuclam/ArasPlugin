using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace IdeaCadConnector.Workspace.NormalizeExport
{
    public static class PdmNameNormalizer
    {
        private static readonly Regex Prefix = new Regex(
            @"^(?<code>[A-Za-z][A-Za-z0-9.-]*\d[A-Za-z0-9.-]*)[ _-]+(?<name>.+)$",
            RegexOptions.Compiled);

        public static string NormalizeProjectCode(string value)
        {
            var result = NormalizeSlug(value).Trim('-');
            if (result.Length > 31) result = result.Substring(0, 31).Trim('-');
            if (result.Length < 2) throw new ArgumentException("Project code is invalid.", nameof(value));
            return result;
        }

        public static string DeriveProjectCodeFromRootFileName(string fileName)
        {
            var stem = System.IO.Path.GetFileNameWithoutExtension(fileName ?? string.Empty);
            var pieces = stem.Split(new[] { '_', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (pieces.Count > 1 && Regex.IsMatch(pieces[pieces.Count - 1], @"^[0-9][0-9-]*$"))
                pieces.RemoveAt(pieces.Count - 1);
            return NormalizeProjectCode(string.Join("-", pieces));
        }

        public static PdmNameParts ParseNodeName(string value)
        {
            var source = (value ?? string.Empty).Trim();
            var match = Prefix.Match(source);
            if (match.Success)
            {
                return new PdmNameParts
                {
                    ItemCode = NormalizeCode(match.Groups["code"].Value),
                    DisplayName = NormalizeDisplayName(match.Groups["name"].Value),
                    IsGeneric = false
                };
            }

            var generic = Regex.Match(source, @"^(?<type>Part|Assembly)(?<number>\d+)$", RegexOptions.IgnoreCase);
            if (generic.Success)
            {
                var type = generic.Groups["type"].Value.ToUpperInvariant();
                var number = int.Parse(generic.Groups["number"].Value);
                return new PdmNameParts
                {
                    DisplayName = type + "-" + number.ToString("000"),
                    IsGeneric = true
                };
            }

            return new PdmNameParts
            {
                DisplayName = NormalizeDisplayName(source),
                IsGeneric = true
            };
        }

        public static string NormalizeDisplayName(string value)
        {
            var source = Regex.Replace((value ?? string.Empty).Trim(), @"([a-z0-9])([A-Z])", "$1-$2");
            return NormalizeSlug(source);
        }

        public static string CreateCanonicalFileName(string projectCode, string type, string itemCode, string displayName)
        {
            var normalizedType = (type ?? string.Empty).ToUpperInvariant();
            if (normalizedType != "ASM" && normalizedType != "PRT")
                throw new ArgumentException("Item type must be ASM or PRT.", nameof(type));
            var name = NormalizeProjectCode(projectCode);
            var code = NormalizeCode(itemCode);
            var display = NormalizeDisplayName(displayName);
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(display))
                throw new ArgumentException("Item code and display name are required.");
            return name + "__" + normalizedType + "__" + code + "__" + display + ".ics";
        }

        public static string NormalizeCode(string value)
        {
            return NormalizeSlug(value).Trim('-');
        }

        private static string NormalizeSlug(string value)
        {
            var upper = (value ?? string.Empty).ToUpperInvariant();
            upper = Regex.Replace(upper, @"[_\s]+", "-");
            upper = Regex.Replace(upper, @"[^A-Z0-9.-]", "");
            upper = Regex.Replace(upper, @"-+", "-");
            return upper.Trim('-');
        }
    }
}
