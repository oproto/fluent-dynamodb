# GitHub Workflows and Configuration

This directory contains the CI/CD workflows and configuration for the Oproto.FluentDynamoDb project.

## Workflows

### Automated Workflows

- **`build.yml`** - Build validation across multiple platforms (Linux, Windows, macOS)
- **`test.yml`** - Unit and integration tests with code coverage
- **`release.yml`** - Automated package building and GitHub release creation
- **`pr-validation.yml`** - Comprehensive PR validation including changelog checks
- **`failure-notification.yml`** - Automatic issue creation for workflow failures

### Manual Configuration Required

⚠️ **Branch Protection Rules** must be configured manually through GitHub's web interface.

See [BRANCH_PROTECTION_SETUP.md](./BRANCH_PROTECTION_SETUP.md) for detailed setup instructions.

Quick link: Navigate to **Settings → Branches** in your repository.

## Setup Checklist

- [x] Build workflow configured
- [x] Test workflow configured
- [x] Release workflow configured
- [x] PR validation workflow configured
- [x] Failure notification workflow configured
- [ ] **Branch protection rules configured** (requires manual setup)
- [ ] Dependabot configured (optional)
- [ ] Status badges added to README (optional)

## Documentation

- [BRANCH_PROTECTION_SETUP.md](./BRANCH_PROTECTION_SETUP.md) - Step-by-step guide for configuring branch protection
- [MANUAL_SETUP_REQUIRED.md](./MANUAL_SETUP_REQUIRED.md) - Overview of manual configuration steps

## Workflow Triggers

### Build Workflow
- Push to `main` or `develop`
- Pull requests to `main` or `develop`
- Manual dispatch

### Test Workflow
- Push to `main` or `develop`
- Pull requests to `main` or `develop`
- Called by PR validation workflow
- Manual dispatch

### Release Workflow
- Push of version tags matching `v*.*.*` (e.g., `v1.0.0`, `v1.2.3-beta.1`)

### PR Validation Workflow
- Pull request opened, synchronized, or reopened
- Targets `main` or `develop`

### Failure Notification Workflow
- Any workflow failure on `main` or `develop` branches

## Status Checks

The following status checks are configured in the workflows:

**Build:**
- `build (ubuntu-latest)`
- `build (windows-latest)`
- `build (macos-latest)`

**Unit Tests:**
- `unit-tests (ubuntu-latest)`
- `unit-tests (windows-latest)`
- `unit-tests (macos-latest)`

**Integration Tests:**
- `integration-tests (ubuntu-latest)`
- `integration-tests (windows-latest)`
- `integration-tests (macos-latest)`

**PR Validation:**
- `validate-pr`

These status check names must be added to branch protection rules for enforcement.

## Release Process

1. Update CHANGELOG.md with version changes
2. Commit changelog updates
3. Create and push version tag: `git tag v1.0.0 && git push origin v1.0.0`
4. Release workflow automatically:
   - Validates tag format
   - Builds NuGet packages
   - Creates GitHub Release
   - Attaches packages to release
5. Manually publish packages to NuGet.org (future: automate)

## Troubleshooting

### Workflows Not Running

- Check that workflows are enabled in repository settings
- Verify trigger conditions match your branch/tag names
- Check workflow run history for errors

### Status Checks Not Appearing

- Workflows must run at least once before status checks appear
- Create a test PR to trigger workflows
- Wait for completion, then check branch protection settings

### Test Failures

- Review workflow logs for detailed error messages
- Check DynamoDB Local logs for integration test failures
- Verify all dependencies are properly installed

## Support

For issues with workflows or CI/CD setup:

1. Check the troubleshooting sections in workflow documentation
2. Review GitHub Actions logs for detailed error messages
3. Create an issue in the repository with workflow run links
