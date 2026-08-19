namespace ParaGateway.Frontend.Services;

public enum ToastKind
{
    Success,
    Error,
    Info
}

public sealed record ToastMessage(Guid Id, string Text, ToastKind Kind);

public sealed class ToastService
{
    private readonly List<ToastMessage> messages = [];

    public IReadOnlyList<ToastMessage> Messages => messages;
    public event Action? Changed;

    public void Success(string text) => Show(text, ToastKind.Success);
    public void Error(string text) => Show(text, ToastKind.Error);
    public void Info(string text) => Show(text, ToastKind.Info);

    public void Dismiss(Guid id)
    {
        messages.RemoveAll(item => item.Id == id);
        Changed?.Invoke();
    }

    private void Show(string text, ToastKind kind)
    {
        var message = new ToastMessage(Guid.NewGuid(), text, kind);
        messages.Add(message);
        Changed?.Invoke();
        _ = AutoDismissAsync(message.Id);
    }

    private async Task AutoDismissAsync(Guid id)
    {
        await Task.Delay(4500);
        Dismiss(id);
    }
}
