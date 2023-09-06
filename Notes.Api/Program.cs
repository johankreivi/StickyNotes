using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Notes.Api.AccessControl;
using Notes.Api.Configuration;
using Notes.Api.Database;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configuration for Logging below, for file logging add something like Serilog.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("Logs/mylog.txt")
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services
    .ConfigureSecrets(builder.Configuration)
    .AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.TypeNameHandling = TypeNameHandling.Auto;
        options.SerializerSettings.Converters.Add(new StringEnumConverter());
    });

builder.Services
    .AddAccessControl()
    .AddSwaggerDocumentation()
    .AddNotesDatabase();

var application = builder.Build();

application
    .UseDeveloperExceptionPage()
    .UseNotesClientServer(application.Environment)
    .UseSwaggerDocumentation()
    .UseRouting()
    .UseAccessControl()
    .UseCorrelationId()
    .UseEndpoints(endpoints => endpoints.MapControllers());

application
    .SeedNotesDatabase()
    .Run();
