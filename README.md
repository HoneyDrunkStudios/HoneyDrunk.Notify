# HoneyDrunk.Notify

## Repository layout

Open `HoneyDrunk.Notify.slnx` from the repository root. Production projects live in `src/`, test and canary projects in `tests/`, and examples in `samples/` where present. Shared build configuration stays at the root; project names, namespaces and package identities are unchanged. Existing documentation and release-history locations are retained.


HoneyDrunk.Notify owns notification delivery mechanics for the Grid: request intake, structural validation, rendering, provider dispatch, retry, queueing, and delivery tracking.

Outbound-message decision policy lives in `HoneyDrunk.Communications`. Notify should not own preferences, cadence, suppression, or communication orchestration decisions; callers invoke Notify after those decisions are made.