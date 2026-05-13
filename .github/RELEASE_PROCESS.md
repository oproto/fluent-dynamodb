# Release Process

This document describes how to publish a new version of Oproto.FluentDynamoDb to NuGet.org.

## Overview

Releases are triggered by pushing a git tag. There are no release branches — `main` is the source of truth, and tagging a commit is the explicit "publish this" signal.

**Flow:**

```
git tag v1.2.0 → push tag → CI validates → tests run → packages built → GitHub Release created → NuGet publish (with approval)
```

## Prerequisites (One-Time Setup)

### 1. Create the `nuget-production` Environment

1. Go to **Settings → Environments → New environment**
2. Name it `nuget-production`
3. Configure protection rules:
   - **Required reviewers**: Add yourself (or team members who can approve releases)
   - **Wait timer** (optional): Add a delay before approval is possible
4. Save

This environment gates the final NuGet publish step. The workflow will pause and wait for manual approval before pushing packages to nuget.org.

### 2. Add the `NUGET_API_KEY` Secret

1. Go to [nuget.org → API Keys](https://www.nuget.org/account/apikeys)
2. Create a new key:
   - **Key name**: `github-actions` (or similar)
   - **Expiration**: 365 days (set a calendar reminder to rotate)
   - **Package owner**: Your account or organization
   - **Glob pattern**: `Oproto.FluentDynamoDb*`
   - **Scopes**: Push new packages and package versions
3. Copy the generated key
4. In GitHub: **Settings → Environments → nuget-production → Environment secrets**
5. Add secret: Name = `NUGET_API_KEY`, Value = the key you copied

> **Why an environment secret instead of a repo secret?** Environment secrets are only exposed to jobs that reference that environment, adding an extra layer of protection. The key is never available to PR builds or other workflows.

### 3. Verify Branch Protection

Ensure `main` has branch protection enabled so tags can only be created from reviewed, tested code. See [BRANCH_PROTECTION_SETUP.md](./BRANCH_PROTECTION_SETUP.md).

## Publishing a Release

### Step 1: Prepare the Changelog

Update `CHANGELOG.md` with a section for the new version:

```markdown
## [1.2.0] - 2026-05-13

### Added
- New feature X

### Fixed
- Bug Y in query builder
```

The release workflow validates that this section exists. It will fail if the version has no changelog entry.

### Step 2: Commit and Push to Main

```bash
git add CHANGELOG.md
git commit -m "Prepare release 1.2.0"
git push origin main
```

Wait for CI to pass on `main`.

### Step 3: Tag and Push

```bash
git tag v1.2.0
git push origin v1.2.0
```

This triggers the release workflow.

### Step 4: Monitor the Workflow

1. Go to **Actions → Release** in GitHub
2. Watch the jobs execute:
   - **Validate Tag**: Checks semver format and changelog entry
   - **Run Tests**: Full unit + integration test suite
   - **Build Packages**: Builds and validates all NuGet packages
   - **Create GitHub Release**: Attaches packages and release notes
   - **Publish to NuGet.org**: Waits for approval, then pushes

### Step 5: Approve the Publish

When the workflow reaches the `publish-nuget` job, it will pause and show a "Review deployments" banner. Click it, review the packages, and approve.

Packages typically appear on nuget.org within 5-15 minutes after publishing.

## Pre-Release Versions

For testing or early access, use pre-release tags:

```bash
git tag v1.2.0-beta.1
git push origin v1.2.0-beta.1
```

Pre-release versions:
- Are marked as pre-release on the GitHub Release
- Are published to NuGet with a pre-release flag (won't be installed by default)
- Still require a changelog entry (e.g., `## [1.2.0-beta.1] - 2026-05-13`)

This is the recommended way to do a trial run of the full pipeline.

## Dry Run (Without Publishing)

To test the pipeline without publishing anything to NuGet:

1. **Don't configure the `NUGET_API_KEY` secret** — the publish job will fail gracefully after everything else succeeds
2. Or **don't approve** the deployment — the workflow will stay in "waiting" state and eventually time out
3. Or tag a pre-release version — packages go to NuGet but won't affect users who install stable versions

## Packages Published

The release workflow builds and publishes these packages:

| Package | Description |
|---------|-------------|
| `Oproto.FluentDynamoDb` | Core library with source generator |
| `Oproto.FluentDynamoDb.BlobStorage.S3` | S3 blob storage integration |
| `Oproto.FluentDynamoDb.Encryption.Kms` | KMS field encryption |
| `Oproto.FluentDynamoDb.FluentResults` | Result pattern extensions |
| `Oproto.FluentDynamoDb.Geospatial` | Geospatial query support |
| `Oproto.FluentDynamoDb.Logging.Extensions` | Microsoft.Extensions.Logging adapter |
| `Oproto.FluentDynamoDb.NewtonsoftJson` | Newtonsoft.Json serialization |
| `Oproto.FluentDynamoDb.Streams` | DynamoDB Streams processing |
| `Oproto.FluentDynamoDb.SystemTextJson` | System.Text.Json serialization |

All packages are versioned together and published atomically.

## What the Workflow Validates

Before publishing, the workflow checks:

- Tag matches semver format (`v{MAJOR}.{MINOR}.{PATCH}[-{PRERELEASE}]`)
- `CHANGELOG.md` has an entry for the version
- All tests pass (unit + integration, all platforms)
- All packages build successfully
- Each package contains DLL files
- Inter-package dependency versions are consistent (e.g., `FluentResults` depends on the same version of the core package)

## Troubleshooting

### "Changelog entry not found for version X"

Add a `## [X.Y.Z] - YYYY-MM-DD` section to `CHANGELOG.md` and push an updated tag:

```bash
git tag -d v1.2.0           # Delete local tag
git push origin :v1.2.0     # Delete remote tag
# Fix changelog, commit, push
git tag v1.2.0
git push origin v1.2.0
```

### Package version mismatch

The workflow builds all projects with `-p:Version=X.Y.Z`. If a dependent package shows the wrong version for its `Oproto.FluentDynamoDb` dependency, ensure the project uses a `<ProjectReference>` (not a `<PackageReference>`) to the core library.

### NuGet push fails with 409 Conflict

The package version already exists on nuget.org. NuGet packages are immutable — you cannot overwrite a published version. Bump the version and try again.

### NuGet push fails with 403 Forbidden

The API key doesn't have permission to push this package. Verify:
- The key's glob pattern matches `Oproto.FluentDynamoDb*`
- The key hasn't expired
- The key has "Push" scope

### Tests pass locally but fail in CI

Check the test workflow artifacts for platform-specific failures. Common causes:
- DynamoDB Local download issues (network timeouts)
- Java version differences
- File path handling (Windows backslashes)

## Rotating the NuGet API Key

NuGet API keys expire. To rotate:

1. Create a new key on [nuget.org](https://www.nuget.org/account/apikeys)
2. Update the `NUGET_API_KEY` secret in the `nuget-production` environment
3. Revoke the old key on nuget.org

Set a calendar reminder for 30 days before expiration.

## Version Strategy

This project uses [Semantic Versioning](https://semver.org/):

- **MAJOR**: Breaking API changes
- **MINOR**: New features, backward-compatible
- **PATCH**: Bug fixes, backward-compatible

Pre-release suffixes: `-alpha.N`, `-beta.N`, `-rc.N`
