using CommunityToolkit.Mvvm.Messaging;
using System.Threading.Tasks;

namespace Caly.Core.Services
{
    internal partial class PdfDocumentsService
    {
        private void RegisterMessagesHandlers()
        {
            App.Messenger.Register<OpenLoadDocumentsRequestMessage>(this, HandleOpenLoadDocumentsRequestMessage);

            App.Messenger.Register<SelectedDocumentChangedMessage>(this, HandleSelectedDocumentChangedMessage);
            App.Messenger.Register<LoadPageSizeMessage>(this, HandleLoadPageSizeMessage);
            App.Messenger.Register<LoadPageMessage>(this, HandleLoadPageMessage);
            App.Messenger.Register<UnloadPageMessage>(this, HandleUnloadPageMessage);
            App.Messenger.Register<LoadThumbnailMessage>(this, HandleLoadThumbnailMessage);
            App.Messenger.Register<UnloadThumbnailMessage>(this, HandleUnloadThumbnailMessage);

            App.Messenger.Register<CopyToClipboardRequestMessage>(this, HandleCopyToClipboardRequestMessage);

            App.Messenger.Register<ShowNotificationMessage>(this, HandleShowNotificationMessage);
            App.Messenger.Register<ShowPdfPasswordDialogRequestMessage>(this, HandleShowPdfPasswordDialogRequestMessage);
        }

        private void HandleOpenLoadDocumentsRequestMessage(object r, OpenLoadDocumentsRequestMessage m)
        {
            m.Reply(Task.Run(() => OpenLoadDocuments(m.Documents, m.Token)));
        }

        private void HandleShowPdfPasswordDialogRequestMessage(object r, ShowPdfPasswordDialogRequestMessage m)
        {
            m.Reply(_dialogService.ShowPdfPasswordDialogAsync());
        }

        private void HandleShowNotificationMessage(object r, ShowNotificationMessage m)
        {
            _dialogService.ShowNotification(m.Value);
        }

        private void HandleCopyToClipboardRequestMessage(object r, CopyToClipboardRequestMessage m)
        {
            m.Reply(_clipboardService.SetAsync(m.Document, m.Token));
        }

        private void HandleSelectedDocumentChangedMessage(object r, SelectedDocumentChangedMessage m)
        {
            foreach (var openedFile in _openedFiles)
            {
                if (openedFile.Value.ViewModel.Equals(m.Value))
                {
                    openedFile.Value.ViewModel.SetActive();
                    continue;
                }

                openedFile.Value.ViewModel.SetInactive();
            }
        }

        private static void HandleLoadPageSizeMessage(object r, LoadPageSizeMessage m)
        {
            m.Value.PdfService.EnqueueRequestPageSize(m.Value);
        }

        private static void HandleLoadPageMessage(object r, LoadPageMessage m)
        {
            m.Value.PdfService.EnqueueRequestPicture(m.Value);
            m.Value.PdfService.EnqueueRequestTextLayer(m.Value);
        }

        private static void HandleUnloadPageMessage(object r, UnloadPageMessage m)
        {
            m.Value.PdfService.EnqueueRemovePicture(m.Value);
            m.Value.PdfService.EnqueueRemoveTextLayer(m.Value);
        }

        private static void HandleLoadThumbnailMessage(object r, LoadThumbnailMessage m)
        {
            m.Value.PdfService.EnqueueRequestThumbnail(m.Value);
        }

        private static void HandleUnloadThumbnailMessage(object r, UnloadThumbnailMessage m)
        {
            m.Value.PdfService.EnqueueRemoveThumbnail(m.Value);
        }
    }
}
