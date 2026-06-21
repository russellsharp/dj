using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Syn.WordNet;

namespace shared.thesaurus;

public class Thesaurus
{
    private WordNetEngine _engine;
    private string _dataDictionaryPath = "wordnet/staticdata/";

    public Thesaurus()
    {
        // Initialize the offline WordNet Engine
        _engine = new WordNetEngine();
    }

    public void Initialize()
    {
        _engine.LoadFromDirectory(Path.GetFullPath(_dataDictionaryPath));
    }

    public async Task<IEnumerable<string>> Search(string baseWord)
    {
        Debug.WriteLine(Path.GetFullPath(_dataDictionaryPath));
        // Get all synonym sets (synsets) associated with the word
        var synSets = _engine.GetSynSets(baseWord);

        Console.WriteLine($"Synonyms for '{baseWord}':");
        foreach (var synSet in synSets)
        {
            // Each synset groups words sharing a specific context/meaning
            foreach (var word in synSet.Words)
            {
                if (!word.Equals(baseWord, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"- {word} ({synSet.PartOfSpeech})");
                }
            }
        }

        return synSets.SelectMany(x => x.Words);
    }
}