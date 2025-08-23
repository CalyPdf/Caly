using Caly.Printing.Models;
using Caly.Printing.Services.Interfaces;
using System.Diagnostics;
using System.Threading.Channels;

namespace Caly.Printing.Services
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

        public void AddJob(CalyPrintJob printJob)
        {
            if (_channelWriter.TryWrite(printJob))
            {
                // TODO - error
            }
        }

        public async Task ProcessingLoop(CancellationToken token)
        {
            try
            {
                while (!await _channelReader.WaitToReadAsync(token).ConfigureAwait(false))
                {
                    var job = await _channelReader.ReadAsync(token);
                    Print(job);
                }
            }
            catch (Exception e)
            {
                // Critical error - can't process printing jobs anymore
                Debug.WriteLine($"ERROR in WorkerProc {e}");
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
        
        protected abstract void Print(CalyPrintJob printJob);
    }
}
