using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using DbConnect.Core.Abstractions;
using DbConnect.Core.Models;
using Newtonsoft.Json;

namespace DbConnect.Core.Services;

public sealed class FileProfileStore : IProfileStore
{
    private readonly string _path;
    private readonly object _lock = new();

    public FileProfileStore(string? path = null)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _path = path ?? Path.Combine(home, ".dbconnect", "profiles.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        if (!File.Exists(_path)) File.WriteAllText(_path, "[]");
    }

    public Task<IReadOnlyList<ConnectionProfile>> ListAsync()
    {
        var list = Read();
        return Task.FromResult<IReadOnlyList<ConnectionProfile>>(list);
    }

    public Task<ConnectionProfile?> GetAsync(string name)
    {
        var p = Read().FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(p);
    }

    public Task UpsertAsync(ConnectionProfile profile)
    {
        lock (_lock)
        {
            var list = Read().ToList();
            var idx = list.FindIndex(x => string.Equals(x.Name, profile.Name, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) list[idx] = profile; else list.Add(profile);
            Write(list);
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string name)
    {
        lock (_lock)
        {
            var list = Read().Where(x => !string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
            Write(list);
        }
        return Task.CompletedTask;
    }

    private List<ConnectionProfile> Read()
    {
        lock (_lock)
        {
            var json = File.ReadAllText(_path);
            return JsonConvert.DeserializeObject<List<ConnectionProfile>>(json) ?? new();
        }
    }

    private void Write(List<ConnectionProfile> list)
    {
        var json = JsonConvert.SerializeObject(list, Formatting.Indented);
        File.WriteAllText(_path, json);
    }
}
