using Quartz;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using SoftwareAsAServiceBot.Caching;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

builder.Services.AddQuartz(q =>
{
    q.UseMicrosoftDependencyInjectionJobFactory();
    q.UseMicrosoftDependencyInjectionJobFactory();
    var jobKey = JobKey.Create(nameof(FetchMatchesJob));
    q.AddJob<FetchMatchesJob>(jobKey)
        .AddTrigger(trigger =>
        {
            trigger.ForJob(jobKey)
            .WithSimpleSchedule(schedule =>
            {
                schedule.WithIntervalInSeconds(5).RepeatForever(); ;
            }

            );
        });
});
builder.Services.AddQuartzHostedService(option=>
{
    option.WaitForJobsToComplete = true;
});
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var connectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING") ?? "localhost:6379";
    return ConnectionMultiplexer.Connect(connectionString);
});

// Optionally configure a distributed cache
builder.Services.AddScoped<ICacheService, CacheService>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
