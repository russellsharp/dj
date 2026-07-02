using System.Text;
using WeCantSpell.Hunspell;

namespace shared;


public static class SearchHelpers
{
    //do not remove spaces to preserve tokens for keywords
    private const string CharsToRemove = @"_()[],-.";

    //split on spaces to create keywords
    private const string CharsToSplitOn = " _()[],-.";

    public static int MatchString(IEnumerable<string> keywords, string content, CancellationToken token)
    {
        if (string.Join(' ', keywords) == SanitizeString(content)) return keywords.Count();
        var fileWords = SanitizeForSearch(content, token, false);
        return fileWords.Intersect(keywords).Count();
    }

    public static string SanitizeString(string content)
    {
        return new string(content.ToLower().Select(c => !CharsToRemove.Contains(c, StringComparison.InvariantCultureIgnoreCase) ? c : ' ').ToArray()).Trim();
    }

    public static IEnumerable<string> SanitizeForSearch(string query, CancellationToken token, bool enforceDictionary = true)
    {
        var cleanedFilePath = SanitizeString(query);
        var movieKeywords = cleanedFilePath.ToLowerInvariant().Split(CharsToSplitOn.ToCharArray(), StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var sanitizedKeywords = new List<string>();

        if (!enforceDictionary)
        {
            sanitizedKeywords.AddRange(movieKeywords);
        }
        else
        {
            var dict = new Dictionary();
            sanitizedKeywords.AddRange(movieKeywords.Where(word => dict.Check(word, token)));
        }
        return sanitizedKeywords.ToList();
    }
}

internal class Dictionary
{
    private string _dicPath = @"dic/index.dic";
    private string _affPath = @"dic/index.aff";
    private WordList? _dictionary = null;
    private bool initialized = false;

    public void Initialize()
    {
        if (!initialized)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _dictionary = WordList.CreateFromFiles(Path.GetFullPath(_dicPath), Path.GetFullPath(_affPath));
            initialized = true;
        }
    }

    public bool Check(string word, CancellationToken token)
    {
        string cwd = Directory.GetCurrentDirectory();
        Console.WriteLine($"Dic directory cwd {cwd}, GetFullPath {Path.GetFullPath(_affPath)}");
        Initialize();
        //exempt large numbers.  Years screw with the TMDB search.
        return _dictionary!.Check(word.ToLower(), token) && (!int.TryParse(word, out int value));
    }
}
