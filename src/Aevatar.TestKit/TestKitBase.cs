namespace Aevatar.TestKit;

/// <summary>A unit test base class that provides a default mock grain activation context.</summary>
public abstract class TestKitBase<TSilo> where TSilo : TestKitSilo, new()
{
    protected TSilo Silo { get; } = new();
}