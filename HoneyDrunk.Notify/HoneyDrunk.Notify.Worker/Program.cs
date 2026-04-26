using HoneyDrunk.Kernel.Abstractions;
using HoneyDrunk.Kernel.Abstractions.Identity;
using HoneyDrunk.Kernel.Hosting;
using HoneyDrunk.Notify.Hosting.AspNetCore.Options;
using HoneyDrunk.Notify.Providers.Email.Resend;
using HoneyDrunk.Notify.Providers.Email.Smtp;
using HoneyDrunk.Notify.Providers.Sms.Twilio;
using HoneyDrunk.Notify.Queue.AzureStorage;
using HoneyDrunk.Notify.Worker.Composition;
using HoneyDrunk.Notify.Worker.Options;
using HoneyDrunk.Vault.EventGrid.Extensions;
using HoneyDrunk.Vault.Providers.AppConfiguration.Extensions;
using HoneyDrunk.Vault.Providers.AzureKeyVault.Extensions;
using Microsoft.AspNetCore.Builder;
using GridEnvironments = HoneyDrunk.Kernel.Abstractions.Environments;

const string NotifyNodeId = "honeydrunk-notify";

var builder = WebApplication.CreateBuilder(args);
builder.Configuration["HONEYDRUNK_NODE_ID"] = NotifyNodeId;

builder.Services
    .AddHoneyDrunkNode(options =>
    {
        options.NodeId = new NodeId(NotifyNodeId);
        options.SectorId = Sectors.Ops;
        options.EnvironmentId = ResolveEnvironment(builder.Configuration);
        options.Version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.1.0";
        options.StudioId = builder.Configuration["Grid:StudioId"] ?? "honeydrunk";
        options.Tags["service"] = "notify";
        options.Tags["adr"] = "ADR-0005,ADR-0006";
    })
    .AddVaultWithAzureKeyVaultBootstrap()
    .AddAppConfiguration();

builder.Services.AddVaultEventGridInvalidation();

builder.Services.Configure<NotifyOptions>(builder.Configuration.GetSection("Notify"));
builder.Services.Configure<ResendOptions>(builder.Configuration.GetSection("Resend"));
builder.Services.Configure<TwilioOptions>(builder.Configuration.GetSection("Twilio"));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<AzureStorageQueueOptions>(options =>
{
    options.ConnectionString = builder.Configuration["NotifyQueueConnection"] ?? string.Empty;
});

builder.Services.AddHoneyDrunkNotifyWorker(ConfigureWorkerOptions);

var app = builder.Build();
app.MapVaultInvalidationWebhook("/internal/vault/invalidate");
app.Run();

void ConfigureWorkerOptions(NotifyWorkerOptions options)
{
    builder.Configuration.GetSection("NotifyWorker").Bind(options);
}

static EnvironmentId ResolveEnvironment(IConfiguration configuration)
{
    var configured =
        configuration["Grid:Environment"]
        ?? configuration["DOTNET_ENVIRONMENT"]
        ?? configuration["ASPNETCORE_ENVIRONMENT"];

    return string.IsNullOrWhiteSpace(configured)
        ? GridEnvironments.Development
        : new EnvironmentId(configured.ToLowerInvariant());
}
