using Caly.Core.ViewModels;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Caly.Core.Services
{
    internal sealed class LoadPageMessage(PdfPageViewModel value) : ValueChangedMessage<PdfPageViewModel>(value);
    internal sealed class LoadPageSizeMessage(PdfPageViewModel value) : ValueChangedMessage<PdfPageViewModel>(value);
    internal sealed class LoadThumbnailMessage(PdfPageViewModel value) : ValueChangedMessage<PdfPageViewModel>(value);
    internal sealed class SelectedDocumentChangedMessage(PdfDocumentViewModel value) : ValueChangedMessage<PdfDocumentViewModel>(value);
    internal sealed class UnloadPageMessage(PdfPageViewModel value) : ValueChangedMessage<PdfPageViewModel>(value);
    internal sealed class UnloadThumbnailMessage(PdfPageViewModel value) : ValueChangedMessage<PdfPageViewModel>(value);

    internal sealed class ShowPrintersWindowMessage(bool value) : ValueChangedMessage<bool>(value);
}
