# GORM public-surface treatment

## Positioning

**Promise:** Model how things connect.

**One-line product definition:** A strongly typed graph ORM for SQL Server Graph with relationship-aware queries, change tracking, in-memory execution and temporal graph history.

GORM should be introduced as a graph ORM first. Graph Intelligence, Digital Twin, GraphRAG, Helix and the ChangeSet compiler are deeper capabilities of the graph runtime, not five separate products competing for the hero section.

## Visual identity

GORM is the third independent product identity in the PjotrCasteel portfolio.

- metaphor: topology, nodes, edges, paths and layers;
- base: deep ink / violet-black;
- primary accent: ultraviolet;
- secondary accents: coral relationship signals and pale jade historical state;
- interaction: **Graph Lens** — look at one graph as traversal, historical state or impact analysis;
- creator signature: the same restrained PC / PjotrCasteel authorship treatment used elsewhere.

This deliberately does not reuse Causalia's failure-lab visual language or Forge's blueprint/fabrication language.

## Public information architecture

1. product promise;
2. why graph persistence deserves first-class relationships;
3. interactive Graph Lens;
4. core CLR/API shape;
5. advanced graph capabilities;
6. responsibility boundary;
7. install/source links only after public identities are decided.

## Important naming work before publication

The source material currently uses `KPN.IRMA.Gorm` throughout. Do not silently present a generic public package name until the actual codebase has been prepared for that identity.

Also rename the existing internal/public namespace area `Application.Forge` before public release. Forge is now a separate product family. Recommended direction:

- public capability name: **ChangeSet & transaction compiler**;
- namespace direction: `Application.ChangeSets` or `Application.Mutations`;
- studio namespace direction: `Application.ChangeSets.Studio` if still needed.

Avoid creating another sub-brand unless there is a real user-facing reason.

## Publication gate

The public GitHub repository and website are now established. NuGet publication remains explicitly deferred until the implementation source migration is complete and validated.

Before publication:

- obtain/prepare the actual GORM source tree;
- decide generic package/namespace migration versus retaining the current package identity;
- rename the GORM-internal Forge capability;
- compile/test the migration;
- align root/NuGet README with the new public front door;
- add package icon, project URL, repository metadata and release verification;
- publish NuGet only as a separate explicit release decision after the migrated source is green.
