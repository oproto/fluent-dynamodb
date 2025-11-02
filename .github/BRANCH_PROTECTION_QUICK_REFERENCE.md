# Branch Protection Quick Reference Card

## Setup URL
`https://github.com/[OWNER]/[REPO]/settings/branches`

## Configuration Summary

### Both Main and Develop Branches

**Require pull request before merging**
- ✅ Enabled
- Required approvals: **1**
- ✅ Dismiss stale approvals

**Require status checks**
- ✅ Enabled
- ✅ Require branches up to date

**Required Status Checks (10 total)**
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

**Additional Settings**
- ✅ Require conversation resolution
- ✅ Do not allow bypassing settings
- ✅ Include administrators
- ❌ Allow force pushes (disabled)
- ❌ Allow deletions (disabled)

## Quick Verification

```bash
# Should fail (protected)
git push origin main

# Should work
git push origin feature/my-feature
```

## Common Issues

**Status checks not appearing?**
→ Run workflows at least once first

**Can't find exact status check names?**
→ Check a recent PR for exact names

**Accidentally locked out?**
→ Temporarily disable "Do not allow bypassing"

---

**Full Documentation**: [BRANCH_PROTECTION_SETUP.md](./BRANCH_PROTECTION_SETUP.md)
