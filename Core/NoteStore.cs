using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetshUi.Core;

/// <summary>
/// AOT 下反射序列化被禁用，备注字典读写走这份源生成的元数据。
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class NoteStoreJsonContext : JsonSerializerContext
{
}

/// <summary>
/// 转发备注的本地存储。portproxy 本身没有备注字段，这里按
/// 「方向|侦听地址|侦听端口」作为键存到用户目录下的 JSON 文件里。
/// </summary>
internal sealed class NoteStore
{
    // 中文按原文写入，方便直接打开文件查看或手改。
    private static readonly NoteStoreJsonContext Context = new(new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });

    private readonly Lock _gate = new();
    private readonly string _path;
    private readonly Dictionary<string, string> _notes = new(StringComparer.OrdinalIgnoreCase);

    private NoteStore(string path)
    {
        _path = path;
    }

    public string FilePath => _path;

    public static string MakeKey(string kind, string listenAddress, string listenPort) =>
        $"{kind}|{listenAddress}|{listenPort}";

    public static NoteStore Load()
    {
        var store = new NoteStore(ResolvePath());

        try
        {
            if (!File.Exists(store._path)) return store;

            var json = File.ReadAllText(store._path);
            var loaded = JsonSerializer.Deserialize(json, Context.DictionaryStringString);
            if (loaded is not null)
            {
                foreach (var pair in loaded)
                {
                    if (!string.IsNullOrWhiteSpace(pair.Value)) store._notes[pair.Key] = pair.Value;
                }
            }
        }
        catch (Exception)
        {
            // 文件损坏时直接从空备注开始，不影响转发管理本身。
        }

        return store;
    }

    /// <summary>
    /// 优先用程序目录下的 portproxy-notes.json（整个文件夹拷走就能带配置），
    /// 没有就退回 %APPDATA%\NetshUi；两处都没有时也写 %APPDATA%，避免程序目录只读时写失败。
    /// 读到哪个就写回哪个。
    /// </summary>
    private static string ResolvePath()
    {
        var local = Path.Combine(AppContext.BaseDirectory, "portproxy-notes.json");
        if (File.Exists(local)) return local;

        return Path.Combine(AppDataDirectory(), "portproxy-notes.json");
    }

    private static string AppDataDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NetshUi");

    public string Get(string key)
    {
        lock (_gate) return _notes.TryGetValue(key, out var note) ? note : string.Empty;
    }

    /// <summary>只改内存，等待调用方择机 <see cref="Save"/>（边打字边改时用，避免每敲一个键就写盘）。</summary>
    public void SetInMemory(string key, string note)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(note)) _notes.Remove(key);
            else _notes[key] = note.Trim();
        }
    }

    public void Set(string key, string note)
    {
        SetInMemory(key, note);
        Save();
    }

    public void Remove(string key)
    {
        bool changed;
        lock (_gate) changed = _notes.Remove(key);

        if (changed) Save();
    }

    /// <summary>只保留仍然存在的转发规则对应的备注。</summary>
    public void Prune(IEnumerable<string> validKeys)
    {
        lock (_gate)
        {
            var alive = new HashSet<string>(validKeys, StringComparer.OrdinalIgnoreCase);
            var stale = _notes.Keys.Where(key => !alive.Contains(key)).ToArray();
            if (stale.Length == 0) return;

            foreach (var key in stale) _notes.Remove(key);
        }

        Save();
    }

    public void Save()
    {
        try
        {
            string json;
            lock (_gate) json = JsonSerializer.Serialize(_notes, Context.DictionaryStringString);

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, json);
        }
        catch (Exception)
        {
            // 写盘失败不打断操作，备注下次再说。
        }
    }
}
