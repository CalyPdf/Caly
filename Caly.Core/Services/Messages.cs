using System.Collections.Generic;
using System.Threading;
using Avalonia.Platform.Storage;
using Caly.Core.Models;
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

    internal sealed class ShowNotificationMessage(CalyNotification value) : ValueChangedMessage<CalyNotification>(value);

    internal sealed class CopyToClipboardRequestMessage : AsyncRequestMessage<bool>
    {
        public PdfDocumentViewModel PdfDocument { get; }

        public CancellationToken Token { get; }

        public CopyToClipboardRequestMessage(PdfDocumentViewModel document, CancellationToken token)
        {
            PdfDocument = document;
            Token = token;
        }
    }

    internal sealed class ShowPdfPasswordDialogRequestMessage() : AsyncRequestMessage<string?>();

    internal sealed class OpenLoadDocumentsRequestMessage : AsyncRequestMessage<int>
    {
        public IEnumerable<IStorageItem> Documents { get; }

        public CancellationToken Token { get; }

        public OpenLoadDocumentsRequestMessage(IEnumerable<IStorageItem> documents, CancellationToken token)
        {
            Documents = documents;
            Token = token;
        }
    }
}
