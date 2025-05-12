using EphemeralMongo;

namespace Aevatar.EventSourcing.MongoDB.Tests;

public class AevatarMongoDbFixture : IDisposable
{
    private static readonly IMongoRunner MongoDbRunner;

    static AevatarMongoDbFixture()
    {
        MongoDbRunner = MongoRunner.Run(new MongoRunnerOptions
        {
            UseSingleNodeReplicaSet = true,
            KillMongoProcessesWhenCurrentProcessExits = true
        });
    }

    public static string GetRandomConnectionString()
    {
        return GetConnectionString("Db_" + Guid.NewGuid().ToString("N"));
    }

    public static string GetConnectionString(string databaseName)
    {
        var stringArray = MongoDbRunner.ConnectionString.Split('?');
        var connectionString = stringArray[0] + (stringArray[0].EndsWith('/') ? "" : '/') + databaseName + "/?" +
                               stringArray[1];
        return connectionString;
    }

    public void Dispose()
    {
        MongoDbRunner?.Dispose();
    }
}
