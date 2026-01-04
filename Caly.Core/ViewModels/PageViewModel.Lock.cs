using System;
using System.Threading;
using System.Threading.Tasks;

namespace Caly.Core.ViewModels;

public partial class PageViewModel
{
    private readonly SemaphoreSlim _renderMutex = new SemaphoreSlim(1, 1);

    public async Task ExecuteWithRenderLockAsync(Func<CancellationToken, Task> action, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        //if (IsDisposed())
        //{
        //    return default;
        //}

        bool hasLock = false;
        try
        {
            await _renderMutex.WaitAsync(token);
            hasLock = true;

            //if (IsDisposed())
            //{
            //    return default;
            //}

            token.ThrowIfCancellationRequested();
            await action(token);
        }
        finally
        {
            if (hasLock) // && !IsDisposed())
            {
                _renderMutex.Release();
            }
        }
    }
}
