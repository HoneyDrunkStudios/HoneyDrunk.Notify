# HoneyDrunk.Notify.Providers.Email.Smtp

SMTP email provider for the HoneyDrunk.Notify notification subsystem.

## Usage

```csharp
services
    .AddHoneyDrunkNotifyRuntime()
    .AddHoneyDrunkNotifySmtpProvider(options =>
    {
        options.Host = "smtp.example.com";
        options.Port = 587;
        options.FromAddress = "noreply@example.com";
        options.FromDisplayName = "My App";
    });
```

SMTP credentials are resolved through `ISecretStore` at send time using `Smtp--Username` and `Smtp--Password`.
