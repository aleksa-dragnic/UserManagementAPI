namespace UserManagementAPI.Application.Abstractions;

/// <summary>
/// Turns a password into the string stored in PasswordHash and checks a
/// password against one. The algorithm is an Infrastructure decision — M4 PR14
/// brings Argon2id; until then an interim PBKDF2 implementation stands behind
/// this interface so no handler ever sees a plaintext password reach the domain.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}