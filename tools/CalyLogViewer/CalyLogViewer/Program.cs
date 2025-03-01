// See https://aka.ms/new-console-template for more information
using System.IO.Pipes;
using System.Text.Json;
using CalyLogViewer;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

Console.WriteLine("### Welcome to Caly Log Viewer ###");

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(theme: AnsiConsoleTheme.Sixteen)
    .CreateLogger();

NamedPipeServerStream pipeServer;

try
{
    pipeServer = new("caly_pdf_logs.pipe", PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.CurrentUserOnly);
}
catch (IOException ioe)
{
    if (!ioe.Message.Equals("All pipe instances are busy."))
    {
        throw;
    }
    Console.WriteLine("Caly Log Viewer is already running, press any key to exit.");
    Console.ReadKey();
    return;
}

CancellationToken token = CancellationToken.None;

while (true)
{
    token.ThrowIfCancellationRequested();

    Memory<byte> pathBuffer = Memory<byte>.Empty;
    try
    {
        // https://learn.microsoft.com/en-us/dotnet/standard/io/how-to-use-named-pipes-for-network-interprocess-communication
        await pipeServer.WaitForConnectionAsync(token);

        Memory<byte> lengthBuffer = new byte[2];
        if (await pipeServer.ReadAsync(lengthBuffer, token) != 2)
        {
            // TODO - Log
            continue;
        }

        var len = BitConverter.ToUInt16(lengthBuffer.Span);

        // Read file path
        pathBuffer = new byte[len];

        if (await pipeServer.ReadAsync(pathBuffer, token) != len)
        {
            // TODO - Log
            continue;
        }

        CalyLogItem? logItem = JsonSerializer.Deserialize(pathBuffer.Span, SourceGenerationContext.Default.CalyLogItem);
        
        if (logItem is null)
        {
            Console.WriteLine("Received a null log item from Caly.");
            continue;
        }

        Log.Write(logItem.LogLevel.ToLogEventLevel(), logItem.Message);
    }
    catch (OperationCanceledException)
    {
        // No op
    }
    catch (Exception e)
    {
        Console.WriteLine(e);
        throw;
    }
    finally
    {
        // We are not connected if operation was canceled
        if (pipeServer.IsConnected)
        {
            pipeServer.Disconnect();
        }
    }
}
