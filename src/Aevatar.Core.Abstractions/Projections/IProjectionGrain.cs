namespace Aevatar.Core.Abstractions.Projections;

public interface IProjectionGrain : IGrainWithStringKey
{
    Task ActivateAsync();
}