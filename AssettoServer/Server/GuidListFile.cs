using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Serilog;

namespace AssettoServer.Server;

public class GuidListFile
{
    public readonly IReadOnlyDictionary<string, bool> List;

    private readonly string _filename;
    private readonly ACServer _server;
    private readonly ConcurrentDictionary<string, bool> _guidList = new();
    private readonly FileSystemWatcher _watcher;
    
    public GuidListFile(ACServer server, string filename)
    {
        List = _guidList;
        _server = server;
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
            Log.Information("File {0} changed on disk, reloading", _filename);
            Task.Run(LoadAsync);
        }
    }

    public async Task LoadAsync()
    {
        await _server.ConnectSemaphore.WaitAsync();
        try
        {
            if (File.Exists(_filename))
            {
                _guidList.Clear();
                foreach (string guid in await File.ReadAllLinesAsync(_filename))
                {
                    if (_guidList.ContainsKey(guid))
                    {
                        Log.Warning("Duplicate entry in {0}: {1}", _filename, guid);
                    }
                    _guidList[guid] = true;
                }
            }
            else
                File.Create(_filename);

            Log.Debug("Loaded {0} with {1} entries", _filename, _guidList.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading {0}", _filename);
        }
        finally
        {
            _server.ConnectSemaphore.Release();
        }
    }

    public bool Contains(string guid)
    {
        return _guidList.ContainsKey(guid);
    }

    public async Task AddAsync(string guid)
    {
        if(_guidList.TryAdd(guid, true))
            await File.AppendAllLinesAsync(_filename, new[] { guid });
    }
}