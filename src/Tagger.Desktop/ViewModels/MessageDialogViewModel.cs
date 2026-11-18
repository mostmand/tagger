namespace Tagger.Desktop.ViewModels;

public sealed class MessageDialogViewModel
{
    public MessageDialogViewModel(
        string title,
        string message,
        string confirmLabel = "Continue",
        string cancelLabel = "Cancel",
        bool isConfirmation = false)
    {
        Title = title;
        Message = message;
        ConfirmLabel = confirmLabel;
        CancelLabel = cancelLabel;
        IsConfirmation = isConfirmation;
    }

    public string Title { get; }

    public string Message { get; }

    public string ConfirmLabel { get; }

    public string CancelLabel { get; }

    public bool IsConfirmation { get; }
}
