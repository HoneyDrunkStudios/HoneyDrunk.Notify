# HoneyDrunk.Notify.Providers.Email.Resend

Resend HTTP API email provider for the HoneyDrunk.Notify notification subsystem.

## Usage

```csharp
services
    .AddHoneyDrunkNotifyRuntime()
    .AddHoneyDrunkNotifyResendProvider(options =>
    {
        options.FromAddress = "noreply@example.com";
        options.FromDisplayName = "My App";
    });
```

The Resend API key is resolved through `ISecretStore` at send time using the `Resend--ApiKey` secret name.

## When to use Resend vs SMTP

| Concern | SMTP | Resend |
|---------|------|--------|
| Deliverability | Depends on your SMTP server | High (dedicated infrastructure) |
| Setup | Requires SMTP server / relay | API key only |
| Local dev | MailHog / Papercut | API key + sandbox domain |
| Cost | Free (self-hosted) | Free tier available, paid plans |
| Features | Basic send | Analytics, webhooks, templates |

Use **SMTP** for local development and self-hosted environments.
Use **Resend** for production workloads requiring high deliverability and delivery analytics.
