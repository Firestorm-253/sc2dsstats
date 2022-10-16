﻿using System;
using System.Linq;
using System.Reflection;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using pax.dsstats.dbng;
using pax.dsstats.dbng.Services;

// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");

var loc = Assembly.GetExecutingAssembly().Location;

var logger = ApplicationLogging.CreateLogger<Program>();
logger.LogInformation("Running ...");

var services = new ServiceCollection();

services.AddDbContext<ReplayContext>(options =>
{
    options
        .UseLoggerFactory(ApplicationLogging.LogFactory)
        .UseSqlite(@"Data Source=Database\dsstats2.db",
        x =>
        {
            x.MigrationsAssembly("SqliteMigrations");
            x.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery);
        }
        ).EnableSensitiveDataLogging()
        .EnableDetailedErrors();
});

var serviceProvider = services.BuildServiceProvider();
var context = serviceProvider.GetService<ReplayContext>();

ArgumentNullException.ThrowIfNull(context);

context.Database.Migrate();

var config = new MapperConfiguration(cfg =>
{
    cfg.AddProfile<AutoMapperProfile>();
});
var mapper = config.CreateMapper();

var mmrService = new FireMmrService(context, mapper);

mmrService.CalcMmmr().GetAwaiter().GetResult();

var dev = await context.Players
    .GroupBy(g => Math.Round(g.DsR, 0))
    .Select(s => new
    {
        Count = s.Count(),
        AvgDsr = s.Average(a => Math.Round(a.DsR, 0)),
        Name = s.First().Name,
    }).ToListAsync();

foreach (var d in dev)
{
    Console.WriteLine($"{d.Name} ({d.Count}) => {d.AvgDsr}");
}

Console.ReadLine();


public static class ApplicationLogging
{
    public static ILoggerFactory LogFactory { get; } = LoggerFactory.Create(builder =>
    {
        builder.ClearProviders();
        // Clear Microsoft's default providers (like eventlogs and others)
        builder.AddSimpleConsole(options =>
        {
            options.IncludeScopes = true;
            options.SingleLine = true;
            options.TimestampFormat = "yyyy-MM-dd hh:mm:ss ";
        }).SetMinimumLevel(LogLevel.Information);
    });

    public static ILogger<T> CreateLogger<T>() => LogFactory.CreateLogger<T>();
}