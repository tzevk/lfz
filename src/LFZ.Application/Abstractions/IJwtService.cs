namespace LFZ.Application.Abstractions;

/// <summary>Issues signed JWTs for API clients. Implemented in Infrastructure.</summary>
public interface IJwtService
{
    string GenerateToken(string userId, string email, string userName, string fullName, IList<string> roles);
}
