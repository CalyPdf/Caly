using CommunityToolkit.Mvvm.Messaging;
using System.Threading.Tasks;

namespace Caly.Core.Services;

internal partial class PdfDocumentsManagerService
{
    private void RegisterMessagesHandlers()
    {
        App.Messenger.Register<OpenLoadDocumentsRequestMessage>(this, HandleOpenLoadDocumentsRequestMessage);
        App.Messenger.Register<SelectedDocumentChangedMessage>(this, HandleSelectedDocumentChangedMessage);
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
        m.Reply(_clipboardService.SetAsync(m.TextSelection, m.PdfPageService, m.Token));
    }

    private void HandleSelectedDocumentChangedMessage(object r, SelectedDocumentChangedMessage m)
    {
        foreach (var openedFile in _openedFiles)
        {
            if (openedFile.Value.Document.Equals(m.Value))
            {
                if (openedFile.Value.Document.IsActive)
                {
                    break;
                }

                openedFile.Value.Document.SetActive();
                continue;
            }

            if (openedFile.Value.Document.IsActive)
            {
                openedFile.Value.Document.SetInactive();
                openedFile.Value.Document.ClearCommand.Execute(null);
            }
        }
    }
}
