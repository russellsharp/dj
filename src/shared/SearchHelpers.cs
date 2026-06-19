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
        //do not remove spaces to preserve tokens for keywords
        private const string CharsToRemove = "()[],-.";

        //split on spaces to create keywords
        private const string CharsToSplitOn = " ()[],-.";

        public static int MatchString(IEnumerable<string> keywords, string content, CancellationToken token)
        {
            if (string.Join(' ', keywords) == SanitizeContent(content, token)) return keywords.Count();
            var fileWords = SanitizeForSearch(content, token, 3, false);
            return fileWords.Intersect(keywords).Count();
        }

        public static string SanitizeContent(string content, CancellationToken token)
        {
            var fileName = Path.GetFileNameWithoutExtension(content).ToLower();
            return fileName.Where(c => !CharsToRemove.Contains(c)).ToString().Trim();
        }

        public static IEnumerable<string> SanitizeForSearch(string filePath, CancellationToken token, int pathDepth = 3, bool enforceDictionary = true)
        {
            var movieKeywords = filePath.Split(CharsToSplitOn.ToCharArray(), StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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