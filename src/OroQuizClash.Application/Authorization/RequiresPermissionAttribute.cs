using OroQuizClash.Domain.Authorization;

namespace OroQuizClash.Application.Authorization;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class RequiresPermissionAttribute : Attribute
{
    public Permission Permission { get; }

    public RequiresPermissionAttribute(string permissionName)
    {
        Permission = Permission.All.FirstOrDefault(p => p.Name == permissionName)
            ?? throw new ArgumentException($"Unknown permission: {permissionName}", nameof(permissionName));
    }

    public RequiresPermissionAttribute(int permissionId)
    {
        Permission = Permission.All.FirstOrDefault(p => p.Id == permissionId)
            ?? throw new ArgumentException($"Unknown permission id: {permissionId}", nameof(permissionId));
    }
}
