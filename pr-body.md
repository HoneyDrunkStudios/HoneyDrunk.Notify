## Summary

- align Notify Worker/Functions host identity with Kernel canonical `WellKnownNodes.Ops.Notify` fallback while preserving deploy-time `HONEYDRUNK_NODE_ID` / `Grid:NodeId` overrides
- move Azure Storage Queue runtime credential resolution behind Vault-backed `ISecretStore`, keeping direct connection strings for local tooling and documenting Functions binding settings as deployment-provided
- consolidate duplicated template file loading/cache logic, provider secret lookup, and provider/queue DI registration helpers
- align package versions to `0.3.0`, Kernel dependencies to `0.7.0`, and Vault dependencies to `0.5.0`

Closes #13
Closes #8

## Validation

- `dotnet build HoneyDrunk.Notify\HoneyDrunk.Notify.slnx -c Release --no-restore`
- `dotnet test HoneyDrunk.Notify\HoneyDrunk.Notify.slnx -c Release --no-build --verbosity minimal`
- `dotnet list HoneyDrunk.Notify\HoneyDrunk.Notify.slnx package --vulnerable --include-transitive`
- `git diff --check`
- lightweight secret pattern scan (gitleaks not installed locally; no matches)

## Notes

- Azure Functions queue trigger still uses `Connection = "NotifyQueueConnection"`; that is the Functions runtime binding boundary and must be populated by deployment/bootstrap, not committed config secrets.
- Intentional remaining duplication: email subject/body parsing stays renderer-specific while shared path safety, file IO, and cache behavior lives in `TemplateFileLoader`.
