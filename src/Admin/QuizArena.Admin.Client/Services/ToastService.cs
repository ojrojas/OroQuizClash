namespace QuizArena.Admin.Client.Services;

public sealed record ToastMessage(Guid Id, string Variant, string Title, string? Message);

public sealed class ToastService
{
    public event Action? OnChange;

    public IReadOnlyList<ToastMessage> Toasts { get; private set; } = [];

    public void Show(string variant, string title, string? message = null)
    {
        var toast = new ToastMessage(Guid.NewGuid(), variant, title, message);
        Toasts = [.. Toasts, toast];
        OnChange?.Invoke();
    }

    public void Success(string title, string? message = null) => Show("success", title, message);

    public void Error(string title, string? message = null) => Show("error", title, message);

    public void Info(string title, string? message = null) => Show("info", title, message);

    public void Warning(string title, string? message = null) => Show("warning", title, message);

    public void Dismiss(Guid id)
    {
        Toasts = Toasts.Where(t => t.Id != id).ToList();
        OnChange?.Invoke();
    }
}
