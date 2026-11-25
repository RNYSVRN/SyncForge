using System.Text;
using System.Text.RegularExpressions;
using SyncForge.Core.Models;

namespace SyncForge.Core.Filtering;

/// <summary>
/// Wertet Ein-/Ausschlussregeln für relative Pfade aus.
/// Musterregeln (Groß-/Kleinschreibung egal):
/// <list type="bullet">
/// <item><c>*.tmp</c> – ohne Pfadtrenner: gilt für den Datei-/Ordnernamen auf jeder Ebene.</item>
/// <item><c>node_modules\</c> – mit abschließendem Trenner: nur Ordner.</item>
/// <item><c>Projekte\*\bin\</c> – mit Trenner: gilt für den relativen Pfad ab Quellordner.</item>
/// <item><c>*</c> = beliebig innerhalb einer Ebene, <c>**</c> = über Ebenen hinweg, <c>?</c> = ein Zeichen.</item>
/// </list>
/// </summary>
public sealed class PathFilter
{
    private readonly List<Rule> _excludes;
    private readonly List<Rule> _includes;
    private readonly FilterSettings _settings;

    public PathFilter(FilterSettings settings)
    {
        _settings = settings;
        _excludes = Parse(settings.ExcludePatterns);
        _includes = Parse(settings.IncludePatterns);
    }

    public static PathFilter None { get; } = new(new FilterSettings { ExcludePatterns = [], ExcludeSystem = false });

    public bool IsDirectoryExcluded(string relativePath, FileAttributes attributes)
    {
        if (IsAttributeExcluded(attributes)) return true;
        return _excludes.Any(r => r.Matches(relativePath, isDirectory: true));
    }

    public bool IsFileExcluded(string relativePath, FileAttributes attributes, long size)
    {
        if (IsAttributeExcluded(attributes)) return true;
        if (_excludes.Any(r => r.Matches(relativePath, isDirectory: false))) return true;
        if (_includes.Count > 0 && !_includes.Any(r => r.Matches(relativePath, isDirectory: false))) return true;
        if (_settings.MinSizeBytes > 0 && size < _settings.MinSizeBytes) return true;
        if (_settings.MaxSizeBytes > 0 && size > _settings.MaxSizeBytes) return true;
        return false;
    }

    private bool IsAttributeExcluded(FileAttributes attributes) =>
        (_settings.ExcludeHidden && attributes.HasFlag(FileAttributes.Hidden)) ||
        (_settings.ExcludeSystem && attributes.HasFlag(FileAttributes.System));

    private static List<Rule> Parse(IEnumerable<string> patterns) =>
        patterns
            .Select(p => p.Trim())
            .Where(p => p.Length > 0 && !p.StartsWith('#'))
            .Select(Rule.Create)
            .ToList();

    private sealed class Rule
    {
        private readonly Regex _regex;
        private readonly bool _directoryOnly;
        private readonly bool _matchFullPath;

        private Rule(Regex regex, bool directoryOnly, bool matchFullPath)
        {
            _regex = regex;
            _directoryOnly = directoryOnly;
            _matchFullPath = matchFullPath;
        }

        public static Rule Create(string pattern)
        {
            var p = pattern.Replace('/', '\\');
            var directoryOnly = p.EndsWith('\\');
            p = p.Trim('\\');
            var fullPath = p.Contains('\\');
            return new Rule(new Regex(GlobToRegex(p), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), directoryOnly, fullPath);
        }

        public bool Matches(string relativePath, bool isDirectory)
        {
            if (_directoryOnly && !isDirectory) return false;
            var candidate = _matchFullPath ? relativePath : Path.GetFileName(relativePath);
            return _regex.IsMatch(candidate);
        }

        private static string GlobToRegex(string glob)
        {
            var sb = new StringBuilder("^");
            for (var i = 0; i < glob.Length; i++)
            {
                var c = glob[i];
                if (c == '*')
                {
                    if (i + 1 < glob.Length && glob[i + 1] == '*')
                    {
                        sb.Append(".*");
                        i++;
                        if (i + 1 < glob.Length && glob[i + 1] == '\\') i++; // "**\" = null oder mehr Ebenen
                    }
                    else
                    {
                        sb.Append(@"[^\\]*");
                    }
                }
                else if (c == '?')
                {
                    sb.Append(@"[^\\]");
                }
                else
                {
                    sb.Append(Regex.Escape(c.ToString()));
                }
            }
            return sb.Append('$').ToString();
        }
    }
}
