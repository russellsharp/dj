using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WeCantSpell.Hunspell;

namespace shared;

public static class SearchHelpers
{
    //do not remove spaces to preserve tokens for keywords
    private const string CharsToRemove = @"()[],-.";

    //split on spaces to create keywords
    private const string CharsToSplitOn = " ()[],-.";

    public static int MatchString(IEnumerable<string> keywords, string content, CancellationToken token)
    {
        if (string.Join(' ', keywords) == SanitizePath(content)) return keywords.Count();
        var fileWords = SanitizeForSearch(content, token, 3, false);
        return fileWords.Intersect(keywords).Count();
    }

    public static string SanitizePath(string content)
    {
        return new string(content.ToLower().Select(c => !CharsToRemove.Contains(c) ? c : ' ').ToArray());
    }

    public static IEnumerable<string> SanitizeForSearch(string filePath, CancellationToken token, int pathDepth = 3, bool enforceDictionary = true)
    {
        var cleanedFilePath = new string(filePath.Select(c => !CharsToRemove.Contains(c) ? c : ' ').ToArray()).Trim();
        var movieKeywords = cleanedFilePath.Split(CharsToSplitOn.ToCharArray(), StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var sanitizedKeywords = new List<string>();

        if (!enforceDictionary)
        {
            sanitizedKeywords.AddRange(movieKeywords);
        }
        else
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var dicPath = Path.GetFullPath(@"dic/index.dic");
            var affPath = Path.GetFullPath(@"dic/index.aff");
            var dictionary = WordList.CreateFromFiles(dicPath, affPath);

            Debug.WriteLine($"dic: {dicPath}, aff: {affPath}");
            foreach (var word in movieKeywords)
            {
                Debug.WriteLine(word.ToLower());
                //exempt large numbers.  Years screw with the TMDB search.
                if (dictionary.Check(word.ToLower(), token) && (!int.TryParse(word, out int value)))
                {
                    Debug.WriteLine("diced:" + word.ToLower());
                    sanitizedKeywords.Add(word);
                }
            }
        }
        return sanitizedKeywords.ToList();
    }

    public static double Levenshtein(string source, string target)
    {
        if (string.IsNullOrWhiteSpace(source) && string.IsNullOrWhiteSpace(target)) return 100.0;
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target)) return 0.0;
        if (source == target) return 100.0;

        source = source.ToLower();
        target = target.ToLower();

        int sourceLength = source.Length;
        int targetLength = target.Length;

        int[,] distance = new int[sourceLength + 1, targetLength + 1];

        for (int i = 0; i <= sourceLength; distance[i, 0] = i++) { }
        for (int j = 0; j <= targetLength; distance[0, j] = j++) { }

        for (int i = 1; i <= sourceLength; i++)
        {
            for (int j = i; j <= targetLength; j++)
            {
                int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;

                distance[i, j] = Math.Min(Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1), distance[i - 1, j - 1] + cost);
            }
        }

        int totalEdits = distance[sourceLength, targetLength];
        int maxLength = Math.Max(sourceLength, targetLength);

        return (1.0 - ((double)totalEdits / maxLength)) * 100.0;
    }

    public static double GetDiceCoefficient(string str1, string str2)
    {
        // Handle null or empty edge cases
        if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
            return 0.0;

        // If identical, similarity is perfect
        if (str1 == str2)
            return 1.0;

        // Generate bigram lists
        var bigrams1 = GetBigrams(str1);
        var bigrams2 = GetBigrams(str2);

        int totalBigrams = bigrams1.Count + bigrams2.Count;
        if (totalBigrams == 0)
            return 0.0;

        // Count intersections using a frequency dictionary for multiset matching
        var counts1 = GetElementCounts(bigrams1);
        int intersection = 0;

        foreach (var bigram in bigrams2)
        {
            if (counts1.TryGetValue(bigram, out int count) && count > 0)
            {
                intersection++;
                counts1[bigram] = count - 1;
            }
        }

        // Return formula: (2 * intersection) / (total elements)
        return (2.0 * intersection) / totalBigrams;
    }

    private static List<string> GetBigrams(string input)
    {
        var bigrams = new List<string>();
        for (int i = 0; i < input.Length - 1; i++)
        {
            bigrams.Add(input.Substring(i, 2));
        }
        return bigrams;
    }

    private static Dictionary<string, int> GetElementCounts(List<string> list)
    {
        var counts = new Dictionary<string, int>();
        foreach (var item in list)
        {
            if (counts.ContainsKey(item))
                counts[item]++;
            else
                counts[item] = 1;
        }
        return counts;
    }
}