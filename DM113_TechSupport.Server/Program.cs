using Microsoft.AspNetCore.Server.Kestrel.Core;
using DM113_TechSupport.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// gRPC exige HTTP/2
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5000, o => o.Protocols = HttpProtocols.Http2);
});

builder.Services.AddGrpc();

var app = builder.Build();
app.MapGrpcService<SupportService>();

Console.WriteLine("Servidor gRPC rodando em http://localhost:5000");
app.Run();
