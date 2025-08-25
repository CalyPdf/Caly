using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Caly.Core.Printing.Models;
using Caly.Core.Printing.Services.Interfaces;

namespace Caly.Core.Printing.Services
{
    public abstract class BasePrintingService : IPrintingService
    {
        public virtual bool IsSupported => false;

        private readonly ChannelWriter<CalyPrintJob> _channelWriter;
        private readonly ChannelReader<CalyPrintJob> _channelReader;

        protected BasePrintingService()
        {
            Channel<CalyPrintJob> fileChannel = Channel.CreateBounded<CalyPrintJob>(new BoundedChannelOptions(50)
            {
                AllowSynchronousContinuations = false,
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropNewest
            }, HandleCannotAddJob);

            _channelWriter = fileChannel.Writer;
            _channelReader = fileChannel.Reader;

            var token = CancellationToken.None;
            _ = Task.Run(() => ProcessingLoop(token), token);
        }

        public bool AddJob(CalyPrintJob printJob)
        {
            return _channelWriter.TryWrite(printJob);
        }

        public async Task ProcessingLoop(CancellationToken token)
        {
            try
            {
                while (await _channelReader.WaitToReadAsync(token).ConfigureAwait(false))
                {
                    var job = await _channelReader.ReadAsync(token).ConfigureAwait(false);
                    Print(job);
                }
            }
            catch (Exception e)
            {
                // Critical error - can't process printing jobs anymore
                System.Diagnostics.Debug.WriteLine($"ERROR in WorkerProc {e}");
                //Debug.WriteExceptionToFile(e);
                //await _dialogService.ShowExceptionWindowAsync(e);
                throw;
            }
        }

        private static void HandleCannotAddJob(CalyPrintJob printJob)
        {
            // TODO - error
        }

        public abstract IEnumerable<CalyPrinterDevice> GetPrinters();
        
        protected abstract Task Print(CalyPrintJob printJob);
    }
}
