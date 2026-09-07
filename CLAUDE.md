# ScHauler

Personal Blazor Server (.NET 10) app tracking chained Star Citizen hauling contracts.
Runs containerized (rootful Podman) on a home server, used from a tablet.

## Layout

- src/ScHauler.csproj — app; tests/ScHauler.Tests.csproj — NUnit 4 tests
- Directory.Build.props + .editorconfig at root: TreatWarningsAsErrors, AnalysisLevel latest-all. Never suppress rules wholesale; per-rule in .editorconfig with a comment.

## Conventions (deliberate, don't "simplify")

- Overengineered DDD domain as a learning exercise: typed ids (ContractId etc., Guid.CreateVersion7), Scu value object, sealed entities, private ctors + factory methods with guard clauses, state transitions as entity behavior (Advance/Correct).
- Domain is persistence-ignorant: EF converters/configurations live in Data/, not Models/.
- Pure logic (ManifestPlanner) stays EF-free and unit-tested; ManifestService is a thin IDbContextFactory adapter.
- NUnit constraint model (Assert.That), NUnit.Analyzers on.
- UI: pickup/deliver = amber/cyan + text verbs (protanopia-safe, never color alone), big tap targets.
