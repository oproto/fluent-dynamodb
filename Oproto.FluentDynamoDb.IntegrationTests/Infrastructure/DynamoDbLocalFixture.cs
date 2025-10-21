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
    
    public IAmazonDynamoDB Client { get; private set; } = null!;
    public string ServiceUrl { get; private set; } = "http://localhost:8000";
    
    public async Task InitializeAsync()
    {
        try
        {
            // 1. Check if DynamoDB Local is already running
            if (await IsDynamoDbLocalRunningAsync())
            {
                Console.WriteLine("[DynamoDB Local] Already running, reusing existing instance");
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
            
            Console.WriteLine("[DynamoDB Local] Started successfully");
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
        
        Console.WriteLine("[DynamoDB Local] Not found, downloading...");
        
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
        
        // Extract tar.gz
        var extractProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "tar",
                Arguments = $"-xzf {tarPath} -C {dynamoDbDir}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        
        extractProcess.Start();
        await extractProcess.WaitForExitAsync();
        
        if (extractProcess.ExitCode != 0)
        {
            var error = await extractProcess.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Failed to extract DynamoDB Local: {error}");
        }
        
        // Clean up tar file
        File.Delete(tarPath);
        
        Console.WriteLine("[DynamoDB Local] Installation complete");
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
        // Try common Java locations
        var javaExecutable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "java.exe" : "java";
        
        // Check JAVA_HOME environment variable
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(javaHome))
        {
            var javaPath = Path.Combine(javaHome, "bin", javaExecutable);
            if (File.Exists(javaPath))
            {
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
                var javaPath = Path.Combine(path, javaExecutable);
                if (File.Exists(javaPath))
                {
                    return javaPath;
                }
            }
        }
        
        // Default to just "java" and hope it's in PATH
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
