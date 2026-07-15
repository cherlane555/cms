using CMS.API.Repositories;

namespace CMS.API.Security;

/// <summary>
/// Reads the JWT signing key from SysConfig once (via a scoped <see cref="IAuthRepository"/>)
/// and caches it for the process lifetime — token validation runs on every request, so we
/// avoid a database round-trip each time. A key rotation requires an app restart.
/// </summary>
public class JwtSigningKeyProvider : IJwtSigningKeyProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Lazy<string> _key;

    public JwtSigningKeyProvider(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        _key = new Lazy<string>(LoadKey, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public string GetSigningKey() => _key.Value;

    private string LoadKey()
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAuthRepository>();
        return repository.GetSigningKeyAsync().GetAwaiter().GetResult();
    }
}
