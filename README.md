# HoneyDrunk.Notify

HoneyDrunk.Notify owns notification delivery mechanics for the Grid: request intake, structural validation, rendering, provider dispatch, retry, queueing, and delivery tracking.

Outbound-message decision policy lives in `HoneyDrunk.Communications`. Notify should not own preferences, cadence, suppression, or communication orchestration decisions; callers invoke Notify after those decisions are made.