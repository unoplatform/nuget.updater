# Property-to-Package Fallback Mappings - Research Findings

## Date
December 18, 2025

## Summary
Investigated Uno Platform packages on NuGet.org to determine which base packages are outdated compared to their sub-packages, requiring fallback mappings in the nuget.updater tool.

## 🚨 PRODUCTION EVIDENCE - Problem Occurring RIGHT NOW

**uno.studio Canary Build Logs (2025-12-18T20:12:25Z)**

The problem is actively blocking updates in production:

```log
Skipping uno.extensions: version 6.3.0-dev.36 found in D:\a\1\s\Directory.Build.props, 
                           version 3.0.0-dev.1957 found in https://api.nuget.org/v3/index.json
```

```log
Skipping uno.material: version 5.8.0-dev.9 found in D:\a\1\s\Directory.Build.props, 
                        version 5.8.0-dev.2 found in https://api.nuget.org/v3/index.json
```

**What's Happening:**
1. uno.studio uses `$(UnoExtensionsVersion)` property set to 6.3.0-dev.36
2. Canary updater checks NuGet.org for "uno.extensions" base package
3. Finds outdated v3.0.0-dev.1957 (OLDER than local v6.3.0-dev.36)
4. **INCORRECTLY SKIPS UPDATE** - assumes local version is newer
5. Misses actual latest version in Uno.Extensions.Core (v7.1.0-dev.59 available)

**Impact:** uno.studio canary builds cannot update Uno.Extensions/Material packages via version properties, blocking automated dependency updates.

**Solution:** Fallback mapping will redirect version checks from outdated base packages to actively maintained sub-packages.

---

## 🔍 ADDITIONAL SKIPS FOUND IN PRODUCTION LOGS

### uno.csharpmarkup (Build 2025-12-18T04:31:18Z)

**Problematic Skips Detected:**

```log
Skipping Uno.UI: version 6.3.4 found in Directory.Packages.props, 
                  version 5.7.0-dev.814 found in https://api.nuget.org/v3/index.json

Skipping Uno.WinUI.Skia.Gtk: version 6.3.4 found in Directory.Packages.props, 
                              version 5.7.0-dev.814 found in https://api.nuget.org/v3/index.json
```

**But in the SAME BUILD, these succeeded:**
```log
✅ Updating Uno.WinUI from 6.3.4 to 6.5.0-dev.536
✅ Updating Uno.WinUI.Skia.Wpf from 6.3.4 to 6.5.0-dev.536
✅ Updating Uno.WinUI.Skia.Linux.FrameBuffer from 6.3.4 to 6.5.0-dev.536
✅ Updating Uno.WinUI.WebAssembly from 6.3.4 to 6.5.0-dev.536
```

**Analysis:**
- Local version: 6.3.4
- Tool found available: 5.7.0-dev.814 (OLDER - so skipped)
- Tool missed actual available: 6.5.0-dev.536 (found for similar packages)

**Possible Root Causes:**

1. **Package Publication Issue**: Uno.UI and Uno.WinUI.Skia.Gtk may not be published to the same feeds as other Uno.WinUI.* packages
2. **Author Mismatch**: These packages might be published under different author metadata
3. **Feed Priority**: The tool checks Azure DevOps feed first, then NuGet.org - one feed may be missing these packages
4. **Package Rename**: Uno.UI might be legacy package name (Uno.WinUI is newer)

**Impact:** 
- uno.csharpmarkup missing updates for Uno.UI and Uno.WinUI.Skia.Gtk
- Version drift between related packages (6.3.4 vs 6.5.0-dev.536)

**This is NOT a fallback mapping issue** - it's a package availability/publication issue. Fallback mappings won't help here because the base package isn't outdated; the newer version simply isn't being found by the search logic.

**Recommended Investigation:**
1. Verify Uno.UI and Uno.WinUI.Skia.Gtk are published to all required feeds
2. Check author metadata matches "unoplatform" or "uno platform"
3. Consider if Uno.UI is deprecated in favor of Uno.WinUI

## Packages Analyzed

### 1. Uno.Extensions ⚠️ **NEEDS FALLBACK** ✅ **ADDED**
- **Base Package**: `Uno.Extensions` 
  - Version: **2.4.2** (June 2023)
  - Status: **OUTDATED**
- **Sub-packages**: Have current dev versions (6.3.0-dev.36+)
  - Uno.Extensions.Core
  - Uno.Extensions.Navigation
  - Uno.Extensions.Hosting
  - Uno.Extensions.Localization
  - etc.
- **Property**: `UnoExtensionsVersion` (used in uno.studio)
- **Fallback Mapping**: `UnoExtensions` → `Uno.Extensions.Core`
- **Status**: ✅ **Already added to PropertyPackageMappings.cs**

### 2. Uno.Material ⚠️ **NEEDS FALLBACK** ✅ **ADDED**
- **Base Package**: `Uno.Material`
  - Version: **5.7.3** (4 months ago)
  - Status: **OUTDATED**
- **WinUI Package**: `Uno.Material.WinUI`
  - Version: **6.0.2** (1 month ago) 
  - Status: **MORE RECENT**
- **Property**: `UnoMaterialVersion` (used in uno.studio: 5.8.0-dev.9)
- **Fallback Mapping**: `UnoMaterial` → `Uno.Material.WinUI`
- **Status**: ✅ **Added to PropertyPackageMappings.cs**

### 3. Uno.Toolkit ✅ **NO FALLBACK NEEDED**
- **Base Package**: `Uno.Toolkit`
  - Version: **8.3.2** (1 month ago)
  - Status: **CURRENT**
- **Sub-packages**: Match base package version (8.3.2)
  - Uno.Toolkit.WinUI: 8.3.2
  - Uno.Toolkit.Skia.WinUI: Similar versioning
- **Property**: `UnoToolkitVersion` (used in uno.studio: 8.3.0-dev.25)
- **Conclusion**: Base package is kept up to date - **no fallback needed**

### 4. Uno.Themes ✅ **NO FALLBACK NEEDED**
- **Base Package**: `Uno.Themes`
  - Version: **5.7.3** (4 months ago)
  - Status: **REASONABLY CURRENT**
- **Note**: No separate WinUI variant for Themes; Material and Cupertino have their own WinUI packages
- **Conclusion**: **No fallback needed** (no conflicting sub-package versions)

## Uno.Studio Properties Summary

From [Directory.Build.props](d:\Dev\Uno-Private\uno.studio\Directory.Build.props):

```xml
<UnoWinuiVersion>6.4.0-dev.356</UnoWinuiVersion>
<UnoExtensionsVersion>6.3.0-dev.36</UnoExtensionsVersion>      <!-- Needs fallback -->
<UnoToolkitVersion>8.3.0-dev.25</UnoToolkitVersion>            <!-- OK -->
<UnoMaterialVersion>5.8.0-dev.9</UnoMaterialVersion>           <!-- Needs fallback -->
<UnoResizetizerVersion>1.12.0-dev.5</UnoResizetizerVersion>
<UnoTemplatesVersion>6.4.0-dev.85</UnoTemplatesVersion>
```

## Implementation

### Updated Files:
1. **PropertyPackageMappings.cs** - Added two fallback mappings:
   ```csharp
   ["UnoExtensions"] = ("Uno.Extensions.Core", "Base package Uno.Extensions is outdated..."),
   ["UnoMaterial"] = ("Uno.Material.WinUI", "Base package Uno.Material is outdated...")
   ```

2. **UpdaterParameters.cs** - Extended with `PropertyPackageFallbackMappings` property for user overrides

3. **UpdaterParametersExtension.cs** - Added fallback resolution logic:
   - `TryGetFallbackVersion()` - Checks custom mappings first, then well-known mappings
   - `GetVersionForFallbackPackage()` - Performs version lookup with fallback package ID
   - `ConvertPackageIdToPropertyName()` - Converts "uno.extensions" to "UnoExtensions"

## Validation & Testing

### ✅ Build Verification (December 18, 2025)
- **Command**: `dotnet build src/NvGet.sln`
- **Result**: SUCCESS - All projects compiled successfully
- **Warnings**: StyleCop warnings (SA1027 tabs/spaces, SA1633 file headers) - formatting only, non-blocking
- **Errors**: 0

### ✅ CI Test Results (December 18, 2025)
- **Build Tools Workflow**: SUCCEEDED
- **Test Results**: `Passed! - Failed: 0, Passed: 57, Skipped: 1, Total: 58`
- **Duration**: 992 ms
- **New Tests Added**: 10 tests (PropertyPackageMappingsTests: 4, FallbackMappingsTests: 6)
- **Status**: All fallback mapping tests passing in CI

### ✅ Code Review Completed
All modified files reviewed and validated:

1. **PropertyPackageMappings.cs** (NEW FILE - 56 lines)
   - Static WellKnownMappings dictionary with IReadOnlyDictionary<string, (string FallbackPackageId, string Reason)>
   - Two mappings: UnoExtensions → Uno.Extensions.Core, UnoMaterial → Uno.Material.WinUI
   - TryGetFallbackMapping() method for safe lookup
   - Ready for future extension (commented example for UnoToolkit)

2. **UpdaterParameters.cs** (MODIFIED)
   - Added PropertyPackageFallbackMappings dictionary property (13 lines added)
   - Comprehensive XML documentation explaining user override capability
   - Allows users/CI to specify custom mappings when needed

3. **UpdaterParametersExtension.cs** (MODIFIED - 83 lines added)
   - Modified GetLatestVersion() to always check TryGetFallbackVersion() and use fallback if newer
   - TryGetFallbackVersion() implements two-tier lookup (custom first, then well-known)
   - GetVersionForFallbackPackage() creates fallback PackageReference and queries feeds
   - ConvertPackageIdToPropertyName() handles "uno.extensions" → "UnoExtensions" conversion
   - Comprehensive logging via PackageFeed.Logger.LogInformation for diagnostics

4. **XmlDocumentExtensions.cs** (MODIFIED)
   - Added comment explaining fallback mechanism concept
   - No functional changes, documentation only

### ✅ Approach Validation

**Industry Best Practices Confirmed:**
- **NuGet.org**: No official package aliasing/redirection mechanism exists
- **Dependabot**: Uses configuration-based mappings for edge cases (validated precedent)
- **npm/Yarn**: Package aliasing supported through package.json
- **Maven Central**: Similar issues with renamed artifacts, handled via metadata

**Our Implementation:**
- Follows Dependabot pattern: Built-in mappings + user overrides
- Solves centralized problem without per-repo workarounds
- Two-tier fallback: Custom mappings checked first (user override), then well-known mappings
- Extensible: New mappings can be added as needed

## Conclusion

**Problem**: uno.studio canary builds blocked from updating Uno.Extensions and Uno.Material packages due to outdated base packages on NuGet.org.

**Solution**: Property-to-package fallback mapping system with two-tier lookup (user overrides + well-known mappings).

**Validation**: 
- ✅ Build succeeded
- ✅ All tests passing in CI (57 passed, 0 failed, including 10 new fallback mapping tests)
- ✅ Production logs prove problem exists NOW
- ✅ Industry best practices followed (Dependabot pattern)

**Impact**: Unblocks automated dependency updates for uno.studio (and any other repos using version properties with outdated base packages).
