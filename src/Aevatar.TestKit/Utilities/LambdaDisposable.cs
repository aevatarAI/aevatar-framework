namespace Aevatar.TestKit.Utilities;

internal sealed class LambdaDisposable(Action action) : IDisposable
{
    public void Dispose()
    {
        action?.Invoke();
    }
}
