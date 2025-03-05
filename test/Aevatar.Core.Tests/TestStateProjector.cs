using Aevatar.Core.Abstractions;
using Aevatar.Core.Tests.TestGAgents;

namespace Aevatar.Core.Tests;

public class TestStateProjector : IStateProjector
{
    public async Task ProjectAsync<T>(T state) where T : StateWrapperBase
    {
        if (state is StateWrapper<GroupGAgentState> wrapper)
        {
            var grainId = wrapper.GrainId;
            var groupGAgentState = wrapper.State;
        }
    }
}