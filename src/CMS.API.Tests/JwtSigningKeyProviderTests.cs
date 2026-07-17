using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CMS.API.Tests;

/// <summary>
/// The provider caches the signing key for the process lifetime (LoadKey hits SysConfig via a
/// scoped IAuthRepository only once) — these tests prove that caching a SUCCESS is durable, but
/// a FAILURE is never cached, so a transiently-unavailable DB on the very first request doesn't
/// wedge auth for the rest of the process.
/// </summary>
public class JwtSigningKeyProviderTests
{
    private static IServiceScopeFactory BuildScopeFactory(Mock<IAuthRepository> repoMock)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => repoMock.Object);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    [Fact]
    public void GetSigningKey_DbFailsOnFirstCall_DoesNotCacheFault_RetriesAndSucceeds()
    {
        var repo = new Mock<IAuthRepository>();
        var callCount = 0;
        repo.Setup(r => r.GetSigningKeyAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                return callCount == 1
                    ? Task.FromException<string>(new InvalidOperationException("SysConfig 'appConfig' row was not found."))
                    : Task.FromResult("recovered-signing-key");
            });

        var provider = new JwtSigningKeyProvider(BuildScopeFactory(repo));

        Assert.Throws<InvalidOperationException>(() => provider.GetSigningKey());

        // If the exception had been cached (the pre-fix ExecutionAndPublication behavior), this
        // second call would rethrow the same cached exception forever instead of retrying.
        var key = provider.GetSigningKey();

        Assert.Equal("recovered-signing-key", key);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public void GetSigningKey_Success_IsCachedForSubsequentCalls()
    {
        var repo = new Mock<IAuthRepository>();
        repo.Setup(r => r.GetSigningKeyAsync(It.IsAny<CancellationToken>())).ReturnsAsync("stable-key");

        var provider = new JwtSigningKeyProvider(BuildScopeFactory(repo));

        var first = provider.GetSigningKey();
        var second = provider.GetSigningKey();

        Assert.Equal("stable-key", first);
        Assert.Equal("stable-key", second);
        repo.Verify(r => r.GetSigningKeyAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
