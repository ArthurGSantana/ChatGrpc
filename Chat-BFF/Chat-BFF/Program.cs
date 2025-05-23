using Chat_BFF.Grpc.Client;
using Chat_BFF.Grpc.Interfaces;
using Chat_BFF.Interfaces.Service;
using Chat_BFF.Service;
using Chat_BFF.SignalR;
using Microsoft.AspNetCore.WebSockets;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddControllers();

builder.Services.AddSingleton<IChatMessagingService, ChatMessagingService>();

builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder
            .WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowCredentials());
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

app.UseCors("AllowAll");

app.UseHttpsRedirection();

app.MapHub<ChatHub>("/hubs/chat");

app.UseWhen(context => context.Request.Path.StartsWithSegments("/ws"),
    appBuilder => appBuilder.UseMiddleware<WebSocketMiddleware>());

app.MapControllers();

app.Run();