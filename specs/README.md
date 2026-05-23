# Specs

Design specifications for non-trivial changes to the NuGet Updater tool and the supporting `NvGet` library.

## When to add a spec

Add a spec under a new `specs/<kebab-name>/` folder for any change that:

- Introduces new behavior with non-obvious tradeoffs
- Modifies the resolution algorithm, the discovery heuristic, or the public CLI / task surface
- Coordinates with multiple downstream consumers (e.g. canary YAMLs across `unoplatform/*` repos)
- Establishes a convention that other repos are expected to follow

Trivial fixes (typos, dependency bumps, internal refactors that don't change behavior) do not need a spec.

## Folder layout

```
specs/
├── README.md                                   ← this file
└── <feature-kebab-name>/
    └── spec.md                                 ← single-file specs
    └── (optionally) appendix-<topic>.md        ← longer multi-file specs
```

Use a short, descriptive, kebab-cased folder name. Unlike some Uno repos, this repo does not number its specs — keep folder names self-describing.

## Spec contents

At minimum:

- **Status** (Proposed / Accepted / Implemented / Superseded)
- **Summary** (1–2 sentences)
- **Motivation** with concrete production evidence where applicable
- **Design** (algorithm, API, interactions)
- **Test plan** referencing the test classes / methods
- **Rollout plan** if downstream coordination is required

Optional but recommended for larger changes:

- Industry alignment / prior-art comparison
- Alternatives considered
- Future work

## Cross-referencing

User-facing docs in `doc/` should link to the relevant spec with an **absolute GitHub URL** (e.g. `https://github.com/unoplatform/nuget.updater/blob/main/specs/...`) so the reference resolves from a rendered docfx site — the `specs/` folder lives outside the docfx tree.

## Index

| Spec | Status |
|---|---|
| [property-package-fallback-mappings](property-package-fallback-mappings/spec.md) | Proposed |
