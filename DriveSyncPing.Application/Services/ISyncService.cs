using System;
using System.Threading.Tasks;

namespace DriveSyncPing.Application.Services;

public interface ISyncService
{
    Task RunSyncAsync(Action<string, int> onProgressUpdate);
}
