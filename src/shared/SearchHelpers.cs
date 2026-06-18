using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WeCantSpell.Hunspell;

namespace shared
{
    public static class SearchHelpers
    {
        private const string CharsToRemove = " ()[],-.";

        public static int MatchFileName(IEnumerable<string> keywords, string filePath, CancellationToken token)
        {

            if (string.Join(' ', keywords) == SanitizeFileName(filePath, token)) return 1;
            var fileWords = SanitizeForSearch(filePath, false, token);
            return fileWords.Union(keywords).Count();
        }

        public static string SanitizeFileName(string filePath, CancellationToken token)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath).ToLower();
            return new string(fileName.Where(c => !CharsToRemove.Contains(c)).ToArray()).Trim();
        }

        public static IEnumerable<string> SanitizeForSearch(string filePath, bool enforceDictionary, CancellationToken token)
        {
            var movieFileName = Path.GetFileNameWithoutExtension(filePath).ToLower();
            var movieKeywords = movieFileName.Split(CharsToRemove.ToCharArray(), StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var sanitizedKeywords = new List<string>();

            if (!enforceDictionary)
            {
                sanitizedKeywords.AddRange(movieKeywords);
            }
            else
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                var dictionary = WordList.CreateFromFiles(@"dic/index.dic", @"dic/index.aff");

                foreach (var word in movieKeywords)
                {
                    //exempt large numbers.  Years screw with the TMDB search.
                    if (dictionary.Check(word.ToLower(), token) && (!int.TryParse(word, out int value) || word.Length < 3))
                    {
                        sanitizedKeywords.Add(word);
                    }
                }
            }
            return sanitizedKeywords;
        }
    }
}