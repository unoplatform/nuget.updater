# Property → Package Fallback Mappings

## Status
Proposed — implemented on branch `dev/agzi/property-package-fallback-mappings`.

## Summary
Add a built-in, in-process fallback table that lets the NuGet Updater resolve the latest version of an MSBuild *version property* whose name-derived package ID points at an outdated or deprecated base package on NuGet.org. The fallback is consulted *after* the normal feed lookup and only "wins" when it yields a newer version than the heuristic-derived package. Behavior is purely additive — no public API changes, no breaking changes for existing consumers.

## Motivation

### Observed problem
Multiple `unoplatform` canary build pipelines have been silently failing to bump well-known MSBuild version properties for months. Example from Uno Studio canary build [`213977`](https://dev.azure.com/uno-platform/uno-private/_build/results?buildId=213977):

```
Skipping uno.material:    version 7.0.0-dev.28 found in Directory.Build.props,
                          version 5.8.0-dev.2  found in https://api.nuget.org/v3/index.json
Skipping uno.extensions:  version 7.0.17       found in Directory.Build.props,
                          version 3.0.0-dev.1957 found in https://api.nuget.org/v3/index.json
```

The updater's discovery heuristic (`src/NvGet/Extensions/XmlDocumentExtensions.cs`) turns the MSBuild property name into a lowercase, dot-separated package ID:

```
<UnoMaterialVersion>   → uno.material
<UnoExtensionsVersion> → uno.extensions
```

These ID derivations are correct for the *legacy UWP-era* packages. But the projects actually reference modern WinUI/SubPackage variants (`Uno.Material.WinUI`, `Uno.Extensions.Core`, etc.) — packages whose version trajectory diverged from the legacy umbrella long ago:

| MSBuild property | Heuristic-derived ID | Latest dev on NuGet.org | Actually referenced package(s) | Latest dev |
|---|---|---|---|---|
| `UnoMaterialVersion` | `uno.material` | `5.8.0-dev.2` (stale UWP) | `Uno.Material.WinUI` | `7.0.0-dev.33+` |
| `UnoThemesVersion` | `uno.themes` | `5.8.0-dev.2` (stale UWP) | `Uno.Themes.WinUI`, `Uno.Simple.WinUI` (lockstep) | `7.0.0-dev.33+` |
| `UnoExtensionsVersion` | `uno.extensions` | `3.0.0-dev.1957` (stale umbrella) | `Uno.Extensions.Core`, `Uno.Extensions.Hosting.WinUI`, … | `7.3.0-dev.78+` |

Because the heuristic-derived ID returns a *lower* version than what's already in `Directory.Build.props`, the updater treats it as a non-upgrade and silently skips. The MSBuild property never advances, and the project drifts further behind every canary cycle until some other Uno canary package raises a transitive floor and a build breaks with `NU1605`.

### Production evidence

| Repo | Property | Failure mode |
|---|---|---|
| Uno Studio canary | `UnoMaterialVersion` | NU1605 — `Uno.Toolkit.WinUI.Material 9.0.0-dev.4` requires `Uno.Material.WinUI ≥ 7.0.0-dev.29` but `Directory.Build.props` was stuck at `7.0.0-dev.28` for 10+ nights |
| Uno Studio canary | `UnoExtensionsVersion` | Latent — stuck at `7.0.17` (stable) while the family ships `7.3.0-dev.78+`. No failure yet because no transitive consumer has raised the floor — but it will. |
| Uno.Toolkit canary | `UnoThemesVersion` | Latent — stuck at `7.0.0-dev.29` while `Uno.Themes.WinUI` ships `7.0.0-dev.33+`. Updater queries legacy `uno.themes` (stale at `5.8.0-dev.2`) and skips. Same risk profile as Material: NU1605 surfaces the moment Toolkit canary raises the floor. |
| uno.extensions canary | `MauiUnoVersion`, `NonMauiUnoVersion` | Repo author noticed the issue and configured a `propertiesToUpdate.json` mapping file — but the ADO task input they used (`projectProperties:`) doesn't exist; their workaround has been silently no-op since 2025-08. |

### Why a built-in?

The existing `--updateProperties=` CLI argument (and the equivalent `useUpdateProperties: true` + `updatePropertiesFile:` ADO task inputs) already lets a repo declare explicit `(PropertyName, PackageId)` mappings. That mechanism is the correct "first-class" override path for repo-specific cases.

But:

1. **Most canary repos haven't configured it.** The mismatch is repository-spanning; expecting every repo owner to discover and apply the same workaround is unrealistic — uno.extensions is the cautionary tale.
2. **The known mismatches are stable.** `uno.material` → `Uno.Material.WinUI` and `uno.extensions` → `Uno.Extensions.Core` are organizational facts, not project-specific ones.
3. **Industry precedent supports built-in defaults.** Dependabot ships ecosystem-specific defaults; Renovate ships shared presets; arcade-services ships `eng/common/` defaults. The Uno tool was the outlier with a heuristic-only path.

A small, hardcoded well-known table catches the common cases automatically — repos that want different behavior can still override with `--updateProperties=`.

## Industry alignment

| Tool | Approach to property/variable substitution | Built-in defaults |
|---|---|---|
| [Dependabot for NuGet](https://devblogs.microsoft.com/dotnet/the-new-dependabot-nuget-updater/) (2024+) | Real MSBuild evaluation | Yes — handles `$(...)` natively |
| [Renovate `nuget` manager](https://docs.renovatebot.com/modules/manager/nuget/) | Literal version strings only; substitution is unsupported ([open since 2018](https://github.com/renovatebot/renovate/issues/2266)) | Shared presets via `extends:` |
| [Maestro / arcade-services](https://github.com/dotnet/arcade/blob/main/Documentation/DependencyDescriptionFormat.md) | Explicit `Version.Details.xml` declarations | Centralized `eng/common/` |
| [NuKeeper](https://github.com/NuKeeperDotNet/NuKeeper) (archived 2022) | Heuristic name-derivation, no fallback | None — the [unresolved issue](https://github.com/NuKeeperDotNet/NuKeeper/issues/478) that contributed to abandonment |

Consistent design principle: **built-in defaults + explicit user override**. The proposed change moves Uno's tool from the NuKeeper-style heuristic-only design (which has a known failure trajectory) toward the Dependabot/Renovate pattern.

## Design

### Two existing mechanisms (unchanged)

1. **`<PackageReference Include="X" Version="literal" />`** — direct reference discovery, exact lookup.
2. **`--updateProperties=<file>.json` / `useUpdateProperties: true` + `updatePropertiesFile:`** — user-supplied `(PropertyName, PackageId)` pairs that *add* explicit references. This is the first-class user override.

### One new mechanism

3. **Built-in well-known property-fragment → fallback-package table.** Lives in `PropertyPackageMappings.WellKnownMappings`. Consulted in `UpdaterParametersExtension.GetLatestVersion` *after* the standard lookup; the fallback version is preferred only when it is *newer* than the standard result.

### Resolution algorithm

```
GetLatestVersion(reference):
  if VersionOverrides has reference.Id with forceVersion:
    return that override.MinVersion  // (existing)

  result := ResolveLatestVersion(reference)  // existing per-feed lookup

  // Suspicion gate: only consider the fallback when the standard lookup looks broken
  // (returned nothing, or returned a version OLDER than the reference's local version —
  // the canary updater wouldn't have written a higher local version than the latest
  // published unless the local file is tracking a different package via property indirection).
  standardIsSuspicious := (result == null) OR (reference.Version > result.Version)

  if standardIsSuspicious:
    propertyFragment := ConvertPackageIdToPropertyFragment(reference.Id)
    if WellKnownMappings contains propertyFragment:
      (fallbackId, reason) := WellKnownMappings[propertyFragment]
      fallbackResult := ResolveLatestVersion(reference with Id = fallbackId)
      if fallbackResult is not null and (result is null or fallbackResult.Version > result.Version):
        log "Applied property fallback mapping: {reference.Id} -> {fallbackId} ({reason})"
        result := fallbackResult

  return result
```

The **suspicion gate** is what makes the fallback safe in the presence of direct (non-property) references to legacy IDs. Without it, a repo that legitimately references `<PackageVersion Include="Uno.Material" Version="5.0.13" />` (e.g. uno.toolkit.ui) would have its version silently rewritten to `Uno.Material.WinUI`'s latest (7.x), which doesn't exist for the original package and would break restore. With the gate, the fallback only fires when the local version is *ahead* of what the standard lookup returns — a signal that the local file is tracking a different package via property indirection.

Round-trip helper `ConvertPackageIdToPropertyFragment` is the inverse of the discovery heuristic in `XmlDocumentExtensions`: split by `.`, capitalize each part, concatenate. So `uno.material` → `UnoMaterial`, matching the property-fragment keying of `WellKnownMappings`.

### Initial well-known table

```csharp
WellKnownMappings = {
  ["UnoMaterial"]   → "Uno.Material.WinUI",
  ["UnoThemes"]     → "Uno.Themes.WinUI",
  ["UnoExtensions"] → "Uno.Extensions.Core",
}
```

Excluded by intent:
- `UnoToolkit` — `uno.toolkit` (parent package) still ships in lockstep with `Uno.Toolkit.WinUI*` family
- `UnoWinui` — `uno.winui` *is* the modern package (case-insensitive ID match)
- `UnoResizetizer` / `UnoTemplates` — single-package families, no divergence

When a new property/package divergence is observed in the field, it can be added with a one-test, one-line PR.

## Compatibility & risk

| Concern | Assessment |
|---|---|
| API breakage | None. No public type or method signature changed. New file adds new public type but only as a single static table. |
| Behavior change | Repos with `UnoMaterialVersion` / `UnoExtensionsVersion` properties will start auto-bumping where they previously silently skipped. This *is the fix*. |
| Major version jumps | Repos that have been silently stuck (e.g. on the legacy `Uno.Material 5.x` track) will jump to the modern `Uno.Material.WinUI 7.x` track on first run. Canary is the safe surface to discover this; existing `--ignorePackages=` flag is the opt-out. |
| Extra feed traffic | One additional `GetPackageVersions` call per canary run *per discovered property with a well-known mapping* (currently two: `UnoMaterial`, `UnoExtensions`). Negligible. |
| Unintended fallback | The fallback is consulted only when a `WellKnownMappings` entry exists for the property fragment **and** the standard lookup looks suspicious (returned nothing, or returned a version older than the local reference). Packages outside the table take zero extra cost and zero behavior change. |
| Direct (non-property) references to legacy IDs | Safe: see the suspicion gate. A repo with `<PackageVersion Include="Uno.Material" Version="5.0.13" />` (uno.toolkit.ui pattern) has local *behind* the latest legacy version, so the gate skips the fallback and the standard lookup wins. Verified by `GivenLocalBehindStandard_FallbackDoesNotFire` test. |

## Test plan

`src/NvGet.Tests/Tools/Updater/`

- `PropertyPackageMappingsTests` — table lookups, including null/empty/unknown-key safety.
- `PropertyPackageFallbackTests` — end-to-end via `UpdaterParametersExtension.GetLatestVersion`:
  - `GivenOutdatedUnoExtensionsBase_FallbackPicksUnoExtensionsCore` (Studio scenario)
  - `GivenOutdatedUnoMaterialBase_FallbackPicksUnoMaterialWinUI` (Studio scenario)
  - `GivenOutdatedUnoThemesBase_FallbackPicksUnoThemesWinUI` (Uno.Toolkit scenario)
  - `GivenBasePackageNewerThanFallback_FallbackIsNotPreferred` (forward-compat against base-package revival)
  - `GivenLocalBehindStandard_FallbackDoesNotFire` (regression guard for direct PackageReference to legacy IDs)
  - `GivenPackageWithoutMapping_NoFallbackAttempted` (no behavior change for non-mapped packages)
  - `GivenBaseReturnsNothing_FallbackStillResolves` (legacy not present on feed)

## Rollout plan

After merge to `main`:

1. Release new version of `Uno.NuGet.Updater.Tool` to NuGet.org (e.g. `1.3.0`).
2. For each canary repo, single-line bump of `nugetUpdaterVersion:` in the canary-updater YAML. Listed approximately in order of latent impact:
   - `unoplatform/uno.studio` — actively failing (Material)
   - `unoplatform/Uno.Gallery` — exposed but unbroken
   - `unoplatform/uno.extensions` — has dead `projectProperties:` line that can be removed
   - `unoplatform/Uno.Themes`, `Uno.Playground`, `uno.toolkit.ui`, `uno.chefs`, `uno.rider`, `uno.csharpmarkup`, `uno.templates`
3. Watch first nightly canary in each repo for `Applied property fallback mapping:` log lines and reduced skip count.
4. Optional cleanup: revert per-repo workarounds that this change makes redundant (e.g. uno.studio's manual `UnoMaterialVersion` bumps on `canaries/dev`).

## Audit methodology

The well-known table was populated by scanning recent canary build logs across all 22 canary build definitions in the three relevant ADO projects (`Uno Platform`, `uno-private`, `Uno Rider Addin`). For each canary, the most recent build's `Canary Update` task log was fetched and filtered for the heuristic-mismatch pattern:

```
Skipping <lowercase.id>: version X found in <props>, version Y found in https://api.nuget.org/v3/index.json
```

where `Y < X` (the heuristic-derived package's latest version is older than what's already in the props, so the bump is silently skipped). Each candidate was then cross-checked against NuGet.org to verify a divergent modern variant exists with active dev versions, and against the canary repo source to confirm the real `<PackageReference>` uses the modern variant via the property.

Three cases met the criteria as of 2026-05-22:

| Canary | Property | Stale ID | Modern variant |
|---|---|---|---|
| Uno Studio | `UnoMaterialVersion` | `uno.material` 5.8.0-dev.2 | `Uno.Material.WinUI` |
| Uno Studio | `UnoExtensionsVersion` | `uno.extensions` 3.0.0-dev.1957 | `Uno.Extensions.Core` |
| Uno.Toolkit | `UnoThemesVersion` | `uno.themes` 5.8.0-dev.2 | `Uno.Themes.WinUI` |

Cases observed in logs but **out of scope** (different bug class):

| Canary | Pattern | Why excluded |
|---|---|---|
| Uno.CSharpMarkup | `Uno.UI` / `Uno.WinUI.Skia.Gtk` skip (direct `<PackageReference>`, not property-derived) | Both `Uno.UI` and `Uno.WinUI` are *actively maintained on parallel major lines* (5.x and 6.x). Not a "deprecated base" situation — the project's reference choice between them is intentional. Auto-redirecting would be an opinionated upgrade, not a bug fix. |
| NuGetPackageExplorer | `Uno.Monaco.Editor 6.4.0-dev.41.gfc...` vs nuget.org `2.0.0-dev.60` | Private Azure Artifacts feed publishes a different version trajectory than nuget.org. Feed-priority/visibility issue, not heuristic mismatch. |
| Various | `*.Bootstrap` skips with `0.0.0.0` | Intentional pins via `useVersionOverrides=` — opt-out by repo design. |
| Various | `Skipping [X]: no dev version found` | Non-Uno packages correctly filtered by `packageAuthor=` — by design. |

If new property→package mismatches are observed in future canary cycles, they can be added to `WellKnownMappings` with a single dict entry + one test method.

## Future work (out of scope here)

- **uno.csharpmarkup `Uno.UI` / `Uno.WinUI.Skia.Gtk` skips.** Different bug class — author/feed metadata mismatch, not property-name heuristic. Needs separate investigation.
- **uno.extensions `projectProperties:` cleanup.** Their YAML references a non-existent task input; once this fix lands and the well-known table covers the cases they intended to map, that line can be removed.
- **User-supplied fallback overrides.** Not shipped here. If a future canary repo needs a property-fragment → package mapping that isn't in the well-known table and *isn't* expressible via the existing `--updateProperties=` mechanism, a follow-up could add a new `--fallbackMappings=` CLI/task input. No demand observed today.

## References

- ADO build [213977 — Uno Studio Canary](https://dev.azure.com/uno-platform/uno-private/_build/results?buildId=213977)
- ADO build [213950 — Uno.Gallery Canary](https://dev.azure.com/uno-platform/Uno%20Platform/_build/results?buildId=213950)
- [Renovate issue #2266 — NuGet MSBuild property versions](https://github.com/renovatebot/renovate/issues/2266)
- [The new Dependabot NuGet updater (Microsoft, 2024)](https://devblogs.microsoft.com/dotnet/the-new-dependabot-nuget-updater/)
- [Microsoft Learn — Central Package Management](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)
