using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Tests.TestSupport;

/// <summary>
/// A test-only controller, wired into the pipeline via an MVC application part in
/// <see cref="CmsApiFactory"/>. It exists solely to throw an unhandled exception so the global
/// <c>ExceptionHandlingMiddleware</c> can be exercised end-to-end — it never ships in the API assembly.
///
/// The thrown message deliberately embeds fake-but-sensitive-looking detail (SQL text, a password,
/// a connection string) so a test can assert none of it leaks into the response body.
/// </summary>
[ApiController]
[Route("api/test-errors")]
public class TestErrorsController : ControllerBase
{
    public const string SensitiveMessage =
        "SELECT PasswordHash FROM AppUser -- Server=db01;Database=CMS;User Id=sa;Password=SuperSecret123;";

    /// <summary>Throws to simulate an unexpected failure escaping a controller/repository.</summary>
    [HttpGet("throw")]
    public IActionResult Throw() => throw new InvalidOperationException(SensitiveMessage);
}
