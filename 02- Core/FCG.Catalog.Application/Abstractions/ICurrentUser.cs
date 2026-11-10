namespace FCG.Catalog.Application.Abstractions;

public interface ICurrentUser
{
    long? UserId { get; }

    string? Email { get; }

    string? Role { get; }

    bool IsAdministrator { get; }
}
