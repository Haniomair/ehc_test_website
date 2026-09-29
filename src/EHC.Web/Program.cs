
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

#if DEBUG
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
#endif
// Server-only settings (connection string, secrets): placed on the server by hand, never published or committed
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.local.json", optional: true, reloadOnChange: true);

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

WebApplication app = builder.Build();


await app.BootUmbracoAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}


app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();
