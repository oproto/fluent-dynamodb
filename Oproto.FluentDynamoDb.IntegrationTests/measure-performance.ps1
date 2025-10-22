# Script to measure integration test performance
# Usage: .\measure-performance.ps1

$ErrorActionPreference = "Stop"

Write-Host "=== Integration Test Performance Measurement ===" -ForegroundColor Cyan
Write-Host ""

# Target performance: 30 seconds for full suite
$targetSeconds = 30

Write-Host "Building project..."
dotnet build --configuration Release --no-restore | Out-Null

Write-Host "Running integration tests..."
Write-Host ""

# Run tests and capture timing
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

dotnet test `
    --configuration Release `
    --no-build `
    --filter "Category=Integration" `
    --logger "console;verbosity=normal" `
    --verbosity quiet

$stopwatch.Stop()
$durationSeconds = [Math]::Round($stopwatch.Elapsed.TotalSeconds, 2)

Write-Host ""
Write-Host "=== Performance Results ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Total execution time: $durationSeconds seconds"
Write-Host "Target time: $targetSeconds seconds"
Write-Host ""

# Check if we met the target
if ($durationSeconds -le $targetSeconds) {
    $underTarget = $targetSeconds - $durationSeconds
    Write-Host "✓ Performance target met!" -ForegroundColor Green
    Write-Host "Tests completed $underTarget seconds under target."
    exit 0
} else {
    $overTarget = $durationSeconds - $targetSeconds
    Write-Host "⚠ Performance target not met" -ForegroundColor Yellow
    Write-Host "Tests took $overTarget seconds longer than target."
    Write-Host ""
    Write-Host "Suggestions:"
    Write-Host "  - Ensure DynamoDB Local is already running"
    Write-Host "  - Check for slow tests in the output above"
    Write-Host "  - Consider running with parallel execution enabled"
    exit 1
}
