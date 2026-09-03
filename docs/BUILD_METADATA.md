# FCC Code Desktop — Build Metadata and Versioning

## Purpose

`FCCD-P01-006` establishes the build identity that later About, diagnostics, packaging, provenance, and release-manifest work consume. The identity is generated centrally for every SDK project and is exposed through the project-owned `IBuildMetadataService` contract.

This is build provenance infrastructure only. It does not create a public release, tag, installer, or P22 release claim.

## Canonical version policy

The target public product version remains `1.0.0`. NuGet/project-reference identity also remains `1.0.0` so the committed dependency lock graph is stable. User-visible build identity is tracked separately:

```text
VersionPrefix / project version: 1.0.0
Development product version:     1.0.0-dev
Production product version:      1.0.0
AssemblyVersion:                 1.0.0.0
FileVersion:                     1.0.0.0
```

`Directory.Build.props` owns these values. Project-local version overrides are not part of the P01 contract.

Normal builds use:

```text
FccIsPublicRelease=false
FccBuildChannel=Development
FccProductVersion=1.0.0-dev
```

A future authorized release build must explicitly use `FccIsPublicRelease=true`, which selects `Production` and the unsuffixed product identity `1.0.0`. The build guard rejects production mode unless exact Git source provenance is supplied.

## Source provenance

`FccGitCommit` is resolved in this order:

1. an explicitly supplied MSBuild property;
2. `GITHUB_SHA` on GitHub Actions;
3. the literal `unknown` for an ordinary local development build.

`unknown` is accepted only for development builds. A public/Production build with missing or malformed source provenance fails before assembly metadata is generated.

Accepted Git identities are 40- or 64-character hexadecimal object IDs. CI therefore embeds the exact GitHub candidate/merge SHA it actually builds instead of a guessed branch tip.

The informational version is deterministic for a given product identity and source identity:

```text
1.0.0-dev+<git-sha>   # CI/internal build
1.0.0-dev+unknown     # ordinary local development build
1.0.0+<git-sha>       # future authorized production build
```

No wall-clock timestamp is embedded into assemblies in P01. Release policy requires a build timestamp in the eventual release manifest; that timestamp belongs to release artifact provenance rather than compilation identity so deterministic binaries are not invalidated merely by rebuilding the same source with the same inputs.

## Runtime service

`FCCCodeDesktop.Core.Build` contains:

- `BuildMetadata` — validated immutable build identity;
- `BuildChannel` — `Development` or `Production`;
- `IBuildMetadataService` — project-owned read contract;
- `AssemblyBuildMetadataService` — reads the centrally generated assembly attributes.

The service exposes product/version/informational version, build channel, exact Git identity (or explicit local `unknown`), build configuration, target framework, repository URL, public-release state, and whether exact source provenance is available.

Malformed metadata is rejected instead of silently being treated as valid provenance.

## Validation

Run the dedicated policy validator:

```powershell
pwsh -NoProfile -File .\tools\build\validate-build-metadata.ps1 -RequireDotNet
```

It verifies the central build policy and service/test wiring, then uses only a disposable temporary MSBuild fixture to prove:

- development builds may use explicit `unknown` provenance;
- malformed Git identities fail;
- Production builds with unknown provenance fail;
- Production/channel mismatches fail;
- a Production build with an exact Git object ID passes the metadata gate.

The unit suite also exercises the runtime metadata model/service. Under GitHub Actions it asserts that the metadata embedded into the compiled Core assembly matches `GITHUB_SHA`.

The permanent Windows CI baseline invokes this validator after the Release build and unit/integration suite. A missing build-metadata validation stage is itself rejected by the CI contract validator.
