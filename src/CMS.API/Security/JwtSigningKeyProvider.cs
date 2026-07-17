using CMS.API.Repositories;

namespace CMS.API.Security;

/// <summary>
/// Reads the JWT signing key from SysConfig once (via a scoped <see cref="IAuthRepository"/>)
/// and caches it for the process lifetime — token validation runs on every request, so we
/// avoid a database round-trip each time. A key rotation requires an app restart.
///
/// <see cref="AuthController"/> also mints new tokens through this same cached provider (rather
/// than reading SysConfig directly), so signing and validation always agree on the key in use —
/// they can't drift out of sync mid-process the way two independent reads could.
/// </summary>
public class JwtSigningKeyProvider : IJwtSigningKeyProvider
{
    private readonly IServiceScopeFactory _scopeFactory;

    // PublicationOnly: if the DB is briefly unavailable on the very first call, the thrown
    // exception is NOT cached (unlike ExecutionAndPublication) — the next call retries LoadKey
    // instead of failing every request for the rest of the process's life.
    private readonly Lazy<string> _key;

    public JwtSigningKeyProvider(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        _key = new Lazy<string>(LoadKey, LazyThreadSafetyMode.PublicationOnly);
    }

    public string GetSigningKey() => _key.Value;

    private string LoadKey()
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAuthRepository>();
        return repository.GetSigningKeyAsync().GetAwaiter().GetResult();
    }
}
