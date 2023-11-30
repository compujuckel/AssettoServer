using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Serilog;

namespace AssettoServer.Server.UserGroup;

public class FileBasedUserGroup : IListableUserGroup, IDisposable
{
    public IReadOnlyCollection<ulong> List { get; private set; }

    public delegate FileBasedUserGroup Factory(string name, string path);
    
    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ConcurrentDictionary<ulong, bool> _guidList = new();
    private readonly FileSystemWatcher _watcher;

    public event EventHandler<IUserGroup, EventArgs>? Changed;
    
    public FileBasedUserGroup(SignalHandler signalHandler, string name, string path)
    {
        List = _guidList.Keys.ToList();
        signalHandler.Reloaded += OnManualReload;
        _path = path;
        _watcher = new FileSystemWatcher(".");
        _watcher.NotifyFilter = NotifyFilters.LastWrite;
        _watcher.Filter = _path;
        _watcher.Changed += OnFileChanged;
        _watcher.Error += OnError;
        _watcher.EnableRaisingEvents = true;
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        Log.Error(e.GetException(), "Error monitoring file {Name} for changes", _path);
    }

    private void OnManualReload(SignalHandler sender, EventArgs args)
    {
        Log.Information("Reloading file {Path}", _path);
        _ = Task.Run(LoadAsync);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Deleted)
        {
            Log.Information("File {Path} changed on disk, reloading", _path);
            _ = Task.Run(LoadAsync);
        }
    }

    public async Task LoadAsync()
    {
        var policy = Policy.Handle<IOException>().WaitAndRetryAsync(3, attempt => TimeSpan.FromMilliseconds(attempt * 100));
        
        await _lock.WaitAsync();
        try
        {
            if (File.Exists(_path))
            {
                _guidList.Clear();
                foreach (string guidStr in await policy.ExecuteAsync(() => File.ReadAllLinesAsync(_path)))
                {
                    if (!ulong.TryParse(guidStr, out ulong guid)) continue;

                    if (_guidList.ContainsKey(guid))
                    {
                        Log.Warning("Duplicate entry in {Path}: {Guid}", _path, guid);
                    }
                    _guidList[guid] = true;
                }
            }
            else
            {
                await using var _ = File.Create(_path);
            }

            List = _guidList.Keys.ToList();
            Log.Debug("Loaded {Path} with {Count} entries", _path, _guidList.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading {Path}", _path);
        }
        finally
        {
            _lock.Release();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task<bool> ContainsAsync(ulong guid)
    {
        await _lock.WaitAsync();
        try
        {
            return _guidList.ContainsKey(guid);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> AddAsync(ulong guid)
    {
        await _lock.WaitAsync();
        try
        {
            if (_guidList.TryAdd(guid, true))
                await File.AppendAllLinesAsync(_path, new[] { guid.ToString() });
        }
        finally
        {
            _lock.Release();
        }

        return true;
    }

    public void Dispose()
    {
        _lock.Dispose();
        _watcher.Dispose();
    }
}
