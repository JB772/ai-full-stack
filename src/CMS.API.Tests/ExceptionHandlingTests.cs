using System.Net;
using System.Net.Http.Json;
using CMS.API.Middleware;
using CMS.API.Models;
using CMS.API.Tests.TestSupport;

namespace CMS.API.Tests;

/// <summary>
/// Tests the global <see cref="ExceptionHandlingMiddleware"/>: an endpoint that throws returns a
/// single consistent 500 with only a generic, safe message — no exception message, stack trace, SQL
/// text or connection details — while the responses that are already meaningful (401 / 403 /
/// validation-400) keep behaving exactly as before.
/// </summary>
public class ExceptionHandlingTests : IDisposable
{
    private readonly CmsApiFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    // ---------- Unexpected exception -> generic 500 ----------

    [Fact]
    public async Task ThrowingEndpoint_Returns500_WithGenericMessageOnly()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/test-errors/throw");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.NotNull(body);
        Assert.Equal(ExceptionHandlingMiddleware.GenericMessage, body!.Message);
    }

    [Fact]
    public async Task ThrowingEndpoint_ResponseBody_LeaksNoExceptionOrSqlDetail()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/test-errors/throw");
        var raw = await response.Content.ReadAsStringAsync();

        // None of the sensitive detail from the thrown exception may appear in the response.
        Assert.DoesNotContain(TestErrorsController.SensitiveMessage, raw);
        Assert.DoesNotContain("Password", raw);
        Assert.DoesNotContain("SELECT", raw);
        Assert.DoesNotContain("Server=", raw);
        Assert.DoesNotContain("InvalidOperationException", raw);
        // A stack trace would show frames like "at CMS.API...".
        Assert.DoesNotContain("   at ", raw);
        Assert.DoesNotContain("StackTrace", raw);
    }

    // ---------- Already-meaningful responses are unchanged ----------

    [Fact]
    public async Task Unauthenticated_StillReturns401_NotWrappedAs500()
    {
        var client = _factory.CreateClient(); // no Authorization header

        var response = await client.GetAsync("/api/app-users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Forbidden_StillReturns403_NotWrappedAs500()
    {
        // Admin-only endpoint reached by a non-Admin role — authorization returns 403, not an exception.
        var client = _factory.CreateAuthenticatedClient("editor", "編輯者", "Editor");

        var response = await client.PostAsJsonAsync("/api/Auth/reset-password",
            new ResetPasswordRequest { UserId = "admin" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ValidationError_StillReturns400_NotWrappedAs500()
    {
        var client = _factory.CreateAuthenticatedClient();

        // Missing required Description -> model validation 400 (produced without throwing).
        var response = await client.PostAsJsonAsync("/api/publish-statuses",
            new PublishStatusRequest { Pkid = 30, Description = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record ErrorBody(string Message);
}
