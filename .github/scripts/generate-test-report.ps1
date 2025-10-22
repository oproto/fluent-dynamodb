# Generate Test Report Script (PowerShell)
# This script analyzes test results and generates a comprehensive report

param(
    [string]$ResultsDir = "./TestResults",
    [string]$OutputFile = "test-report.md"
)

Write-Host "Generating test report from: $ResultsDir"
Write-Host "Output file: $OutputFile"

# Initialize report
$report = @"
# Test Results Report

## Summary

"@

# Count test result files
$unitFiles = 0
$integrationFiles = 0

if (Test-Path "$ResultsDir/Unit") {
    $unitFiles = (Get-ChildItem -Path "$ResultsDir/Unit" -Filter "*.trx" -ErrorAction SilentlyContinue).Count
}

if (Test-Path "$ResultsDir/Integration") {
    $integrationFiles = (Get-ChildItem -Path "$ResultsDir/Integration" -Filter "*.trx" -ErrorAction SilentlyContinue).Count
}

$report += "- Unit Test Result Files: $unitFiles`n"
$report += "- Integration Test Result Files: $integrationFiles`n"
$report += "`n"

# Function to parse TRX summary
function Parse-TrxSummary {
    param(
        [string]$TrxFile,
        [string]$TestType
    )
    
    if (Test-Path $TrxFile) {
        try {
            [xml]$trxContent = Get-Content $TrxFile
            $counters = $trxContent.TestRun.ResultSummary.Counters
            
            $total = $counters.total
            $passed = $counters.passed
            $failed = $counters.failed
            
            $summary = @"
### $TestType

- Total: $total
- Passed: ✅ $passed
- Failed: ❌ $failed

"@
            return $summary
        }
        catch {
            Write-Warning "Failed to parse $TrxFile : $_"
            return ""
        }
    }
    return ""
}

# Process unit test results
if (Test-Path "$ResultsDir/Unit") {
    $report += "## Unit Test Results`n`n"
    
    Get-ChildItem -Path "$ResultsDir/Unit" -Filter "*.trx" -ErrorAction SilentlyContinue | ForEach-Object {
        $summary = Parse-TrxSummary -TrxFile $_.FullName -TestType $_.Name
        $report += $summary
    }
}

# Process integration test results
if (Test-Path "$ResultsDir/Integration") {
    $report += "## Integration Test Results`n`n"
    
    Get-ChildItem -Path "$ResultsDir/Integration" -Filter "*.trx" -ErrorAction SilentlyContinue | ForEach-Object {
        $summary = Parse-TrxSummary -TrxFile $_.FullName -TestType $_.Name
        $report += $summary
    }
}

# Add timestamp
$report += "`n---`n`n"
$report += "Report generated at: $((Get-Date).ToUniversalTime().ToString('yyyy-MM-dd HH:mm:ss')) UTC`n"

# Write report to file
$report | Out-File -FilePath $OutputFile -Encoding UTF8

Write-Host "Test report generated successfully: $OutputFile"
