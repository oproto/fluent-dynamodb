# Publishing Packages to CodeArtifact

This guide explains how to publish NuGet packages to AWS CodeArtifact.

## Prerequisites

1. **AWS CLI** installed and configured with appropriate credentials
2. **IAM Permissions** for CodeArtifact operations:
   - `codeartifact:GetAuthorizationToken`
   - `codeartifact:GetRepositoryEndpoint`
   - `codeartifact:PublishPackageVersion`
3. **.NET SDK** installed (for `dotnet nuget push`)

## Quick Start

### Publish All Packages

```bash
./scripts/publish-to-codeartifact.sh \
  --domain YOUR_DOMAIN \
  --domain-owner YOUR_AWS_ACCOUNT_ID \
  --repository YOUR_REPOSITORY \
  --region us-east-1
```

### Publish Specific Version

To publish only the 1.0.0-prerelease20 packages:

```bash
./scripts/publish-to-codeartifact.sh \
  --domain YOUR_DOMAIN \
  --domain-owner YOUR_AWS_ACCOUNT_ID \
  --repository YOUR_REPOSITORY \
  --version 1.0.0-prerelease20
```


### Dry Run (Preview)

See what would be published without actually publishing:

```bash
./scripts/publish-to-codeartifact.sh \
  --domain YOUR_DOMAIN \
  --domain-owner YOUR_AWS_ACCOUNT_ID \
  --repository YOUR_REPOSITORY \
  --version 1.0.0-prerelease20 \
  --dry-run
```

## Using Environment Variables

Instead of passing command line arguments every time, you can set environment variables:

```bash
export CODEARTIFACT_DOMAIN="your-domain"
export CODEARTIFACT_DOMAIN_OWNER="123456789012"
export CODEARTIFACT_REPOSITORY="your-repository"
export AWS_REGION="us-east-1"

# Now you can run without arguments
./scripts/publish-to-codeartifact.sh --version 1.0.0-prerelease20
```

## Common Scenarios

### Scenario 1: Publish All FluentDynamoDb 1.0.0-prerelease20 Packages

```bash
./scripts/publish-to-codeartifact.sh \
  --domain oproto \
  --domain-owner 123456789012 \
  --repository nuget-packages \
  --version 1.0.0-prerelease20
```

This will publish:
- Oproto.FluentDynamoDb.1.0.0-prerelease20.nupkg
- Oproto.FluentDynamoDb.BlobStorage.S3.1.0.0-prerelease20.nupkg
- Oproto.FluentDynamoDb.Encryption.Kms.1.0.0-prerelease20.nupkg
- Oproto.FluentDynamoDb.FluentResults.1.0.0-prerelease20.nupkg
- Oproto.FluentDynamoDb.Geospatial.1.0.0-prerelease20.nupkg
- Oproto.FluentDynamoDb.Logging.Extensions.1.0.0-prerelease20.nupkg
- Oproto.FluentDynamoDb.NewtonsoftJson.1.0.0-prerelease20.nupkg
- Oproto.FluentDynamoDb.Streams.1.0.0-prerelease20.nupkg
- Oproto.FluentDynamoDb.SystemTextJson.1.0.0-prerelease20.nupkg

### Scenario 2: Publish All Lambda.OpenApi 1.3.0-prerelease1 Packages

```bash
./scripts/publish-to-codeartifact.sh \
  --domain oproto \
  --domain-owner 123456789012 \
  --repository nuget-packages \
  --version 1.3.0-prerelease1
```

This will publish:
- Oproto.Lambda.OpenApi.1.3.0-prerelease1.nupkg
- Oproto.Lambda.OpenApi.Merge.1.3.0-prerelease1.nupkg
- Oproto.Lambda.OpenApi.Merge.Cdk.1.3.0-prerelease1.nupkg
- Oproto.Lambda.OpenApi.Merge.Tool.1.3.0-prerelease1.nupkg

### Scenario 3: Publish Everything

```bash
./scripts/publish-to-codeartifact.sh \
  --domain oproto \
  --domain-owner 123456789012 \
  --repository nuget-packages
```

This will publish all .nupkg files in the packages directory.

## Troubleshooting

### Authentication Issues

If you get authentication errors:

```bash
# Verify AWS credentials
aws sts get-caller-identity

# Test CodeArtifact access
aws codeartifact list-repositories --domain YOUR_DOMAIN --domain-owner YOUR_ACCOUNT_ID
```

### Package Already Exists

The script uses `--skip-duplicate` flag, so packages that already exist in CodeArtifact will be skipped automatically. This is safe and expected behavior.

### Permission Denied

If you get "permission denied" when running the script:

```bash
chmod +x scripts/publish-to-codeartifact.sh
```

## Script Features

- **Automatic authentication** with CodeArtifact
- **Skip duplicates** - won't fail if package already exists
- **Version filtering** - publish only specific versions
- **Dry run mode** - preview what will be published
- **Progress tracking** - shows success/skip/fail counts
- **Symbol packages excluded** - only publishes .nupkg files (not .snupkg)

## Notes

- Symbol packages (.snupkg) are automatically excluded from publishing
- The script sorts packages alphabetically before publishing
- Failed packages are listed at the end for easy retry
- Authentication tokens are valid for 12 hours by default
