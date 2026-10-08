# Real GORM Playground translator — first browser milestone

This is a free, static **.NET 10 WebAssembly** wrapper around GORM's own `Explain()` query pipeline.

## Supported, evidence-backed queries

- Outgoing, incoming and two-hop graph traversals
- Node filtering with deterministic ordering and `Skip/Take`

The WebAssembly `[JSExport]` entry point delegates to `Gorm.Playground.Core.PlaygroundQueryEngine`, which constructs typed GORM LINQ queries and invokes the real `Explain()` implementation. `Gorm.Playground.Verify` runs the *same* code natively to assert that its SQL comes from GORM.

**Not supported as authoritative yet:** arbitrary edited C#, custom schemas, the temporal `AsOf` preset, and `Include` (the current GORM Explain() path throws for Include). These still render via the explicitly labelled JavaScript documentation preview.

Queries are strictly matched against the four known canonical source examples. They use fixed demo values to build safe expression trees; browser inputs are not compiled or executed as arbitrary C#. No SQL Server, paid backend, credential or remote evaluation is involved.

## Build and verification

```bash
dotnet run --project samples/Gorm.Playground.Verify/Gorm.Playground.Verify.csproj -c Release
dotnet workload install wasm-tools
dotnet publish samples/Gorm.Playground.Browser/Gorm.Playground.Browser.csproj -c Release
```

The publish output is at `samples/Gorm.Playground.Browser/bin/Release/net10.0/publish/`. The GitHub Pages workflow copies that output into the site under `playground-wasm/`; it does not check generated binaries into the repository.

**Trimming is deliberately disabled.** GORM's current expression-tree and reflection code is not guaranteed to survive linker trimming. Measure the browser download budget and verify execution before attempting to enable trimming or AOT.

## Follow-up acceptance tests

- Launch the production build in desktop/mobile browsers and exercise the `ExplainPreset` exported method.
- Compare actual WASM SQL to native `Explain()` results, not to the older illustrative JavaScript SQL.
- Report the final first-load compressed size and startup time.
- Expand from fixed examples into safe, validated query intent (schema-bound filters, order and paging), with no silent fallback to pseudo-authoritative SQL.
