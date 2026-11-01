using Blazor.Mud.DemoCenter.Components;
using Blazor.Mud.DemoCenter.Modules.Lookup;
using Polhem.Api.Contracts.Form;
using Polhem.UI.Core.Definitions;
using Polhem.Web.Blazor.Mud.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPolhemMud();

// Only for the lookup case: a lookup dialog asks these two services before it falls back to an API client.
builder.Services.AddScoped<DemoLookupSource>();
builder.Services.AddScoped<IFormDefinitionSource>(sp => sp.GetRequiredService<DemoLookupSource>());
builder.Services.AddScoped<IFormApiConnectorFactory>(sp => sp.GetRequiredService<DemoLookupSource>());

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
