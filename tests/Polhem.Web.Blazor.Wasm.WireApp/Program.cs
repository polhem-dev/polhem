using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Polhem.Web.Blazor.Wasm.DependencyInjection;
using Polhem.Web.Blazor.Wasm.WireApp;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<WireRunner>("#app");

// The client the Northwind head uses, registered the same way, against the JSON-RPC endpoint of the host that serves
// this page.
builder.Services.AddPolhemBlazor(options =>
{
    options.Endpoint = new Uri(new Uri(builder.HostEnvironment.BaseAddress), "/api").AbsoluteUri;
    options.ApiKey = Polhem.Web.Blazor.Wasm.BrowserTests.WireSample.ApiKey;
});

await builder.Build().RunAsync();
