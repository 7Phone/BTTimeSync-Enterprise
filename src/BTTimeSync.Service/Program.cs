using BTTimeSync.Application;
using BTTimeSync.Bluetooth;
using BTTimeSync.Core;
using BTTimeSync.Service;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddCore()
    .AddBluetooth()
    .AddApplication(builder.Configuration);

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

host.Run();