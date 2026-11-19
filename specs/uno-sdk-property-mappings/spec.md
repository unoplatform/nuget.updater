# Uno.Sdk Property Mappings

## Status
Proposed. Tracking issue: [#45](https://github.com/unoplatform/nuget.updater/issues/45).

## Summary
Read the version property → package pairs from the Uno.Sdk itself, so a property the Uno.Sdk adds later is updated by the canaries without an updater change. The built-in list from [`property-package-fallback-mappings`](../property-package-fallback-mappings/spec.md) gains the Uno.Sdk properties that exist today, for Uno.Sdk versions that don't publish the pairs.

## Motivation

The updater turns a version property into a package ID from its name (`UnoCSharpMarkupVersion` → `uno.csharp.markup`). As of 2026-11-19, for 10 Uno.Sdk properties that ID matches no package, so the updater logs `Skipping [uno.csharp.markup]: no dev or stable version found.` and the property never moves:

| Property | Name-derived ID | Package it controls |
|---|---|---|
| `UnoCSharpMarkupVersion` | `uno.csharp.markup` | `Uno.WinUI.Markup` (+ `Uno.Extensions.Markup.Generators`) |
| `UnoCoreLoggingSingletonVersion` | `uno.core.logging.singleton` | `Uno.Core.Extensions.Logging.Singleton` |
| `UnoFontsVersion` | `uno.fonts` | `Uno.Fonts.OpenSans` (+ `.Fluent`, `.Roboto`) |
| `UnoHotDesignVersion` | `uno.hot.design` | `Uno.UI.HotDesign` |
| `UnoLoggingVersion` | `uno.logging` | `Uno.Extensions.Logging.OSLog` (+ `.WebAssembly.Console`) |
| `UnoMauiVersion` | `uno.maui` | `Uno.Maui.Embedding` (+ `.Handlers`) |
| `UnoReactorVersion` | `uno.reactor` | `Uno.Microsoft.UI.Reactor` |
| `UnoRetargetVersion` | `uno.retarget` | `Uno.WinUI.Retarget` |
| `UnoSettingsVersion` | `uno.settings` | `Uno.Settings.DevServer` |
| `UnoAppMcpVersion` | `uno.app.mcp` | `Uno.UI.App.Mcp` |

Pins the canaries left behind because of this: `UnoCSharpMarkupVersion` 6.3.19 in a private repository, 5.2.14 in the uno.extensions MauiEmbedding sample.

Adding names to the built-in list by hand doesn't keep up: the Uno.Sdk adds properties regularly, and each fix needs an updater release plus a `nugetUpdaterVersion` bump in every canary pipeline.

## Design

### Source of truth
The Uno.Sdk owns the pairing (`ImplicitPackagesResolver` in unoplatform/uno). [unoplatform/uno#26611](https://github.com/unoplatform/uno/pull/26611) writes it into each group of `packages.json`, with a CI check that keeps it in sync with the code:

```json
{ "group": "CSharpMarkup", "version": "7.0.1", "packages": [ "Uno.WinUI.Markup", "Uno.Extensions.Markup.Generators" ], "versionProperty": "UnoCSharpMarkupVersion" }
```

[unoplatform/uno.templates#2444](https://github.com/unoplatform/uno.templates/pull/2444) keeps the field in the public `Uno.Sdk`'s `packages.json`.

### Resolution order (`UpdaterParametersExtension.GetLatestVersion`)
Unchanged from 1.3.1 until the suspicion gate: the name-derived ID is looked up first, and the fallbacks are only consulted when that returns nothing or a version older than the current one.

1. **Built-in list** (`PropertyPackageMappings`): the 3 legacy entries plus the 10 properties above. It covers Uno.Sdk versions without the field (6.x and 7.0 lines) and is not meant to grow: later Uno.Sdk properties come from step 2.
2. **Uno.Sdk mappings** (`UnoSdkPropertyMappings`): only when the built-in list has no entry. The updater reads `targets/netstandard2.0/packages.json` from the latest `Uno.Sdk.Private` on the configured feeds (a forced override on `Uno.Sdk.Private` picks that version instead), once per run, the first time it's needed. A property maps to the first package of its group; groups of one Uno.Sdk release ship together.

Only groups whose first package starts with `Uno.` are used: third-party groups (SkiaSharp, Microsoft.WindowsAppSDK, AndroidX, MAUI…) are not floated by the canaries, and with an empty `--packageAuthor` they would otherwise start moving.

Reading the Uno.Sdk never fails the run: no package, a failed download or an unreadable manifest is logged and gives an empty table.

### Version overrides on the mapped package
A version override set on the mapped package ID now applies to the property too, unless the property's own ID has one. Example: uno.extensions caps `Uno.Extensions.Logging.OSLog` at 1.7.0 because its newest build (1.9.0-dev.2) is stale older work; a repository that sets `UnoLoggingVersion` gets the same cap from the same entry.

## Test plan
`src/NvGet.Tests/Tools/Updater/UnoSdkPropertyMappingsTests.cs`:
- each of the 10 properties, from the real property name through the name-derived ID to its package (built-in list, derived ID absent from the feed);
- parsing: Uno groups only, groups without the field, a byte-order mark;
- a property the built-in list doesn't know is resolved from the Uno.Sdk mappings;
- a property nobody maps is still skipped, and a current name-derived package still wins (1.3.1 order);
- forced and range overrides on the mapped package;
- loading: latest `Uno.Sdk.Private`, a forced override, never failing (no package, download error, broken JSON), one download per run.

Canary simulation: 1.3.1 and this build on every canary branch with its own pipeline arguments, against a feed holding a `Uno.Sdk.Private` with the field (see the PR).

## Rollout
1. Merge unoplatform/uno#26611, then unoplatform/uno.templates#2444, then this.
2. Release the updater (1.3.2 or 1.4.0).
3. Canary pipelines: `nugetUpdaterVersion: '1.*'` instead of a fixed version, once per repository. `dotnet tool install --version "1.*"` installs the newest stable 1.x, so later updater releases reach every canary without a pipeline change.

## Not covered
- Properties that are not Uno.Sdk properties (`_Uno_XamlMerge_Task_Version` in Uno.Themes): fix the name in the repository, or use `--updateProperties=`.
- Packages whose nuspec author is their own ID (`Uno.ShowMeTheXAML`, `Uno.UITest.*`): skipped by the `--packageAuthor` filter, not by the name lookup.
