using System.Data;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Syn.WordNet;
using File = System.IO.File;

namespace shared.thesaurus;

public record ThesaurusEntry
{
    public string? word { get; init; }
    public string? wordnet_id { get; init; }
    public string? key { get; init; }
    public string? pos { get; init; }
    [JsonConverter(typeof(EmptyArrayOrStringConverter))]
    public string? synonyms { get; init; }
    public List<string> synonyms_list => !string.IsNullOrWhiteSpace(synonyms)
        ? JsonSerializer.Deserialize<List<string>>(synonyms) ?? new()
        : new();
    [JsonConverter(typeof(EmptyArrayOrStringConverter))]
    public string? desc { get; init; }
    public List<string> desc_list => !string.IsNullOrWhiteSpace(desc)
        ? JsonSerializer.Deserialize<List<string>>(desc) ?? new()
        : new();
}

public class ThesaurusConfiguration
{
    public string DictionaryPath { get; init; } = "wordnet/staticdata/";
    public string DatabasePath { get; init; } = "wordnet/database/wordnet.db";
}

public interface IThesaurus
{
    Task ImportFromJsonl(string filePath);
    void Initialize();
    Task<IEnumerable<string>> Search(string baseWord);
    Task<IEnumerable<string>> SearchFlatFiles(string baseWord);
}

public class Thesaurus : IThesaurus
{
    private WordNetEngine _engine;
    private ThesaurusConfiguration _config = new();
    private SqliteConnection? _connection;
    public Thesaurus(IOptions<ThesaurusConfiguration> config)
    {
        // Initialize the offline WordNet Engine
        _engine = new WordNetEngine();
        _config = config?.Value ?? new();
    }

    public void Initialize()
    {
        _engine?.LoadFromDirectory(Path.GetFullPath(_config.DictionaryPath));
    }

    public async Task<IEnumerable<string>> SearchFlatFiles(string baseWord)
    {
        // Get all synonym sets (synsets) associated with the word
        var synSets = _engine.GetSynSets(baseWord);

        return synSets.SelectMany(x => x.Words);
    }

    public async Task<IEnumerable<string>> Search(string baseWord)
    {
        EnsureConnected();

        const string sql = @"SELECT * FROM thesaurus WHERE word = @word";

        var connection = _connection ?? throw new InvalidOperationException("Thesaurus database is not connected.");
        var command = connection.CreateCommand();

        command.CommandText = sql;
        command.Parameters.AddWithValue("word", baseWord);

        var entries = await connection.QueryAsync<ThesaurusEntry>(sql, new { word = baseWord });

        var synonyms = entries.SelectMany(entry => entry.synonyms_list);

        Disconnect();

        return synonyms;
    }

    public async Task ImportFromJsonl(string filePath)
    {
        filePath = Path.GetFullPath(filePath);

        if (!FileHelper.CanAccess(filePath)) throw new FieldAccessException($"Cannot access file: {filePath} for reading.");

        var lines = File.ReadAllLines(filePath);

        var entries = lines
            .Select(line => JsonSerializer.Deserialize<ThesaurusEntry>(line))
            .Where(entry => entry is not null)
            .Cast<ThesaurusEntry>()
            .ToList();

        if (entries.Any())
            await BuildDatabase(entries);
    }

    private async Task BuildDatabase(IEnumerable<ThesaurusEntry> entries)
    {
        EnsureConnected();

        await Truncate();

        await Create();

        const string sql = "INSERT INTO thesaurus (word, wordnet_id, key, pos, synonyms, desc) VALUES(@word, @wordnet_id, @key, @pos, @synonyms, @desc);";

        var insertingData = entries.Select(entry => new
        {
            word = entry.word,
            wordnet_id = entry.wordnet_id,
            key = entry.key,
            pos = entry.pos,
            synonyms = entry.synonyms,
            desc = entry.desc
        });

        using var transaction = _connection!.BeginTransaction();

        try
        {
            var rowsAffected = await _connection.ExecuteAsync(sql, insertingData);

            transaction.Commit();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error while inserting data.  Rolling back transaction. \n {ex}");
            transaction.Rollback();
            throw;
        }
        Disconnect();
    }

    private void Connect()
    {
        var databasePath = Path.GetFullPath(_config.DatabasePath);

        Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? throw new InvalidOperationException("Unable to determine thesaurus database directory"));

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };

        _connection = new SqliteConnection(builder.ConnectionString);

        _connection.Open();

        Debug.Assert(_connection.Database == "main", $"Expected main, found: {_connection.Database}");
    }

    private void Disconnect()
    {
        if (_connection is not null && _connection.State == ConnectionState.Open)
            _connection.Close();
    }

    private void EnsureConnected()
    {
        if (_connection is null)
        {
            Connect();
            return;
        }

        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
        }
    }

    private const string SqlCreateDatabase = """
        CREATE TABLE IF NOT EXISTS thesaurus (
            word TEXT NOT NULL,
            wordnet_id TEXT,
            key TEXT,
            pos TEXT,
            synonyms TEXT,
            desc TEXT);
        """;
    private async Task Create()
    {
        EnsureConnected();

        string query = SqlCreateDatabase;

        using (var transaction = _connection?.BeginTransaction() ?? throw new NullReferenceException("Null database connection or failure to create transaction"))
        {
            try
            {
                using var command = new SqliteCommand(query, _connection, transaction);
                command.ExecuteNonQuery();
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }

    private async Task Truncate()
    {
        EnsureConnected();

        string query = "DROP TABLE IF EXISTS thesaurus;";

        using (var transaction = _connection?.BeginTransaction() ?? throw new NullReferenceException("Null database connection or failure to create transaction"))
        {
            try
            {
                using var command = new SqliteCommand(query, _connection, transaction);
                command.ExecuteNonQuery();
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }
}
public class EmptyArrayOrStringConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // read from beginning of the array '[]
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            List<string> contents = new();
            // to the end of the array ']'
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                //Add any items to the array
                contents.Add($"\"{reader.GetString()}\"");
            }
            //serialize manually because jsonserializer escape the items
            return $"[{string.Join(',', contents)}]"; // Return your fallback string value
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        // Handle fallback or throw an exception for unexpected types
        using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
        {
            return doc.RootElement.GetRawText();
        }
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
