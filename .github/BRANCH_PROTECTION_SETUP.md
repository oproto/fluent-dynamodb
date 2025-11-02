# Branch Protection Rules Setup Guide

This guide provides step-by-step instructions for configuring branch protection rules for the Oproto.FluentDynamoDb repository.

## Overview

Branch protection rules ensure code quality by requiring pull requests, reviews, and passing CI checks before code can be merged to protected branches.

## Prerequisites

- Repository admin access
- All CI/CD workflows must be set up and running:
  - Build workflow (`.github/workflows/build.yml`)
  - Test workflow (`.github/workflows/test.yml`)
  - PR validation workflow (`.github/workflows/pr-validation.yml`)

## Branch Protection Configuration

### Main Branch Protection

Navigate to: **Settings → Branches → Add branch protection rule**

#### Branch Name Pattern
```
main
```

#### Protection Settings

**Require a pull request before merging**
- ✅ Enable this setting
- **Required approvals**: `1`
- ✅ Dismiss stale pull request approvals when new commits are pushed
- ❌ Require review from Code Owners (optional - enable if you have CODEOWNERS file)
- ❌ Restrict who can dismiss pull request reviews (optional)
- ❌ Allow specified actors to bypass required pull requests (leave empty)
- ✅ Require approval of the most recent reviewable push

**Require status checks to pass before merging**
- ✅ Enable this setting
- ✅ Require branches to be up to date before merging

**Required Status Checks** (add all of these):
```
build (ubuntu-latest)
build (windows-latest)
build (macos-latest)
unit-tests (ubuntu-latest)
unit-tests (windows-latest)
unit-tests (macos-latest)
integration-tests (ubuntu-latest)
integration-tests (windows-latest)
integration-tests (macos-latest)
validate-pr
```

**Require conversation resolution before merging**
- ✅ Enable this setting

**Require signed commits**
- ❌ Disable (optional - enable if you want to enforce commit signing)

**Require linear history**
- ❌ Disable (allows merge commits)

**Require deployments to succeed before merging**
- ❌ Disable (not applicable)

**Lock branch**
- ❌ Disable (allows commits via PR)

**Do not allow bypassing the above settings**
- ✅ Enable this setting
- ✅ Include administrators

**Restrict who can push to matching branches**
- ❌ Disable (all users can create PRs)

**Allow force pushes**
- ❌ Disable (prevents force pushes)
- ❌ Specify who can force push (leave empty)

**Allow deletions**
- ❌ Disable (prevents branch deletion)

### Develop Branch Protection

Navigate to: **Settings → Branches → Add branch protection rule**

#### Branch Name Pattern
```
develop
```

#### Protection Settings

Apply the **exact same settings** as the main branch (see above).

The develop branch should have identical protection rules to maintain the same quality standards.

## Verification Steps

After configuring branch protection rules:

### 1. Test Direct Push Prevention
```bash
# This should fail
git checkout main
echo "test" >> README.md
git commit -am "test direct push"
git push origin main
# Expected: Error - protected branch
```

### 2. Test PR Workflow
```bash
# Create a feature branch
git checkout -b test/branch-protection
echo "test" >> README.md
git commit -am "test: verify branch protection"
git push origin test/branch-protection

# Create PR via GitHub UI
# Verify:
# - Cannot merge without approval
# - Cannot merge without passing checks
# - Cannot merge with unresolved conversations
```

### 3. Verify Status Checks
- Create a PR that breaks tests
- Verify that the PR is blocked from merging
- Fix the tests
- Verify that the PR becomes mergeable after checks pass

### 4. Verify Review Requirement
- Create a PR
- Attempt to merge without approval
- Verify merge is blocked
- Get approval from another user
- Verify merge becomes available

## Status Check Names Reference

The status check names must match exactly what appears in the GitHub Actions workflows. Here's how they map:

### Build Workflow (`build.yml`)
- Job name: `build`
- Matrix: `os: [ubuntu-latest, windows-latest, macos-latest]`
- Status check names:
  - `build (ubuntu-latest)`
  - `build (windows-latest)`
  - `build (macos-latest)`

### Test Workflow (`test.yml`)
- Job names: `unit-tests`, `integration-tests`
- Matrix: `os: [ubuntu-latest, windows-latest, macos-latest]`
- Status check names:
  - `unit-tests (ubuntu-latest)`
  - `unit-tests (windows-latest)`
  - `unit-tests (macos-latest)`
  - `integration-tests (ubuntu-latest)`
  - `integration-tests (windows-latest)`
  - `integration-tests (macos-latest)`

### PR Validation Workflow (`pr-validation.yml`)
- Job name: `validate-pr`
- Status check name:
  - `validate-pr`

## Troubleshooting

### Status Checks Not Appearing

**Problem**: Required status checks don't appear in the dropdown when configuring branch protection.

**Solution**: 
1. The workflows must run at least once before status checks appear
2. Create a test PR to trigger the workflows
3. Wait for workflows to complete
4. Return to branch protection settings - status checks should now be available

### Cannot Find Status Check Names

**Problem**: Unsure what the exact status check names are.

**Solution**:
1. Go to a recent PR
2. Scroll to the bottom where checks are displayed
3. The exact names shown there are what you need to add to branch protection

### Accidentally Locked Out

**Problem**: Admin cannot push or merge due to branch protection.

**Solution**:
1. Go to Settings → Branches
2. Edit the branch protection rule
3. Temporarily disable "Do not allow bypassing the above settings"
4. Make your changes
5. Re-enable the setting

### Status Checks Always Failing

**Problem**: Required status checks are failing and blocking all PRs.

**Solution**:
1. Check the workflow runs to identify the issue
2. If it's a systemic issue, temporarily remove the failing check from required status checks
3. Fix the underlying issue
4. Re-add the status check once fixed

## Maintenance

### Adding New Status Checks

When adding new workflows or jobs that should be required:

1. Run the workflow at least once
2. Go to Settings → Branches
3. Edit the branch protection rule
4. Add the new status check name to the required list
5. Save changes

### Removing Status Checks

When removing workflows or jobs:

1. Go to Settings → Branches
2. Edit the branch protection rule
3. Remove the status check from the required list
4. Save changes

## Best Practices

1. **Test First**: Always test branch protection rules on a test repository or branch first
2. **Document Changes**: Keep this guide updated when modifying protection rules
3. **Communicate**: Notify team members before changing branch protection rules
4. **Monitor**: Regularly review branch protection settings to ensure they're still appropriate
5. **Backup**: Take screenshots of your branch protection settings for reference

## Related Documentation

- [GitHub Branch Protection Documentation](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches)
- [Status Checks Documentation](https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/collaborating-on-repositories-with-code-quality-features/about-status-checks)
- Project CONTRIBUTING.md (for contributor guidelines)

## Checklist

Use this checklist when setting up branch protection:

### Main Branch
- [ ] Navigate to Settings → Branches
- [ ] Click "Add branch protection rule"
- [ ] Set branch name pattern to `main`
- [ ] Enable "Require a pull request before merging"
- [ ] Set required approvals to 1
- [ ] Enable "Dismiss stale pull request approvals"
- [ ] Enable "Require status checks to pass before merging"
- [ ] Enable "Require branches to be up to date before merging"
- [ ] Add all 10 required status checks (3 build + 3 unit-tests + 3 integration-tests + 1 validate-pr)
- [ ] Enable "Require conversation resolution before merging"
- [ ] Enable "Do not allow bypassing the above settings"
- [ ] Enable "Include administrators"
- [ ] Disable "Allow force pushes"
- [ ] Disable "Allow deletions"
- [ ] Click "Create" or "Save changes"

### Develop Branch
- [ ] Navigate to Settings → Branches
- [ ] Click "Add branch protection rule"
- [ ] Set branch name pattern to `develop`
- [ ] Apply same settings as main branch (see above)
- [ ] Click "Create" or "Save changes"

### Verification
- [ ] Test direct push prevention
- [ ] Test PR creation and merge workflow
- [ ] Verify status checks are required
- [ ] Verify review requirement
- [ ] Verify conversation resolution requirement
- [ ] Document completion date and who configured it

## Configuration Record

**Configured by**: _________________  
**Date**: _________________  
**Verified by**: _________________  
**Date**: _________________  

**Notes**:
