using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Xunit;

namespace Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;

public class DynamoDbLocalFixture : IAsyncLifetime
{
    private Process? _dynamoDbProcess;
    private bool _startedByFixture;
    private readonly Stopwatch _startupTimer = new();
    
    public IAmazonDynamoDB Client { get; private set; } = null!;
    public string ServiceUrl { get; private set; } = "http://localhost:8000";
    
    /// <summary>
    /// Gets the startup time in milliseconds. Returns 0 if reusing an existing instance.
    /// </summary>
    public long StartupTimeMs => _startupTimer.ElapsedMilliseconds;
    
    /// <summary>
    /// Gets whether this fixture started DynamoDB Local or reused an existing instance.
    /// </summary>
    public bool ReusedExistingInstance => !_startedByFixture;
    
    public async Task InitializeAsync()
    {
        _startupTimer.Start();
        
        try
        {
            // 1. Check if DynamoDB Local is already running
            if (await IsDynamoDbLocalRunningAsync())
            {
                _startupTimer.Stop();
                Console.WriteLine($"[DynamoDB Local] Already running, reusing existing instance (check took {_startupTimer.ElapsedMilliseconds}ms)");
                Client = CreateClient();
                _startedByFixture = false;
                return;
            }
            
            // 2. Download DynamoDB Local if not present
            await EnsureDynamoDbLocalInstalledAsync();
            
            // 3. Start DynamoDB Local process
            _dynamoDbProcess = StartDynamoDbLocal();
            _startedByFixture = true;
            
            // 4. Wait for service to be ready
            await WaitForDynamoDbLocalAsync();
            
            // 5. Create client
            Client = CreateClient();
            
            _startupTimer.Stop();
            Console.WriteLine($"[DynamoDB Local] Started successfully in {_startupTimer.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to start DynamoDB Local. " +
                "Ensure Java is installed and DynamoDB Local is downloaded. " +
                "Run: ./scripts/setup-dynamodb-local.sh\n" +
                $"Error: {ex.Message}", ex);
        }
    }
    
    public async Task DisposeAsync()
    {
        // Only stop DynamoDB Local if we started it
        if (_startedByFixture && _dynamoDbProcess != null && !_dynamoDbProcess.HasExited)
        {
            try
            {
                Console.WriteLine("[DynamoDB Local] Stopping process...");
                _dynamoDbProcess.Kill();
                await _dynamoDbProcess.WaitForExitAsync();
                Console.WriteLine("[DynamoDB Local] Stopped successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DynamoDB Local] Warning: Failed to stop process: {ex.Message}");
            }
        }
        
        Client?.Dispose();
    }
    
    private IAmazonDynamoDB CreateClient()
    {
        var config = new AmazonDynamoDBConfig
        {
            ServiceURL = ServiceUrl,
            AuthenticationRegion = "us-east-1"
        };
        
        return new AmazonDynamoDBClient(
            new BasicAWSCredentials("dummy", "dummy"),
            config);
    }
    
    private async Task<bool> IsDynamoDbLocalRunningAsync()
    {
        try
        {
            using var client = CreateClient();
            await client.ListTablesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    private async Task EnsureDynamoDbLocalInstalledAsync()
    {
        var dynamoDbDir = GetDynamoDbLocalDirectory();
        var jarPath = GetDynamoDbLocalJarPath();
        
        if (File.Exists(jarPath))
        {
            Console.WriteLine($"[DynamoDB Local] Found at {dynamoDbDir}");
            return;
        }
        
        Console.WriteLine($"[DynamoDB Local] Not found, downloading... (Platform: {GetPlatformName()})");
        
        Directory.CreateDirectory(dynamoDbDir);
        
        var downloadUrl = "https://s3.us-west-2.amazonaws.com/dynamodb-local/dynamodb_local_latest.tar.gz";
        var tarPath = Path.Combine(dynamoDbDir, "dynamodb_local_latest.tar.gz");
        
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(5);
        
        var response = await httpClient.GetAsync(downloadUrl);
        response.EnsureSuccessStatusCode();
        
        await using var fileStream = File.Create(tarPath);
        await response.Content.CopyToAsync(fileStream);
        
        Console.WriteLine("[DynamoDB Local] Downloaded, extracting...");
        
        // Extract tar.gz - platform-specific handling
        await ExtractTarGzAsync(tarPath, dynamoDbDir);
        
        // Clean up tar file
        File.Delete(tarPath);
        
        Console.WriteLine("[DynamoDB Local] Installation complete");
    }
    
    private async Task ExtractTarGzAsync(string tarPath, string destinationDir)
    {
        ProcessStartInfo startInfo;
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows: Use tar.exe (available in Windows 10+)
            startInfo = new ProcessStartInfo
            {
                FileName = "tar.exe",
                Arguments = $"-xzf \"{tarPath}\" -C \"{destinationDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        else
        {
            // Linux/macOS: Use standard tar
            startInfo = new ProcessStartInfo
            {
                FileName = "tar",
                Arguments = $"-xzf \"{tarPath}\" -C \"{destinationDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
        }
        
        var extractProcess = Process.Start(startInfo);
        if (extractProcess == null)
        {
            throw new InvalidOperationException("Failed to start extraction process");
        }
        
        await extractProcess.WaitForExitAsync();
        
        if (extractProcess.ExitCode != 0)
        {
            var error = await extractProcess.StandardError.ReadToEndAsync();
            throw new InvalidOperationException(
                $"Failed to extract DynamoDB Local on {GetPlatformName()}: {error}");
        }
    }
    
    private string GetPlatformName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "Windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "Linux";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "macOS";
        return "Unknown";
    }
    
    private Process StartDynamoDbLocal()
    {
        var javaPath = FindJavaExecutable();
        var dynamoDbJar = GetDynamoDbLocalJarPath();
        var dynamoDbDir = GetDynamoDbLocalDirectory();
        
        Console.WriteLine($"[DynamoDB Local] Starting with Java: {javaPath}");
        Console.WriteLine($"[DynamoDB Local] JAR: {dynamoDbJar}");
        
        var startInfo = new ProcessStartInfo
        {
            FileName = javaPath,
            Arguments = $"-Djava.library.path=./DynamoDBLocal_lib -jar {Path.GetFileName(dynamoDbJar)} -inMemory -port 8000",
            WorkingDirectory = dynamoDbDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        var process = Process.Start(startInfo);
        
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start DynamoDB Local process");
        }
        
        // Capture output for debugging
        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Console.WriteLine($"[DynamoDB Local] {e.Data}");
            }
        };
        
        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Console.WriteLine($"[DynamoDB Local ERROR] {e.Data}");
            }
        };
        
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        
        return process;
    }
    
    private async Task WaitForDynamoDbLocalAsync()
    {
        var maxAttempts = 30;
        var delayMs = 1000;
        
        Console.WriteLine("[DynamoDB Local] Waiting for service to be ready...");
        
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var client = CreateClient();
                await client.ListTablesAsync();
                Console.WriteLine($"[DynamoDB Local] Ready after {attempt} attempts");
                return;
            }
            catch
            {
                if (attempt == maxAttempts)
                {
                    throw new TimeoutException(
                        $"DynamoDB Local did not become ready after {maxAttempts} attempts");
                }
                
                await Task.Delay(delayMs);
            }
        }
    }
    
    private string FindJavaExecutable()
    {
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var javaExecutable = isWindows ? "java.exe" : "java";
        
        Console.WriteLine($"[DynamoDB Local] Looking for Java on {GetPlatformName()}...");
        
        // Check JAVA_HOME environment variable
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(javaHome))
        {
            var javaPath = Path.Combine(javaHome, "bin", javaExecutable);
            if (File.Exists(javaPath))
            {
                Console.WriteLine($"[DynamoDB Local] Found Java via JAVA_HOME: {javaPath}");
                return javaPath;
            }
        }
        
        // Try to find java in PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            var paths = pathEnv.Split(Path.PathSeparator);
            foreach (var path in paths)
            {
                try
                {
                    var javaPath = Path.Combine(path, javaExecutable);
                    if (File.Exists(javaPath))
                    {
                        Console.WriteLine($"[DynamoDB Local] Found Java in PATH: {javaPath}");
                        return javaPath;
                    }
                }
                catch
                {
                    // Skip invalid paths
                }
            }
        }
        
        // Platform-specific fallback locations
        if (isWindows)
        {
            // Common Windows Java locations
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var commonLocations = new[]
            {
                Path.Combine(programFiles, "Java"),
                Path.Combine(programFiles, "Eclipse Adoptium"),
                Path.Combine(programFiles, "Amazon Corretto")
            };
            
            foreach (var location in commonLocations)
            {
                if (Directory.Exists(location))
                {
                    var javaPath = Directory.GetFiles(location, javaExecutable, SearchOption.AllDirectories)
                        .FirstOrDefault();
                    if (javaPath != null)
                    {
                        Console.WriteLine($"[DynamoDB Local] Found Java at: {javaPath}");
                        return javaPath;
                    }
                }
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS: Check common Homebrew and system locations
            var commonLocations = new[]
            {
                "/opt/homebrew/opt/openjdk/bin/java",
                "/usr/local/opt/openjdk/bin/java",
                "/Library/Java/JavaVirtualMachines"
            };
            
            foreach (var location in commonLocations)
            {
                if (File.Exists(location))
                {
                    Console.WriteLine($"[DynamoDB Local] Found Java at: {location}");
                    return location;
                }
                
                if (Directory.Exists(location))
                {
                    var javaPath = Directory.GetFiles(location, javaExecutable, SearchOption.AllDirectories)
                        .FirstOrDefault();
                    if (javaPath != null)
                    {
                        Console.WriteLine($"[DynamoDB Local] Found Java at: {javaPath}");
                        return javaPath;
                    }
                }
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Linux: Check common package manager locations
            var commonLocations = new[]
            {
                "/usr/lib/jvm",
                "/usr/java"
            };
            
            foreach (var location in commonLocations)
            {
                if (Directory.Exists(location))
                {
                    var javaPath = Directory.GetFiles(location, javaExecutable, SearchOption.AllDirectories)
                        .FirstOrDefault();
                    if (javaPath != null)
                    {
                        Console.WriteLine($"[DynamoDB Local] Found Java at: {javaPath}");
                        return javaPath;
                    }
                }
            }
        }
        
        // Default to just "java" and hope it's in PATH
        Console.WriteLine("[DynamoDB Local] Using 'java' from PATH (assuming it's available)");
        return javaExecutable;
    }
    
    private string GetDynamoDbLocalDirectory()
    {
        // Use environment variable if set, otherwise use default location
        var envPath = Environment.GetEnvironmentVariable("DYNAMODB_LOCAL_PATH");
        if (!string.IsNullOrEmpty(envPath))
        {
            return envPath;
        }
        
        // Default to ./dynamodb-local in the solution root
        var solutionRoot = FindSolutionRoot();
        return Path.Combine(solutionRoot, "dynamodb-local");
    }
    
    private string GetDynamoDbLocalJarPath()
    {
        var dynamoDbDir = GetDynamoDbLocalDirectory();
        return Path.Combine(dynamoDbDir, "DynamoDBLocal.jar");
    }
    
    private string FindSolutionRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        
        while (currentDir != null)
        {
            if (Directory.GetFiles(currentDir, "*.sln").Any())
            {
                return currentDir;
            }
            
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        
        // Fallback to current directory
        return Directory.GetCurrentDirectory();
    }
}
