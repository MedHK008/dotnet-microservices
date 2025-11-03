using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

var serviceEndpoints = builder.Configuration
    .GetSection("ServiceEndpoints")
    .Get<Dictionary<string, string>>()
    ?? throw new InvalidOperationException("ServiceEndpoints configuration section is missing.");

foreach (var endpoint in serviceEndpoints)
{
    if (!Uri.TryCreate(endpoint.Value, UriKind.Absolute, out var uri))
    {
        throw new InvalidOperationException($"Service endpoint '{endpoint.Key}' is not a valid absolute URI.");
    }

    builder.Services.AddHttpClient(endpoint.Key, client =>
    {
        client.BaseAddress = uri;
    });
}

builder.Services.AddHttpClient();

var app = builder.Build();

var whiteListedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "/api/auth/login",
    "/api/auth/register",
    "/api/auth/validate"
};

var routeMappings = new List<RouteMapping>
{
    new("/api/auth", "AuthService"),
    new("/api/products", "CatalogService"),
    new("/api/cart", "CartService"),
    new("/health", "CartService")
};

app.MapGet("/", () => Results.Ok(new { status = "Gateway is running" }));

app.Map("/{**catchAll}", async context =>
{
    var normalizedPath = NormalizePath(context.Request.Path.Value);

    var matchingRoute = routeMappings.FirstOrDefault(route =>
        PathMatches(normalizedPath, route.Prefix));

    if (matchingRoute is null)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsJsonAsync(new { message = "Route not found" });
        return;
    }

    var httpClientFactory = context.RequestServices.GetRequiredService<IHttpClientFactory>();

    if (!whiteListedPaths.Contains(normalizedPath))
    {
        var token = ExtractBearerToken(context);

        if (string.IsNullOrWhiteSpace(token))
        {
            await WriteUnauthorizedAsync(context);
            return;
        }

        try
        {
            var isValid = await ValidateTokenAsync(token, httpClientFactory, context.RequestAborted);

            if (!isValid)
            {
                await WriteUnauthorizedAsync(context);
                return;
            }
        }
        catch (HttpRequestException)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new { message = "Authentication service is unavailable" });
            return;
        }
    }

    var targetClient = httpClientFactory.CreateClient(matchingRoute.ServiceKey);

    try
    {
        await ForwardRequestAsync(context, targetClient);
    }
    catch (HttpRequestException)
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new { message = $"{matchingRoute.ServiceKey} is unavailable" });
    }
});

app.Run();

static async Task ForwardRequestAsync(HttpContext context, HttpClient httpClient)
{
    var targetUri = BuildTargetUri(context);
    using var downstreamRequest = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUri);

    var content = await CreateHttpContentAsync(context);
    if (content is not null)
    {
        downstreamRequest.Content = content;
    }

    CopyRequestHeaders(context, downstreamRequest);

    using var downstreamResponse = await httpClient.SendAsync(
        downstreamRequest,
        HttpCompletionOption.ResponseHeadersRead,
        context.RequestAborted);

    context.Response.StatusCode = (int)downstreamResponse.StatusCode;

    foreach (var header in downstreamResponse.Headers)
    {
        context.Response.Headers[header.Key] = header.Value.ToArray();
    }

    foreach (var header in downstreamResponse.Content.Headers)
    {
        context.Response.Headers[header.Key] = header.Value.ToArray();
    }

    context.Response.Headers.Remove("transfer-encoding");
    context.Response.Headers.Remove("Transfer-Encoding");
    context.Response.Headers.Remove("Connection");

    await downstreamResponse.Content.CopyToAsync(context.Response.Body);
}

static async Task<HttpContent?> CreateHttpContentAsync(HttpContext context)
{
    if (!HttpMethods.IsPost(context.Request.Method) &&
        !HttpMethods.IsPut(context.Request.Method) &&
        !HttpMethods.IsPatch(context.Request.Method) &&
        !HttpMethods.IsDelete(context.Request.Method))
    {
        return null;
    }

    context.Request.EnableBuffering();

    var memoryStream = new MemoryStream();
    await context.Request.Body.CopyToAsync(memoryStream);
    memoryStream.Position = 0;

    if (context.Request.Body.CanSeek)
    {
        context.Request.Body.Position = 0;
    }

    var content = new StreamContent(memoryStream);
    return content;
}

static void CopyRequestHeaders(HttpContext context, HttpRequestMessage downstreamRequest)
{
    foreach (var header in context.Request.Headers)
    {
        if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (downstreamRequest.Content is not null && header.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
        {
            downstreamRequest.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            continue;
        }

        downstreamRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
    }
}

static Uri BuildTargetUri(HttpContext context)
{
    var path = context.Request.Path.Value ?? string.Empty;
    var query = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty;
    return new Uri(string.Concat(path, query), UriKind.Relative);
}

static string NormalizePath(string? path)
{
    if (string.IsNullOrWhiteSpace(path))
    {
        return string.Empty;
    }

    return path.Length > 1 && path.EndsWith('/')
        ? path.TrimEnd('/')
        : path;
}

static bool PathMatches(string requestPath, string prefix)
{
    if (string.IsNullOrEmpty(requestPath) || string.IsNullOrEmpty(prefix))
    {
        return false;
    }

    if (requestPath.Equals(prefix, StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if (!requestPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    return requestPath.Length > prefix.Length && requestPath[prefix.Length] == '/';
}

static string? ExtractBearerToken(HttpContext context)
{
    if (!context.Request.Headers.TryGetValue("Authorization", out var authorization))
    {
        return null;
    }

    var headerValue = authorization.ToString();
    const string bearerPrefix = "Bearer ";

    if (!headerValue.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
    {
        return null;
    }

    var token = headerValue[bearerPrefix.Length..].Trim();
    return string.IsNullOrWhiteSpace(token) ? null : token;
}

static Task WriteUnauthorizedAsync(HttpContext context)
{
    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    return context.Response.WriteAsJsonAsync(new { message = "User is not authorized" });
}

static async Task<bool> ValidateTokenAsync(string token, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
{
    var authClient = httpClientFactory.CreateClient("AuthService");
    using var response = await authClient.PostAsJsonAsync(
        "/api/auth/validate",
        new TokenValidationRequest(token),
        cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
        return false;
    }

    var payload = await response.Content.ReadFromJsonAsync<TokenValidationResponse>(cancellationToken: cancellationToken);
    return payload?.Valid ?? false;
}

file sealed record RouteMapping(string Prefix, string ServiceKey);

file sealed record TokenValidationRequest(string Token);

file sealed record TokenValidationResponse
{
    [JsonPropertyName("valid")]
    public bool Valid { get; init; }
}
