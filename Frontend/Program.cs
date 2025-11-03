using Frontend.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Razor Pages
builder.Services.AddRazorPages();

// Add HttpContextAccessor for cookie access
builder.Services.AddHttpContextAccessor();

// Register HTTP clients for microservices
builder.Services.AddHttpClient<AuthService>();
builder.Services.AddHttpClient<CatalogService>();
builder.Services.AddHttpClient<CartService>();

// Add session support, needed for cart management
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

// Middleware pipeline
// Configure authentication and authorization
app.UseStaticFiles();
// Enable routing, session, and authorization
app.UseRouting();
app.UseSession();
app.UseAuthorization();
// Map Razor Pages
app.MapRazorPages();

app.Run();
