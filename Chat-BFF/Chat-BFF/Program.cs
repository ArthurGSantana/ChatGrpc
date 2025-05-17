using Chat.Service.Protos;
using Chat_BFF.Grpc.Client;
using Chat_BFF.Grpc.Interfaces;
using Chat_BFF.Interfaces.Service;
using Chat_BFF.Interfaces.WebSocketConfig;
using Chat_BFF.Service;
using Chat_BFF.WebSocketConfig;
using Microsoft.AspNetCore.WebSockets;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddControllers();

builder.Services.AddSingleton<IWebSocketConnectionManager, WebSocketConnectionManager>();
builder.Services.AddSingleton<IChatMessagingService, ChatMessagingService>();

builder.Services.AddWebSockets(options =>
{
    options.KeepAliveInterval = TimeSpan.FromMinutes(2);
});

var grpcFreightUrl = builder.Configuration.GetSection("GrpcServices:ChatService").Value ?? "";

if (!string.IsNullOrEmpty(grpcFreightUrl))
{
    builder.Services.AddSingleton<IChatClientService>(provider =>
    {
        var logger = provider.GetRequiredService<ILogger<ChatClientService>>();
        return new ChatClientService(logger, grpcFreightUrl);
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseWebSockets();

app.UseWhen(context => context.Request.Path.StartsWithSegments("/ws"), 
    appBuilder => appBuilder.UseMiddleware<WebSocketMiddleware>());

app.MapControllers();

app.Run();