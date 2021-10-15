﻿using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Datack.Agent.Data;
using Datack.Agent.Models;
using Datack.Agent.Services;
using Datack.Agent.Services.DataConnections;
using Datack.Agent.Services.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Exceptions;

namespace Datack.Agent
{
    public static class Program
    {
        public static LoggingLevelSwitch LoggingLevelSwitch { get; set; }

        public static async Task Start(String[] args)
        {
            try
            {
                var builder = CreateHostBuilder(args);
                
                var version = Assembly.GetEntryAssembly()?.GetName().Version;
                Log.Warning($"Starting host on version {version}");

                await builder.RunConsoleAsync();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static IHostBuilder CreateHostBuilder(String[] args)
        {
            var configuration = new ConfigurationBuilder()
#if DEBUG
                                .AddJsonFile("appsettings.Development.json", true, false)
#else
                                .AddJsonFile("appsettings.json", true, false)
#endif
                                .Build();

            var appSettings = new AppSettings();
            configuration.Bind(appSettings);

            if (!String.IsNullOrWhiteSpace(appSettings.Logging.LogLevel.Default))
            {
                LoggingLevelSwitch = new LoggingLevelSwitch(Enum.Parse<LogEventLevel>(appSettings.Logging.LogLevel.Default));
            }

            Log.Logger = new LoggerConfiguration()
                         .Enrich.FromLogContext()
                         .Enrich.WithExceptionDetails()
                         .WriteTo.File(appSettings.Logging.File.Path, 
                                       rollOnFileSizeLimit: true, 
                                       fileSizeLimitBytes: appSettings.Logging.File.FileSizeLimitBytes, 
                                       retainedFileCountLimit: appSettings.Logging.File.MaxRollingFiles)
                         .WriteTo.Console()
                         .MinimumLevel.ControlledBy(LoggingLevelSwitch)
                         .CreateLogger();

            Serilog.Debugging.SelfLog.Enable(msg =>
            {
                Debug.Print(msg);
                Debugger.Break();
                Console.WriteLine(msg);
                Debug.WriteLine(msg);
            });

            return Host.CreateDefaultBuilder(args)
                       .ConfigureAppConfiguration((_, config) =>
                       {
                           config.SetBasePath(Directory.GetCurrentDirectory());
                           config.AddJsonFile("appsettings.json", true);

                           if (args != null)
                           {
                               config.AddCommandLine(args);
                           }
                       })
                       .ConfigureLogging((_, logging) =>
                       {
                           logging.AddSerilog();
                       })
                       .ConfigureServices((_, services) =>
                       {
                           var connectionString = $"Data Source={appSettings.Database.Path}";
                           
                           services.AddSingleton(appSettings);
                           services.AddSingleton(connectionString);

                           services.AddSingleton<DatabaseAdapter>();
                           services.AddSingleton<DataContextFactory>();
                           services.AddSingleton<Jobs>();
                           services.AddSingleton<JobRuns>();
                           services.AddSingleton<JobScheduler>();
                           services.AddSingleton<RpcService>();
                           services.AddSingleton<Servers>();
                           services.AddSingleton<JobTasks>();
                           services.AddSingleton<JobRunTasks>();
                           services.AddSingleton<JobRunTaskLogs>();
                           services.AddSingleton<SqlServerConnection>();

                           services.AddSingleton<CreateBackupTask>();
                           services.AddSingleton<CompressTask>();

                           services.AddHostedService<StartupHostedService>();
                           services.AddHostedService<AgentHostedService>();
                       })
                       .UseSerilog();
        }
    }
}