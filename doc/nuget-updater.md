---
uid: uno.tools.nuget.updater
---

## About

NuGet.Updater allows batch updates of NuGet packages found in solutions.

## Getting Started

The NuGet Updater can be installed as a standalone .Net Core tool using the following command:
`dotnet tool install -g unoplatform.NuGet.Updater.Tool`

Help can be found with :
`nugetupdater --help`

The NuGet Updater can also be used as part of the NvGet library

## Sample commands

- Update all packages in the current folder (and its subfolders) to the latest stable version found on NuGet.org
```
nugetupdater --useNuGetorg
```
This can also be achieved using the following command
```
nugetupdater --feed=https://api.nuget.org/v3/index.json
```

- Update all packages in `MySolution.sln` to the latest stable version available on NuGet.org
```
nugetupdater --solution=MySolution.sln -n
```

- Update packages to either beta, stable or alpha (whichever's the highest)
```
nugetupdater -s=MySolution.sln -n --version=beta -v=alpha
```

- Update packages to the latest beta version available on a private feed
```
nugetupdater -s=MySolution.sln --feed=https://pkgs.dev.azure.com/account/_packaging/feed/nuget/v3/index.json|personalaccesstoken --version=beta
```

- Update packages from `unoplatform` from NuGet.org, except for `PackageA` and `PackageB`
```
nugetupdater -s=MySolution.sln -n --packageAuthor=unoplatform --ignore=PackageA -i=PackageB
```

- Update packages from `unoplatform` and `microsoft` from NuGet.org, except for `PackageA` and `PackageB`
```
nugetupdater -s=MySolution.sln -n --packageAuthor=unoplatform,microsoft --ignore=PackageA -i=PackageB
```

- Update only `PackageA` and `PackageB` from NuGet.org and a private feed
```
nugetupdater -s=MySolution.sln -n -f=https://pkgs.dev.azure.com/account/_packaging/feed/nuget/v3/index.json|personalaccesstoken --update=PackageA -u=PackageB
```

- Update packages to latest stable, even if a higher version is already found in the solution
```
nugetupdater -s=MySolution.sln -n --allowDowngrade
```

- Update packages to specific versions (forcefully and/or with nuget version ranges). See : https://docs.microsoft.com/en-us/nuget/concepts/package-versioning#version-ranges
```
nugetupdater -s=MySolution.sln -n -v=dev -v=stable --allowDowngrade --versionOverrides=versions.json
```
versions.json example:
```
[
  {
    "PackageId": "Uno.UI",
    "UpdatedVersion": "2.3.0-dev.76" -> Force 2.3.0-dev.76
  },
  {
    "PackageId": "Uno.Wasm.Bootstrap",
    "UpdatedVersion": "(,1.2.0-dev.18]" -> Resolves 1.2.0-dev.18 (-v=dev + -v=stable), 1.0.10 (-v=stable)
  },
  {
    "PackageId": "Uno.WinUI",
    "UpgradePolicy": "Minor" -> Only upgrades when a minor version of the same major is available
  },
  {
    "PackageId": "Uno.Core",
    "UpgradePolicy": "Major" -> Always upgrades
  },
  {
    "PackageId": "Uno.Core.Extensions",
    "UpgradePolicy": "Patch" -> Only upgrades when a patch version of the same major,minor is available
  }
]
```

- Update versions tracked in project properties. eg:
```xml
<UnoVersion>4.9.26</UnoVersion>
```

```
nugetupdater -s=MySolution.sln -n -v=dev -v=stable --allowDowngrade --updateProperties=properties.json
```
properties.json example:
```
[
  {
    "PropertyName": "UnoVersion",
    "PackageId": "Uno.UI"
  }
]
```
In this case the `UnoVersion` property will be updated to the latest version of `Uno.UI` found in the solution.

- Exclude paths from the updater:
```
nugetupdater -x "sub_path1" -x "sub_path2"
```

## Supported types of updates

The nuget updater supports updating:
- `.csproj`, `Directory.Build.props`, `Directory.Build.targets` and `Directory.Packages.props`
    For this type of files, the tool will update:
    - `PackageReference` and `PackageVersion` items
    - MSBuild properties named with the `Uno<PackageId>Version` convention (e.g. `UnoWinuiVersion`, `UnoExtensionsNavigationVersion`). The package ID is derived from the property name as described in [Property → package resolution](#property--package-resolution) below.
- `.nuspec`
  For this type of files, the tool will update `reference` entries.
- `global.json`
  For this type of files, the tool will update `msbuild-sdk` entries

## Property → package resolution

When a version is tracked via an MSBuild property (e.g. `<UnoMaterialVersion>7.0.0-dev.28</UnoMaterialVersion>`), the updater resolves the corresponding NuGet package ID in two complementary ways:

1. **Explicit mapping (recommended for repo-specific cases).** Pass a JSON file via `--updateProperties=` listing `(PropertyName, PackageId)` pairs. See the `properties.json` example above. This is the authoritative path — the updater will treat the property's value as the version of the named package and update both in lockstep.

2. **Name-derivation heuristic.** When no explicit mapping is provided, the updater derives a package ID from the property name by stripping the trailing `Version`, splitting at lowercase→uppercase boundaries, lowercasing, and joining with dots:

   | Property | Derived package ID |
   |---|---|
   | `UnoWinuiVersion` | `uno.winui` |
   | `UnoToolkitVersion` | `uno.toolkit` |
   | `UnoMaterialVersion` | `uno.material` |
   | `UnoExtensionsVersion` | `uno.extensions` |

   **Property name casing matters.** The split happens at every lowercase→uppercase transition, so adjacent uppercase letters (acronyms) produce different IDs: `UnoWinuiVersion` derives to `uno.winui`, but `UnoWinUIVersion` derives to `uno.win.ui`. The lowercase-acronym form (`UnoWinuiVersion`) is what the modern Uno packages publish on NuGet.org and is the established convention across `unoplatform/*` repos. Several of them document it inline in `Directory.Build.props`:

   ```xml
   <!--
   IMPORTANT: The `UnoWinuiVersion` and `UnoSettingsVersion` property
   names follow a convention expected by the uno.nugetupdater. The casing
   is significant in order for the package lookup to be performed properly
   (e.g. UnoWinuiVersion instead of UnoWinUIVersion).
   -->
   ```

   If your property uses acronym casing and the derived ID doesn't match a real package, prefer the lowercase-acronym form or use the explicit `--updateProperties=` override above.

3. **Built-in well-known fallback table.** When the derived ID points at an outdated or deprecated base package on NuGet.org, the updater also consults a small built-in table of canonical replacements. The fallback's version is preferred only when it is *newer* than what the derived ID returns. Currently:

   | Property fragment | Falls back to | Reason |
   |---|---|---|
   | `UnoMaterial` | `Uno.Material.WinUI` | Legacy `uno.material` (UWP era) is at v5.x; modern consumers reference the WinUI 3 variant. |
   | `UnoThemes` | `Uno.Themes.WinUI` | Legacy `uno.themes` (UWP era) is at v5.x; modern consumers reference the WinUI 3 variant (and `Uno.Simple.WinUI` ships in lockstep). |
   | `UnoExtensions` | `Uno.Extensions.Core` | Legacy umbrella `uno.extensions` is at v2.x; modern consumers reference the `Uno.Extensions.*` family (in lockstep, so `.Core` is a canonical lookup). |

   No flag is required to enable the fallback table. If you need behavior that differs from these defaults for a specific repository, use `--updateProperties=` to declare the explicit `(PropertyName, PackageId)` you want.

See [`specs/property-package-fallback-mappings/spec.md`](https://github.com/unoplatform/nuget.updater/blob/main/specs/property-package-fallback-mappings/spec.md) for the full design rationale and rollout plan. (Absolute link so the reference resolves from both the repo and a rendered docs site, since the `specs/` folder lives outside the docfx tree.)
