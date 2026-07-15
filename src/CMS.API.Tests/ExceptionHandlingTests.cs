using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CMS.API.Middleware;
using CMS.API.Repositories;
using CMS.API.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CMS.API.Tests;

/// <summary>
/// Integration tests over the real middleware pipeline: an unhandled exception becomes a safe,
/// generic 500 (logged server-side, nothing sensitive in the body), while the meaningful
/// responses — 401, 403, validation 400 — are unchanged.
/// </summary>
public class ExceptionHandlingTests : IClassFixture<ExceptionHandlingTests.ApiFactory>
{
    private const string TestKey = "exception-integration-test-signing-key-0123456789";

    // Deliberately "sensitive" text that must never reach the client.
    private const string SensitiveMessage =
        "SqlException: SELECT PasswordHash FROM AppUser; Server=.\\SQLEXPRESS;Database=CMS";

    private readonly ApiFactory _factory;

    public ExceptionHandlingTests(ApiFactory factory) => _factory = factory;

    private sealed class StubKeyProvider : IJwtSigningKeyProvider
    {
        public string GetSigningKey() => TestKey;
    }

    /// <summary>Captures server-side log entries so the tests can prove the exception was logged.</summary>
    public sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose() { }

        private sealed class CapturingLogger : ILogger
        {
            private readonly CapturingLoggerProvider _owner;
            public CapturingLogger(CapturingLoggerProvider owner) => _owner = owner;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter)
            {
                _owner.Entries.Enqueue($"{formatter(state, exception)} | {exception}");
            }
        }
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        public Mock<IAppRoleRepository> AppRoleRepo { get; } = new();
        public Mock<IPublishStatusRepository> PublishStatusRepo { get; } = new();
        public Mock<IAuthRepository> AuthRepo { get; } = new();
        public CapturingLoggerProvider Logs { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureLogging(logging => logging.AddProvider(Logs));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IJwtSigningKeyProvider>();
                services.AddSingleton<IJwtSigningKeyProvider>(new StubKeyProvider());

                services.RemoveAll<IAppRoleRepository>();
                services.AddScoped(_ => AppRoleRepo.Object);

                services.RemoveAll<IPublishStatusRepository>();
                services.AddScoped(_ => PublishStatusRepo.Object);

                services.RemoveAll<IAuthRepository>();
                services.AddScoped(_ => AuthRepo.Object);
            });
        }
    }

    private static string AdminToken() =>
        new TokenService()
            .CreateToken("miles@uuu.com.tw", "Miles Sun", new[] { "Admin", "User" }, TestKey)
            .AccessToken;

    private static string NonAdminToken() =>
        new TokenService()
            .CreateToken("reg@uuu.com.tw", "Reg User", new[] { "User" }, TestKey)
            .AccessToken;

    private HttpClient AuthedClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task ThrowingEndpoint_Returns500_WithGenericMessage_AndNoLeak()
    {
        _factory.AppRoleRepo
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(SensitiveMessage));

        var response = await AuthedClient(AdminToken()).GetAsync("/api/app-roles");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(ExceptionHandlingMiddleware.GenericMessage, body);
        // Nothing sensitive leaks: no SQL, no connection details, no exception type, no stack frames.
        Assert.DoesNotContain("SELECT", body);
        Assert.DoesNotContain("SQLEXPRESS", body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain("   at ", body);
    }

    [Fact]
    public async Task ThrowingEndpoint_LogsFullExceptionServerSide()
    {
        _factory.AppRoleRepo
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(SensitiveMessage));

        await AuthedClient(AdminToken()).GetAsync("/api/app-roles");

        var log = string.Join("\n", _factory.Logs.Entries);
        Assert.Contains("Unhandled exception", log);
        Assert.Contains(SensitiveMessage, log);            // full message is in the server log
        Assert.Contains("InvalidOperationException", log); // with the exception type + stack
    }

    [Fact]
    public async Task Unauthenticated_Still401_NotConvertedTo500()
    {
        var response = await _factory.CreateClient().GetAsync("/api/app-roles");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Forbidden_Still403_NotConvertedTo500()
    {
        var response = await AuthedClient(NonAdminToken())
            .PostAsJsonAsync("/api/Auth/reset-password", new { userId = "target@x.com" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ValidationError_Still400_NotConvertedTo500()
    {
        // pkid 0 + blank description trip the controller's ModelState checks.
        var response = await AuthedClient(AdminToken())
            .PostAsJsonAsync("/api/publish-statuses", new { pkid = 0, description = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Pkid", body); // the validation detail still reaches the client
    }
}
