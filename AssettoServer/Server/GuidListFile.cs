using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Serilog;

namespace AssettoServer.Server;

public class GuidListFile
{
    public readonly IReadOnlyDictionary<string, bool> List;

    private readonly string _filename;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ConcurrentDictionary<string, bool> _guidList = new();
    private readonly FileSystemWatcher _watcher;

    public event EventHandler<GuidListFile, EventArgs>? Reloaded;
    
    public GuidListFile(string filename)
    {
        List = _guidList;
        _filename = filename;
        _watcher = new FileSystemWatcher(".");
        _watcher.NotifyFilter = NotifyFilters.LastWrite;
        _watcher.Filter = _filename;
        _watcher.Changed += OnFileChanged;
        _watcher.EnableRaisingEvents = true;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Deleted)
        {
            Log.Information("File {Path} changed on disk, reloading", _filename);
            _ = Task.Run(LoadAsync);
        }
    }

    public async Task LoadAsync()
    {
        var policy = Policy.Handle<IOException>().WaitAndRetryAsync(3, attempt => TimeSpan.FromMilliseconds(attempt * 100));

        await _lock.WaitAsync();
        try
        {
            if (File.Exists(_filename))
            {
                _guidList.Clear();
                foreach (string guid in await policy.ExecuteAsync(() => File.ReadAllLinesAsync(_filename)))
                {
                    if (_guidList.ContainsKey(guid))
                    {
                        Log.Warning("Duplicate entry in {Path}: {Guid}", _filename, guid);
                    }
                    _guidList[guid] = true;
                }
            }
            else
            {
                await using var _ = File.Create(_filename);
            }

            Log.Debug("Loaded {Path} with {Count} entries", _filename, _guidList.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading {Path}", _filename);
        }
        finally
        {
            _lock.Release();
            Reloaded?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool Contains(string guid)
    {
        _lock.Wait();
        try
        {
            return _guidList.ContainsKey(guid);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AddAsync(string guid)
    {
        await _lock.WaitAsync();
        try
        {
            if (_guidList.TryAdd(guid, true))
                await File.AppendAllLinesAsync(_filename, new[] { guid });
        }
        finally
        {
            _lock.Release();
        }
    }
}
