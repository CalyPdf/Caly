using System;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using Caly.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Caly.Core.Loggers
{
    internal sealed record CalyLogItem(LogLevel LogLevel, EventId EventId, string Message)
    { }

    // https://learn.microsoft.com/en-us/dotnet/core/extensions/custom-logging-provider

    internal sealed class CalyLogger : ILogger, IAsyncDisposable
    {
        private static readonly string _pipeName = "caly_pdf_logs.pipe";
        private static readonly TimeSpan _connectTimeout = TimeSpan.FromSeconds(5);
        private readonly BlockingCollection<CalyLogItem> _pendingLogs = new();
        private readonly Task _sendMessagesTask;

        public string Name { get; }

        public CalyLogger(string name)
        {
            Name = name;

            _sendMessagesTask = Task.Run(() =>
            {
                // https://learn.microsoft.com/en-us/dotnet/standard/collections/thread-safe/blockingcollection-overview
                while (!_pendingLogs.IsCompleted)
                {
                    // Blocks if dataItems.Count == 0.
                    // IOE means that Take() was called on a completed collection.
                    // Some other thread can call CompleteAdding after we pass the
                    // IsCompleted check but before we call Take.
                    // In this example, we can simply catch the Exception since the
                    // loop will break on the next iteration.
                    try
                    {
                        var logItem = _pendingLogs.Take();

                        SendLogItem(logItem);
                    }
                    catch (OperationCanceledException) { }
                    catch (InvalidOperationException) { }
                }

                _ = _pendingLogs.GetConsumingEnumerable().ToArray();

                App.Current?.Logger.LogInformation("Main log loop finished.");
            });
        }

        private void SendLogItem(CalyLogItem logItem)
        {
            if (_pendingLogs.IsAddingCompleted)
            {
                return;
            }

            try
            {
                using (var pipeClient = new NamedPipeClientStream(".", _pipeName,
                           PipeDirection.Out, PipeOptions.CurrentUserOnly,
                           TokenImpersonationLevel.Identification))
                {
                    pipeClient.Connect(_connectTimeout);

                    string json = JsonSerializer.Serialize(logItem, SourceGenerationContext.Default.CalyLogItem);
                    Memory<byte> pathBytes = Encoding.UTF8.GetBytes(json);
                    Memory<byte> lengthBytes = BitConverter.GetBytes((ushort)pathBytes.Length);
                    pipeClient.Write(lengthBytes.Span);
                    pipeClient.Write(pathBytes.Span);

                    pipeClient.Flush();
                }
            }
            catch (TimeoutException)
            {
                _pendingLogs.CompleteAdding();
            }
            catch (UnauthorizedAccessException uae)
            {
                // Server must be running in admin, but not the client
                // Handle the case and display error message
                Debug.WriteExceptionToFile(uae);
                throw;
            }
            catch (Exception e)
            {
                Debug.WriteExceptionToFile(e);
                throw;
            }
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (_pendingLogs.IsAddingCompleted)
            {
                return;
            }

            var sb = new StringBuilder();
            sb.Append('[');
            sb.Append(Name);
            sb.Append("] ");
            sb.Append(formatter(state, exception));

            if (exception is not null)
            {
                sb.AppendLine();
                sb.AppendLine(exception.ToString());
            }

            _pendingLogs.Add(new CalyLogItem(logLevel, eventId, sb.ToString()));
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return default!;
        }
        
        public async ValueTask DisposeAsync()
        {
            await _sendMessagesTask;
            _pendingLogs.Dispose();
        }
    }
}
