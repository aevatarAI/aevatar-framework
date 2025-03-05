namespace Aevatar.Core.Abstractions;

public interface IStateProjector
{
    Task ProjectAsync<T>(T state) where T : StateWrapperBase;
}