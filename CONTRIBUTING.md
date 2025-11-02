# Contributing to Oproto.FluentDynamoDb

Thank you for your interest in contributing to Oproto.FluentDynamoDb! This document provides guidelines and instructions for contributing to the project.

## Table of Contents

- [Getting Started](#getting-started)
- [Development Workflow](#development-workflow)
- [Coding Standards](#coding-standards)
- [Testing Guidelines](#testing-guidelines)
- [Release Process](#release-process)

## Getting Started

### Prerequisites

Before you begin, ensure you have the following installed:

- **.NET 8 SDK** or later
  - Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download)
  - Verify installation: `dotnet --version`

- **Java 17** or later (required for integration tests)
  - Required to run DynamoDB Local for integration testing
  - Download from [adoptium.net](https://adoptium.net/) or use your system's package manager
  - Verify installation: `java -version`

- **Git**
  - Download from [git-scm.com](https://git-scm.com/)
  - Verify installation: `git --version`

- **IDE** (recommended)
  - Visual Studio 2022 or later
  - JetBrains Rider
  - Visual Studio Code with C# extension

### Setup Instructions

1. **Clone the repository**
   ```bash
   git clone https://github.com/oproto/Oproto.FluentDynamoDb.git
   cd Oproto.FluentDynamoDb
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Build the project**
   ```bash
   dotnet build
   ```

4. **Verify the setup**
   ```bash
   # Run unit tests to verify everything is working
   dotnet test --filter "Category=Unit"
   ```

### Building the Project

To build the entire solution:

```bash
# Build in Debug configuration (default)
dotnet build

# Build in Release configuration
dotnet build --configuration Release
```

To build a specific project:

```bash
dotnet build Oproto.FluentDynamoDb/Oproto.FluentDynamoDb.csproj
```

### Running Tests

The project has two types of tests:

**Unit Tests** (fast, no external dependencies):
```bash
# Run all unit tests
dotnet test --filter "Category=Unit"

# Run unit tests for a specific project
dotnet test Oproto.FluentDynamoDb.UnitTests
```

**Integration Tests** (require DynamoDB Local):
```bash
# Run all integration tests
dotnet test Oproto.FluentDynamoDb.IntegrationTests

# Run specific integration test class
dotnet test --filter "FullyQualifiedName~QueryOperationsTests"
```

**All Tests**:
```bash
# Run all tests (unit + integration)
dotnet test
```

**With Code Coverage**:
```bash
dotnet test --collect:"XPlat Code Coverage"
```


## Development Workflow

### Branching Strategy

We follow a Git Flow-inspired branching model:

**Main Branches**:
- `main` - Production-ready code, protected branch
- `develop` - Integration branch for features, protected branch

**Supporting Branches**:
- `feature/*` - New features (e.g., `feature/add-batch-operations`)
- `bugfix/*` - Bug fixes for develop (e.g., `bugfix/fix-query-pagination`)
- `hotfix/*` - Critical fixes for production (e.g., `hotfix/fix-null-reference`)

**Branch Workflow**:

1. **For new features**:
   ```bash
   # Create feature branch from develop
   git checkout develop
   git pull origin develop
   git checkout -b feature/your-feature-name
   ```

2. **For bug fixes**:
   ```bash
   # Create bugfix branch from develop
   git checkout develop
   git pull origin develop
   git checkout -b bugfix/your-bugfix-name
   ```

3. **For hotfixes**:
   ```bash
   # Create hotfix branch from main
   git checkout main
   git pull origin main
   git checkout -b hotfix/your-hotfix-name
   ```

### Commit Message Guidelines

Write clear, descriptive commit messages that explain what changed and why:

**Good commit messages**:
```
Add support for batch write operations

Implement BatchWriteItemRequestBuilder to support batch put and delete
operations. This allows users to write up to 25 items in a single request.

Fixes #123
```

**Format**:
- Use present tense ("Add feature" not "Added feature")
- Use imperative mood ("Move cursor to..." not "Moves cursor to...")
- First line should be 50 characters or less
- Add a blank line before the detailed description
- Reference issues and pull requests when relevant

**Commit message structure**:
```
<type>: <subject>

<body>

<footer>
```

**Types**:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `test`: Adding or updating tests
- `refactor`: Code refactoring
- `perf`: Performance improvements
- `chore`: Maintenance tasks

### Pull Request Process

1. **Create your branch** from `develop` (or `main` for hotfixes)

2. **Make your changes**:
   - Write clean, well-documented code
   - Follow the coding standards (see below)
   - Add or update tests as needed
   - Update documentation if needed

3. **Update the CHANGELOG**:
   - Add your changes to the `[Unreleased]` section in `CHANGELOG.md`
   - Categorize under: Added, Changed, Deprecated, Removed, Fixed, or Security
   - Example:
     ```markdown
     ## [Unreleased]
     
     ### Added
     - Support for batch write operations (#123)
     ```

4. **Commit your changes**:
   ```bash
   git add .
   git commit -m "feat: add batch write operations"
   ```

5. **Push to your fork**:
   ```bash
   git push origin feature/your-feature-name
   ```

6. **Create a Pull Request**:
   - Go to the repository on GitHub
   - Click "New Pull Request"
   - Select your branch
   - Fill in the PR template with:
     - Description of changes
     - Related issues
     - Testing performed
     - Breaking changes (if any)

7. **Address review feedback**:
   - Respond to comments
   - Make requested changes
   - Push additional commits
   - Request re-review when ready

8. **Merge requirements**:
   - At least 1 approving review
   - All CI checks must pass (build, tests on all platforms)
   - Conversations must be resolved
   - Branch must be up to date with target branch

### Changelog Update Requirements

**Every PR must update CHANGELOG.md** unless it's a trivial change (typo fix, etc.).

Add your changes to the `[Unreleased]` section under the appropriate category:

```markdown
## [Unreleased]

### Added
- New feature description (#PR-number)

### Changed
- Modified behavior description (#PR-number)

### Fixed
- Bug fix description (#PR-number)
```

**Categories**:
- **Added**: New features
- **Changed**: Changes in existing functionality
- **Deprecated**: Soon-to-be removed features
- **Removed**: Removed features
- **Fixed**: Bug fixes
- **Security**: Security vulnerability fixes

To skip changelog validation (for trivial changes), add the `skip-changelog` label to your PR.


## Coding Standards

### C# Conventions

Follow standard C# coding conventions and .NET best practices:

**Naming Conventions**:
- `PascalCase` for classes, methods, properties, and public fields
- `camelCase` for local variables and parameters
- `_camelCase` for private fields
- `IPascalCase` for interfaces (prefix with 'I')

**Code Style**:
- Use meaningful, descriptive names
- Keep methods focused and small (single responsibility)
- Prefer explicit types over `var` when type is not obvious
- Use nullable reference types (`string?` for nullable strings)
- Add XML documentation comments for public APIs

**Example**:
```csharp
/// <summary>
/// Builds a query request for DynamoDB.
/// </summary>
/// <param name="tableName">The name of the table to query.</param>
/// <returns>A configured query request builder.</returns>
public QueryRequestBuilder Query(string tableName)
{
    if (string.IsNullOrEmpty(tableName))
        throw new ArgumentException("Table name cannot be null or empty.", nameof(tableName));
    
    return new QueryRequestBuilder(_client, tableName);
}
```

### Code Formatting

- **Indentation**: 4 spaces (no tabs)
- **Line length**: Aim for 120 characters or less
- **Braces**: Opening brace on same line (K&R style)
- **Blank lines**: Use to separate logical sections

Use `.editorconfig` settings (if present) or follow the existing code style in the project.

### Documentation Requirements

**XML Documentation Comments**:
- Required for all public APIs (classes, methods, properties)
- Include `<summary>`, `<param>`, `<returns>`, and `<exception>` tags
- Provide clear, concise descriptions
- Include code examples for complex APIs

**Example**:
```csharp
/// <summary>
/// Executes a query operation against DynamoDB.
/// </summary>
/// <param name="keyCondition">The key condition expression.</param>
/// <param name="cancellationToken">Cancellation token for the operation.</param>
/// <returns>A task representing the query result.</returns>
/// <exception cref="ArgumentNullException">Thrown when keyCondition is null.</exception>
/// <example>
/// <code>
/// var result = await table.Query()
///     .Where("pk = :pk")
///     .WithValue(":pk", "USER#123")
///     .ExecuteAsync();
/// </code>
/// </example>
public async Task<QueryResponse> ExecuteAsync(
    string keyCondition, 
    CancellationToken cancellationToken = default)
{
    // Implementation
}
```

**README and Guides**:
- Update README.md if adding new features
- Add examples to documentation
- Update relevant guides in `docs/` folder

### AOT Compatibility

This library is AOT-compatible. When contributing:

- Avoid reflection where possible
- Use source generators instead of runtime code generation
- Test with trimming enabled
- Mark types appropriately with `[DynamicallyAccessedMembers]` if needed


## Testing Guidelines

### Writing Tests

**Test Structure**:
- Use xUnit as the testing framework
- Use FluentAssertions for assertions
- Use NSubstitute for mocking

**Test Organization**:
- Mirror the main library structure in test projects
- One test class per production class
- Group related tests using nested classes or clear naming

**Test Naming Convention**:
```csharp
[Fact]
public void MethodName_Scenario_ExpectedBehavior()
{
    // Arrange
    var builder = new QueryRequestBuilder(client, "TestTable");
    
    // Act
    var result = builder.Where("pk = :pk");
    
    // Assert
    result.Should().NotBeNull();
}
```

**Examples**:
- `Query_WithValidCondition_ReturnsBuilder`
- `ExecuteAsync_WhenTableNotFound_ThrowsException`
- `ToRequest_WithMultipleConditions_BuildsCorrectRequest`

### Unit Tests

**Focus**: Test individual components in isolation

**Guidelines**:
- Mock external dependencies (DynamoDB client, etc.)
- Test one thing per test method
- Use descriptive test names
- Cover happy path and error cases
- Aim for high code coverage (70%+)

**Example**:
```csharp
public class QueryRequestBuilderTests
{
    private readonly IAmazonDynamoDB _mockClient;
    
    public QueryRequestBuilderTests()
    {
        _mockClient = Substitute.For<IAmazonDynamoDB>();
    }
    
    [Fact]
    public void Where_WithValidExpression_SetsKeyConditionExpression()
    {
        // Arrange
        var builder = new QueryRequestBuilder(_mockClient, "TestTable");
        
        // Act
        builder.Where("pk = :pk");
        var request = builder.ToRequest();
        
        // Assert
        request.KeyConditionExpression.Should().Be("pk = :pk");
    }
}
```

### Integration Tests

**Focus**: Test interactions with DynamoDB Local

**Guidelines**:
- Use DynamoDB Local for testing
- Create and clean up test tables
- Test real DynamoDB operations
- Verify end-to-end scenarios

**Example**:
```csharp
[Collection("DynamoDbLocal")]
public class QueryOperationsTests : IntegrationTestBase
{
    [Fact]
    public async Task Query_WithKeyCondition_ReturnsMatchingItems()
    {
        // Arrange
        await CreateTestTableAsync();
        await PutTestItemAsync("USER#123", "PROFILE");
        
        // Act
        var response = await Table.Query()
            .Where("pk = :pk")
            .WithValue(":pk", "USER#123")
            .ExecuteAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        response.Items[0]["pk"].S.Should().Be("USER#123");
    }
}
```

### Test Categories

Use traits to categorize tests:

```csharp
[Trait("Category", "Unit")]
public class UnitTestClass { }

[Trait("Category", "Integration")]
public class IntegrationTestClass { }
```

This allows running specific test categories:
```bash
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
```

### Code Coverage

- Aim for at least 70% code coverage
- Focus on testing critical paths
- Don't write tests just to increase coverage
- Use coverage reports to identify untested code

Generate coverage reports:
```bash
dotnet test --collect:"XPlat Code Coverage"
```


## Release Process

### Semantic Versioning

This project follows [Semantic Versioning 2.0.0](https://semver.org/):

**Version Format**: `MAJOR.MINOR.PATCH[-PRERELEASE]`

**When to increment**:

- **MAJOR** version (e.g., 1.0.0 → 2.0.0):
  - Breaking changes to public API
  - Removal of deprecated features
  - Major architectural changes
  - Changes that require users to modify their code

- **MINOR** version (e.g., 1.0.0 → 1.1.0):
  - New features added
  - Backward-compatible API additions
  - Significant enhancements
  - Deprecation of features (but not removal)

- **PATCH** version (e.g., 1.0.0 → 1.0.1):
  - Bug fixes
  - Performance improvements
  - Documentation updates
  - Internal refactoring (no API changes)

### Pre-release Versions

Pre-release versions use suffixes:

- **alpha** (e.g., `2.0.0-alpha.1`): Early testing, unstable, breaking changes expected
- **beta** (e.g., `2.0.0-beta.1`): Feature complete, testing phase, API mostly stable
- **rc** (e.g., `2.0.0-rc.1`): Release candidate, final testing before stable release

**Pre-release progression**:
```
2.0.0-alpha.1 → 2.0.0-alpha.2 → 2.0.0-beta.1 → 2.0.0-rc.1 → 2.0.0
```

### Creating a Stable Release

**Prerequisites**:
- All features for the release are merged to `develop`
- All tests pass on `develop`
- CHANGELOG.md is up to date

**Steps**:

1. **Merge develop to main**:
   ```bash
   git checkout main
   git pull origin main
   git merge develop
   ```

2. **Update CHANGELOG.md**:
   - Move items from `[Unreleased]` to a new version section
   - Add release date
   - Ensure all changes are categorized
   
   Example:
   ```markdown
   ## [Unreleased]
   
   ## [1.2.0] - 2025-11-01
   
   ### Added
   - Support for batch write operations
   - New pagination helpers
   
   ### Fixed
   - Query builder null reference issue
   ```

3. **Commit the changelog**:
   ```bash
   git add CHANGELOG.md
   git commit -m "chore: prepare release 1.2.0"
   ```

4. **Create and push the version tag**:
   ```bash
   git tag v1.2.0
   git push origin main
   git push origin v1.2.0
   ```

5. **Automated release workflow**:
   - GitHub Actions will automatically:
     - Build all NuGet packages
     - Create a GitHub Release
     - Attach packages to the release
     - Extract release notes from CHANGELOG.md

6. **Publish to NuGet.org** (manual for now):
   - Download packages from GitHub Release
   - Verify package contents
   - Publish using `dotnet nuget push`:
   ```bash
   dotnet nuget push Oproto.FluentDynamoDb.1.2.0.nupkg \
     --api-key YOUR_API_KEY \
     --source https://api.nuget.org/v3/index.json
   ```

7. **Merge main back to develop**:
   ```bash
   git checkout develop
   git merge main
   git push origin develop
   ```

### Creating a Pre-release

Pre-releases are typically created from the `develop` branch for testing.

**Steps**:

1. **Ensure develop is ready**:
   ```bash
   git checkout develop
   git pull origin develop
   ```

2. **Update CHANGELOG.md**:
   - Add a pre-release section
   
   Example:
   ```markdown
   ## [Unreleased]
   
   ## [2.0.0-beta.1] - 2025-11-01
   
   ### Added
   - New experimental feature X
   ```

3. **Commit the changelog**:
   ```bash
   git add CHANGELOG.md
   git commit -m "chore: prepare pre-release 2.0.0-beta.1"
   ```

4. **Create and push the pre-release tag**:
   ```bash
   git tag v2.0.0-beta.1
   git push origin develop
   git push origin v2.0.0-beta.1
   ```

5. **Automated workflow**:
   - GitHub Actions will create a pre-release on GitHub
   - Packages will be marked as pre-release

6. **Publish to NuGet.org** (optional):
   ```bash
   dotnet nuget push Oproto.FluentDynamoDb.2.0.0-beta.1.nupkg \
     --api-key YOUR_API_KEY \
     --source https://api.nuget.org/v3/index.json
   ```

### Hotfix Process

For critical bugs in production that need immediate fixes:

1. **Create hotfix branch from main**:
   ```bash
   git checkout main
   git pull origin main
   git checkout -b hotfix/critical-bug-fix
   ```

2. **Fix the issue**:
   - Make minimal changes to fix the bug
   - Add tests to verify the fix
   - Update CHANGELOG.md

3. **Test thoroughly**:
   ```bash
   dotnet test
   ```

4. **Create PR to main**:
   - Get it reviewed and approved
   - Merge to main

5. **Create patch version tag**:
   ```bash
   git checkout main
   git pull origin main
   git tag v1.2.1
   git push origin main
   git push origin v1.2.1
   ```

6. **Merge to develop**:
   ```bash
   git checkout develop
   git merge main
   git push origin develop
   ```

7. **Release workflow runs automatically**

### Version Tag Examples

**Stable releases**:
- `v1.0.0` - First stable release
- `v1.1.0` - Minor version with new features
- `v1.1.1` - Patch version with bug fixes
- `v2.0.0` - Major version with breaking changes

**Pre-releases**:
- `v2.0.0-alpha.1` - First alpha release
- `v2.0.0-alpha.2` - Second alpha release
- `v2.0.0-beta.1` - First beta release
- `v2.0.0-rc.1` - First release candidate
- `v2.0.0` - Final stable release

### Verifying Packages Before Publishing

Before publishing to NuGet.org, thoroughly verify the packages to ensure quality and correctness.

#### 1. Download Packages from GitHub Release

After the release workflow completes:

1. Go to the [Releases page](https://github.com/oproto/Oproto.FluentDynamoDb/releases)
2. Find your release (e.g., "Release v1.2.0")
3. Download all `.nupkg` and `.snupkg` files from the Assets section
4. Download the `package-manifest.json` for reference

#### 2. Inspect Package Contents

Extract and examine each package:

```bash
# Create a directory for inspection
mkdir package-inspection
cd package-inspection

# Extract the package (it's a zip file)
unzip ../Oproto.FluentDynamoDb.1.2.0.nupkg -d Oproto.FluentDynamoDb

# Inspect the structure
tree Oproto.FluentDynamoDb
```

**Verify the following**:

- ✅ **DLL files** in `lib/net8.0/` directory
- ✅ **XML documentation** files (`.xml`) alongside DLLs
- ✅ **README.md** in the package root
- ✅ **LICENSE** file in the package root
- ✅ **Icon** file (if configured)
- ✅ **Dependencies** listed correctly in `.nuspec`

**Check for issues**:
- ❌ No debug symbols (`.pdb`) in release packages (should be in `.snupkg`)
- ❌ No test assemblies or test-related files
- ❌ No unnecessary dependencies
- ❌ No absolute paths in any files

#### 3. Verify Package Metadata

Inspect the `.nuspec` file inside the package:

```bash
# Extract and view the nuspec
cat Oproto.FluentDynamoDb/Oproto.FluentDynamoDb.nuspec
```

**Verify**:
- Version number matches the release tag
- Package ID is correct
- Description is accurate and helpful
- Authors and copyright information is correct
- Project URL points to the repository
- License information is present
- Tags are relevant and helpful
- Dependencies list correct versions
- Target framework is `net8.0`

#### 4. Test Package Locally

Create a test project to verify the package works:

```bash
# Create a test console app
mkdir test-package
cd test-package
dotnet new console -n PackageTest
cd PackageTest

# Add the local package
dotnet add package Oproto.FluentDynamoDb \
  --source /path/to/downloaded/packages \
  --version 1.2.0

# Verify it restores and builds
dotnet restore
dotnet build
```

**Test basic functionality**:

```csharp
// Program.cs
using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Requests;

var client = new AmazonDynamoDBClient();
var builder = new QueryRequestBuilder(client, "TestTable");

Console.WriteLine("Package loaded successfully!");
```

Run the test:
```bash
dotnet run
```

#### 5. Verify Symbol Package

Check the symbol package (`.snupkg`):

```bash
unzip ../Oproto.FluentDynamoDb.1.2.0.snupkg -d symbols
```

**Verify**:
- Contains `.pdb` files
- PDB files match the DLL versions
- Source link information is embedded (for source debugging)

#### 6. Compare with Previous Version

If updating an existing package:

```bash
# Download previous version
dotnet tool install -g NuGetPackageExplorer

# Compare packages visually
# Or use command line tools to diff
```

**Check for**:
- Breaking changes are documented
- Version number follows semantic versioning
- Changelog reflects all changes

### Manual NuGet Publishing Steps

Once packages are verified, publish them to NuGet.org manually.

#### Prerequisites

1. **NuGet.org Account**:
   - Create account at [nuget.org](https://www.nuget.org/)
   - Verify email address

2. **API Key**:
   - Go to [nuget.org/account/apikeys](https://www.nuget.org/account/apikeys)
   - Click "Create"
   - Set key name (e.g., "Oproto.FluentDynamoDb Release")
   - Set expiration (recommend 1 year)
   - Select packages: Choose specific packages or pattern `Oproto.FluentDynamoDb*`
   - Set glob pattern: `Oproto.FluentDynamoDb*`
   - Click "Create"
   - **Copy and save the API key securely** (shown only once)

#### Publishing Packages

**Option 1: Using dotnet CLI** (Recommended)

```bash
# Navigate to the directory with downloaded packages
cd ~/Downloads/release-packages

# Publish each package
dotnet nuget push Oproto.FluentDynamoDb.1.2.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json

dotnet nuget push Oproto.FluentDynamoDb.BlobStorage.S3.1.2.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json

dotnet nuget push Oproto.FluentDynamoDb.Encryption.Kms.1.2.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json

dotnet nuget push Oproto.FluentDynamoDb.FluentResults.1.2.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json

dotnet nuget push Oproto.FluentDynamoDb.Logging.Extensions.1.2.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json

dotnet nuget push Oproto.FluentDynamoDb.NewtonsoftJson.1.2.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json

dotnet nuget push Oproto.FluentDynamoDb.SystemTextJson.1.2.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

**Publish all packages at once**:
```bash
# Publish all .nupkg files in current directory
for pkg in *.nupkg; do
  echo "Publishing $pkg..."
  dotnet nuget push "$pkg" \
    --api-key YOUR_API_KEY \
    --source https://api.nuget.org/v3/index.json \
    --skip-duplicate
done
```

**Option 2: Using NuGet.org Web Interface**

1. Go to [nuget.org/packages/manage/upload](https://www.nuget.org/packages/manage/upload)
2. Click "Browse" and select the `.nupkg` file
3. Click "Upload"
4. Review package details
5. Click "Submit"
6. Repeat for each package

#### Publishing Pre-release Packages

Pre-release packages are published the same way. NuGet.org automatically detects pre-release versions from the version suffix.

```bash
# Pre-release versions are automatically marked
dotnet nuget push Oproto.FluentDynamoDb.2.0.0-beta.1.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

**Note**: Pre-release packages:
- Are not shown by default in package managers
- Require explicit version specification or "Include prerelease" option
- Are useful for testing before stable release

#### Publishing Symbol Packages

Symbol packages (`.snupkg`) are published to the symbol server:

```bash
# Symbol packages are pushed to the symbol server automatically
dotnet nuget push Oproto.FluentDynamoDb.1.2.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json \
  --symbol-source https://api.nuget.org/v3/index.json \
  --symbol-api-key YOUR_API_KEY
```

Or push symbols separately:
```bash
dotnet nuget push Oproto.FluentDynamoDb.1.2.0.snupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

#### Verify Publication

After publishing, verify the packages are available:

1. **Check NuGet.org**:
   - Go to `https://www.nuget.org/packages/Oproto.FluentDynamoDb/1.2.0`
   - Verify version is listed
   - Check package details are correct

2. **Test installation**:
   ```bash
   # Create a test project
   mkdir test-published
   cd test-published
   dotnet new console
   
   # Install from NuGet.org (may take a few minutes to propagate)
   dotnet add package Oproto.FluentDynamoDb --version 1.2.0
   
   # Verify it works
   dotnet build
   ```

3. **Check all packages**:
   - Verify all 7 packages are published
   - Check dependencies are resolved correctly
   - Verify README and license are displayed

#### Post-Publication

1. **Update GitHub Release**:
   - Add links to NuGet.org packages in the release notes
   - Example:
     ```markdown
     ## NuGet Packages
     
     - [Oproto.FluentDynamoDb](https://www.nuget.org/packages/Oproto.FluentDynamoDb/1.2.0)
     - [Oproto.FluentDynamoDb.BlobStorage.S3](https://www.nuget.org/packages/Oproto.FluentDynamoDb.BlobStorage.S3/1.2.0)
     - ...
     ```

2. **Announce the release**:
   - Post in GitHub Discussions (if enabled)
   - Update project README if needed
   - Notify users through appropriate channels

3. **Monitor for issues**:
   - Watch for GitHub issues
   - Monitor NuGet.org package statistics
   - Check for installation problems

### Troubleshooting Releases

#### Tag and Version Issues

**Problem**: Tag format is invalid
```
Error: Invalid version tag format: v1.0
```

**Solution**:
- Ensure tag follows `v{MAJOR}.{MINOR}.{PATCH}[-{PRERELEASE}]` format
- Valid examples: `v1.0.0`, `v2.1.3`, `v2.0.0-beta.1`
- Check for typos in version number
- Don't use `v1.0` or `v1` (must have all three numbers)

**Problem**: Changelog entry missing
```
Error: Changelog entry not found for version 1.2.0
```

**Solution**:
- Add a section in CHANGELOG.md for the version
- Format: `## [1.2.0] - 2025-11-01`
- Ensure the version number matches exactly
- Place it above the `[Unreleased]` section

**Problem**: Version mismatch between tag and changelog
```
Error: Tag version 1.2.0 does not match changelog version 1.2.1
```

**Solution**:
- Ensure CHANGELOG.md version matches the git tag
- Update CHANGELOG.md and commit before tagging
- Delete and recreate the tag if needed:
  ```bash
  git tag -d v1.2.0
  git push origin :refs/tags/v1.2.0
  # Fix changelog, commit, then recreate tag
  git tag v1.2.0
  git push origin v1.2.0
  ```

#### Build and Workflow Issues

**Problem**: Build fails during release workflow
```
Error: Build failed with exit code 1
```

**Solution**:
1. Check GitHub Actions logs for specific error
2. Verify all projects build locally:
   ```bash
   dotnet build --configuration Release
   ```
3. Ensure all tests pass:
   ```bash
   dotnet test
   ```
4. Check for missing dependencies or version conflicts
5. Verify .NET 8 SDK is available in the workflow

**Problem**: Package build fails
```
Error: Failed to create package for Oproto.FluentDynamoDb
```

**Solution**:
1. Check project file (`.csproj`) for issues
2. Verify `<IsPackable>true</IsPackable>` is set
3. Check for missing required metadata:
   - `<PackageId>`
   - `<Version>`
   - `<Authors>`
   - `<Description>`
4. Build package locally to see detailed errors:
   ```bash
   dotnet pack --configuration Release
   ```

**Problem**: Workflow doesn't trigger on tag push
```
No workflow run created for tag v1.2.0
```

**Solution**:
1. Verify tag format matches workflow trigger pattern
2. Check workflow file syntax:
   ```yaml
   on:
     push:
       tags:
         - 'v*.*.*'
   ```
3. Ensure tag was pushed to remote:
   ```bash
   git push origin v1.2.0
   ```
4. Check GitHub Actions is enabled for the repository

#### Package Validation Issues

**Problem**: Package validation fails
```
Error: Package validation failed - missing required files
```

**Solution**:
1. Check package metadata in .csproj files
2. Verify all required files are included:
   - DLL files
   - XML documentation
   - README.md
   - LICENSE
3. Check `<PackageReadmeFile>` and `<PackageLicenseFile>` settings
4. Ensure files are set to be included in package:
   ```xml
   <ItemGroup>
     <None Include="README.md" Pack="true" PackagePath="/" />
     <None Include="LICENSE" Pack="true" PackagePath="/" />
   </ItemGroup>
   ```

**Problem**: Symbol package is missing or invalid
```
Warning: Symbol package not found
```

**Solution**:
1. Ensure `<IncludeSymbols>true</IncludeSymbols>` is set
2. Set `<SymbolPackageFormat>snupkg</SymbolPackageFormat>`
3. Verify PDB files are generated:
   ```xml
   <PropertyGroup>
     <DebugType>embedded</DebugType>
   </PropertyGroup>
   ```

#### NuGet Publishing Issues

**Problem**: Authentication fails when publishing
```
Error: 401 Unauthorized
```

**Solution**:
1. Verify API key is correct and not expired
2. Check API key has permission for the package
3. Regenerate API key if needed
4. Ensure you're using the correct NuGet source URL

**Problem**: Package already exists
```
Error: 409 Conflict - Package version already exists
```

**Solution**:
- NuGet.org doesn't allow overwriting published versions
- You must increment the version number
- Delete the tag and create a new one with incremented version:
  ```bash
  git tag -d v1.2.0
  git push origin :refs/tags/v1.2.0
  git tag v1.2.1
  git push origin v1.2.1
  ```

**Problem**: Package upload times out
```
Error: Request timeout
```

**Solution**:
1. Check internet connection
2. Try uploading again (may be temporary NuGet.org issue)
3. Try uploading packages one at a time instead of batch
4. Check package size (very large packages may timeout)

**Problem**: Package not appearing on NuGet.org
```
Package published but not visible
```

**Solution**:
1. Wait 5-10 minutes for indexing
2. Check package status at `https://www.nuget.org/packages/Oproto.FluentDynamoDb`
3. Verify package passed validation
4. Check for email from NuGet.org about validation issues

#### DynamoDB Local Issues (Integration Tests)

**Problem**: DynamoDB Local fails to start
```
Error: Failed to start DynamoDB Local
```

**Solution**:
1. Verify Java 17+ is installed:
   ```bash
   java -version
   ```
2. Check port 8000 is not in use:
   ```bash
   lsof -i :8000  # macOS/Linux
   netstat -ano | findstr :8000  # Windows
   ```
3. Check DynamoDB Local download succeeded
4. Verify DynamoDB Local files are not corrupted

**Problem**: Integration tests timeout
```
Error: Test execution timeout after 30 seconds
```

**Solution**:
1. Increase timeout in test configuration
2. Check DynamoDB Local is responding:
   ```bash
   curl http://localhost:8000
   ```
3. Check test logs for specific failures
4. Run integration tests locally to reproduce

#### General Troubleshooting Tips

1. **Check GitHub Actions logs**:
   - Go to Actions tab in repository
   - Click on the failed workflow run
   - Expand failed steps to see detailed logs

2. **Reproduce locally**:
   - Try to reproduce the issue on your local machine
   - Use the same commands as the workflow
   - Check for environment-specific issues

3. **Verify prerequisites**:
   - .NET 8 SDK installed
   - Java 17+ for integration tests
   - Git configured correctly
   - All dependencies restored

4. **Clean and rebuild**:
   ```bash
   # Clean all build outputs
   dotnet clean
   rm -rf */bin */obj
   
   # Restore and rebuild
   dotnet restore
   dotnet build --configuration Release
   ```

5. **Check for common issues**:
   - Uncommitted changes
   - Merge conflicts
   - Missing files
   - Incorrect file paths
   - Permission issues

6. **Get help**:
   - Check existing GitHub Issues
   - Create a new issue with:
     - Error message
     - Steps to reproduce
     - Environment details
     - Relevant logs
   - Tag maintainers if urgent

---

## Questions or Issues?

If you have questions or run into issues:

- Check existing [GitHub Issues](https://github.com/oproto/Oproto.FluentDynamoDb/issues)
- Create a new issue with details
- Join discussions in pull requests

Thank you for contributing to Oproto.FluentDynamoDb! 🎉
