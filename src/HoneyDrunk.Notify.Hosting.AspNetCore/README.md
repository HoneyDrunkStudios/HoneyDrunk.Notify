# HoneyDrunk.Notify.Hosting.AspNetCore

ASP.NET Core hosting integration for HoneyDrunk.Notify.

This package wires Notify services into ASP.NET Core dependency injection, maps configured `NotifyOptions` into runtime options, and keeps non-secret defaults aligned with host configuration.

## Usage

```csharp
services.AddHoneyDrunkNotify(options =>
{
    options.Enabled = true;
});
```

Provider credentials remain owned by provider packages and are resolved through `ISecretStore` at send time.
