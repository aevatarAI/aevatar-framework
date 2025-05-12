using Microsoft.Extensions.Logging;

namespace Aevatar.Core;

public static class AsyncTaskRunner
{
    public static void RunSafely(Func<Task> taskFactory, ILogger logger)
    {
        Task.Run(async () =>
        {
            try
            {
                await taskFactory().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Unhandled async exception");
            }
        });
    }
}