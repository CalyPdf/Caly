using Caly.Core.Services;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using Caly.Core.Utilities;
using CommunityToolkit.Mvvm.Messaging;

namespace Caly.Core.ViewModels;

public sealed partial class PdfEmbeddedFileViewModel
{
    public string Name { get; }

    public ReadOnlyMemory<byte> Data { get; }

    public string FileSize { get; }

    public PdfEmbeddedFileViewModel(string name, ReadOnlyMemory<byte> data)
    {
        Name = name;
        Data = data;
        FileSize = Helpers.FormatSizeBytes(Data.Length);
    }

    [RelayCommand]
    private async Task Open()
    {
        _ = await App.Messenger.Send(new OpenEmbeddedFileRequestMessage(this));
    }

    [RelayCommand]
    private async Task SaveAs()
    {
        var file = await App.Messenger.Send(new SaveEmbeddedFileRequestMessage(this));
        file?.Dispose();
    }
}
