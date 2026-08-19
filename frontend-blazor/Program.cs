using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ParaGateway.Frontend;
using ParaGateway.Frontend.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddDevExpressBlazor();

builder.Services.AddScoped<BrowserCredentialsHandler>();
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<BrowserCredentialsHandler>();
    handler.InnerHandler = new HttpClientHandler();
    var configuredApi = builder.Configuration["ApiBaseUrl"];
    var apiBase = string.IsNullOrWhiteSpace(configuredApi)
        ? new Uri(builder.HostEnvironment.BaseAddress)
        : new Uri(configuredApi, UriKind.Absolute);
    return new HttpClient(handler) { BaseAddress = apiBase };
});
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<AuthSession>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<AnnouncementService>();

await builder.Build().RunAsync();
