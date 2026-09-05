using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Infrastructure;
using OnlineJudge.JudgeWorker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ICurrentUser, BackgroundCurrentUser>();
builder.Services.AddScoped<JudgeJobProcessor>();
builder.Services.AddSingleton(JudgeWorkerOptions.FromConfiguration(builder.Configuration));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
