using HoneyDrunk.Kernel.Abstractions;
using HoneyDrunk.Kernel.Abstractions.Identity;
using HoneyDrunk.Kernel.Hosting;
using HoneyDrunk.Notify.Hosting.AspNetCore.Options;
using HoneyDrunk.Notify.Hosting.AspNetCore.ServiceCollectionExtensions;
using HoneyDrunk.Notify.Providers.Email.Resend;
using HoneyDrunk.Notify.Providers.Email.Resend.DependencyInjection;
using HoneyDrunk.Notify.Providers.Email.Smtp;
using HoneyDrunk.Notify.Providers.Email.Smtp.DependencyInjection;
using HoneyDrunk.Notify.Providers.Sms.Twilio;
using HoneyDrunk.Notify.Providers.Sms.Twilio.DependencyInjection;
using HoneyDrunk.Vault.EventGrid.Extensions;
using HoneyDrunk.Vault.Providers.AppConfiguration.Extensions;
using HoneyDrunk.Vault.Providers.AzureKeyVault.Extensions;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using GridEnvironments = HoneyDrunk.Kernel.Abstractions.Environments;

const string NotifyNodeId = "honeydrunk-notify";

var builder = FunctionsApplication.CreateBuilder(args);
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

builder.Services
    .AddHoneyDrunkNotify()
    .AddHoneyDrunkNotifySmtpProvider()
    .AddHoneyDrunkNotifyResendProvider(_ => { })
    .AddHoneyDrunkNotifyTwilioProvider(_ => { });

builder.Build().Run();

static EnvironmentId ResolveEnvironment(IConfiguration configuration)
{
    var configured =
        configuration["Grid:Environment"]
        ?? configuration["DOTNET_ENVIRONMENT"]
        ?? configuration["AZURE_FUNCTIONS_ENVIRONMENT"];

    return string.IsNullOrWhiteSpace(configured)
        ? GridEnvironments.Development
        : new EnvironmentId(configured.ToLowerInvariant());
}
