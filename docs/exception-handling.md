# Exception handling

Centralized on **both** sides. Do not add per-controller `try/catch` for *unexpected* errors, and
do not add a competing Angular interceptor — the two seams below own it.

## Backend — `src/CMS.API/Middleware/ExceptionHandlingMiddleware.cs`

Registered in `Program.cs` **just inside `UseCors`, before `UseAuthentication`** — so CORS headers
are on the error response and the browser can actually read it. It catches any **unhandled**
exception, logs the full detail (message + stack trace) server-side, and returns exactly one
generic `500 {"message":"An unexpected error occurred."}`
(`ExceptionHandlingMiddleware.GenericMessage`) — **never** the stack trace, SQL text, or connection
details.

It deliberately does **not** touch `401` / `403` / validation-`400`: those are produced *without
throwing* (auth middleware, model validation) and so pass through untouched. Only genuinely
unexpected failures become the generic 500. Let exceptions propagate to this middleware rather than
catching-and-reshaping them in a controller.

**Testing.** The API assembly ships no throwing endpoint, so `ExceptionHandlingTests` reaches a
test-only `TestSupport/TestErrorsController` that `CmsApiFactory` wires in via
`AddControllers().AddApplicationPart(...)`. The tests assert the 500 leaks no exception / SQL / stack
detail while `401` / `403` / `400` responses are unchanged.

## Frontend — `authInterceptor` (`features/auth/`)

The **existing** `authInterceptor` — not a new interceptor — is the single place HTTP errors are
handled centrally:

- **5xx** → a friendly PrimeNG toast via `MessageService`, built from the response body's safe
  `message` (falling back to a generic string).
- **401** → clears the session and redirects to `/login`.
- **everything else** (e.g. validation `400`) → re-thrown for the form to handle.

**Testing gotcha.** Because `authInterceptor` now `inject`s `MessageService` on **every** request,
any spec that wires the real interceptor must provide `MessageService` (a `jasmine.createSpyObj`
stub).
