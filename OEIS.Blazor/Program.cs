using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using OEIS.Blazor;
using OEIS.Blazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Point Blazor to the API URL
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("https://localhost:7094/")
});

// MudBlazor services
builder.Services.AddMudServices();

// Simple client-side auth state container
builder.Services.AddScoped<AuthState>();
builder.Services.AddScoped<NotificationCenterService>();

await builder.Build().RunAsync();
