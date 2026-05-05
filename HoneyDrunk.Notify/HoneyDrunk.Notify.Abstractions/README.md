# HoneyDrunk.Notify.Abstractions

Pure contracts and abstractions for the HoneyDrunk.Notify notification subsystem.

This package contains channel-agnostic notification interfaces, message templates, delivery primitives, and context contracts. It is safe for downstream Nodes and libraries that need Notify contracts without runtime provider dependencies.

Outbound-message decision policy contracts are intentionally excluded. Preferences, cadence, suppression, and communication orchestration belong to `HoneyDrunk.Communications`; Notify accepts already-approved delivery requests and owns structural validation plus delivery mechanics.
