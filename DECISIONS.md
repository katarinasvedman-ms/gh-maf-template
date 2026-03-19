# Decisions

1. Template architecture is two-layer: guidance-first plus product reference implementation.
2. Guidance standard is repo-wide plus path-specific instructions; custom-agent profiles are not part of the supported model.
3. Runtime target is local-first with .NET 10.0 baseline.
4. Tool execution is policy-first with explicit allow-listing.
5. High-sensitivity tool calls require approval by default.
6. MCP tools are normalized into common tool contracts.
7. Observability uses OpenTelemetry-compatible primitives without vendor lock-in.
8. Evaluation is built into the template as a first-class concern.
