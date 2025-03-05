namespace Aevatar.PermissionManagement;

[GenerateSerializer]
public class UserContext
{
    [Id(0)] public Guid UserId { get; set; }
    [Id(1)] public string[] Roles { get; set; } = [];
    [Id(2)] public string UserName { get; set; } = string.Empty;
    [Id(3)] public string Email { get; set; } = string.Empty;
    [Id(4)] public string ClientId { get; set; } = string.Empty;
}