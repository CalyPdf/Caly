using System.Collections.Generic;
using System.Threading;
using Avalonia.Platform.Storage;
using Caly.Core.Models;
using Caly.Core.ViewModels;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Caly.Core.Services
{
    internal sealed class LoadPageMessage(PageViewModel value) : ValueChangedMessage<PageViewModel>(value);
    internal sealed class LoadPageSizeMessage(PageViewModel value) : ValueChangedMessage<PageViewModel>(value);
    internal sealed class LoadThumbnailMessage(PageViewModel value) : ValueChangedMessage<PageViewModel>(value);
    internal sealed class SelectedDocumentChangedMessage(DocumentViewModel value) : ValueChangedMessage<DocumentViewModel>(value);
    internal sealed class UnloadPageMessage(PageViewModel value) : ValueChangedMessage<PageViewModel>(value);
    internal sealed class UnloadThumbnailMessage(PageViewModel value) : ValueChangedMessage<PageViewModel>(value);

    internal sealed class ShowNotificationMessage(CalyNotification value) : ValueChangedMessage<CalyNotification>(value);

    internal sealed class CopyToClipboardRequestMessage : AsyncRequestMessage<bool>
    {
        public DocumentViewModel Document { get; }

        public CancellationToken Token { get; }

        public CopyToClipboardRequestMessage(DocumentViewModel document, CancellationToken token)
        {
            Document = document;
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
