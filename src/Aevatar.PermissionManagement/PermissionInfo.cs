namespace Aevatar.PermissionManagement;

public class PermissionInfo
{
    public required string Type { get; set; }
    public required string GroupName { get; set; }
    public required string Name { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}