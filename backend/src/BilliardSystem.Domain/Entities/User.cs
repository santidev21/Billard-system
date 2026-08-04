using BilliardSystem.Domain.Common;
using BilliardSystem.Domain.Enums;

namespace BilliardSystem.Domain.Entities;

public sealed class User : Entity
{
    private User()
    {
    }

    public User(string displayName, string userName, string passwordHash, UserRole role)
    {
        DisplayName = displayName;
        UserName = userName;
        PasswordHash = passwordHash;
        Role = role;
    }

    public string DisplayName { get; private set; } = string.Empty;
    public string UserName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; } = true;
}
