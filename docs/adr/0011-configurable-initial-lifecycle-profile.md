# ADR-0011: Configurable Initial Part-CAD Lifecycle Profile

## Status

Accepted as the IDEA target policy; live Aras configuration remains unapproved evidence.

## Context

IDEA wants the first PDM workflow to be simple and familiar: Part and CAD should use the same business roles for initial design, detailed design, review, release, and later change. The inspected live Aras environment assigns different custom lifecycle maps to Part and CAD, and both active maps contain the core state names `Khoi tao`, `Thiet ke chi tiet`, `In Review`, and `Released`, while retaining additional ItemType-specific states.

Treating the live map as the product definition would make the product difficult to change. Treating both ItemTypes as one raw state enum would make backend replacement and future lifecycle configuration unsafe.

## Decision

Define one initial IDEA lifecycle **profile** at the semantic-role level:

`Initial design` → `Detailed design` → `In review` → `Released`, with `In change` and `Superseded` for later revision work.

Part and CAD adapters map that profile independently to their authority-specific ItemType and lifecycle-map states. The mapping is configuration/policy data, not a shared raw-state enum. The current live `Custom Part` map contains the core profile but is still recorded as environment evidence, not silently adopted as the complete IDEA product model.

## Consequences

- The initial UI and policy can stay simple while the backend and Aras lifecycle maps remain replaceable.
- An Aras administrator may either configure the live Part map to match the target profile or provide an explicit mapping from the existing Part states to the semantic roles.
- No Part/CAD action is enabled merely because two display names happen to match.
- Evidence gates must verify the selected mapping and transitions before runtime actions are enabled.
