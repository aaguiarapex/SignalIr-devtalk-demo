using DemoApp.Hubs;
using Microsoft.Azure.SignalR;

var builder = WebApplication.CreateBuilder(args);

var signalRConnection = builder.Configuration["Azure:SignalR:ConnectionString"];
var useAzureSignalR = !builder.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(signalRConnection);

var signalRBuilder = builder.Services.AddSignalR();

if (useAzureSignalR)
{
    signalRBuilder.AddAzureSignalR(signalRConnection);
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .SetIsOriginAllowed(_ => true)
            .AllowCredentials();
    });
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors();

app.MapHub<ChatHub>("/chat");

app.MapFallbackToFile("index.html");

app.Run();
