using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ebuild.api;

namespace ebuild.Tests.resources;

public class ZlibEbuild : ModuleBase
{
    private const string ZlibVersion = "1.3.1";
    private const string ZlibUrl = $"https://github.com/madler/zlib/releases/download/v{ZlibVersion}/zlib-{ZlibVersion}.tar.gz";
    private const string ZlibSha256 = "9a93b2b7dfdac77ceba5a558a580e74667dd6fede4585b91eefb60f03b72df23";
    
    public ZlibEbuild(ModuleContext context) : base(context)
    {
        Type = ModuleType.StaticLibrary;
        Name = "zlib";
        
        // Setup should be called in constructor as per README
        // Wait for setup to complete so source files are available
        Setup().GetAwaiter().GetResult();
    }

    public async Task<bool> Setup()
    {
        var downloadPath = GetDownloadPath();
        var extractPath = GetExtractPath();
        
        // Check if already downloaded and extracted
        if (Directory.Exists(extractPath) && File.Exists(Path.Combine(extractPath, "zlib.h")))
        {
            Console.WriteLine("Zlib already downloaded and extracted.");
            SetupSourceFiles(extractPath);
            return true;
        }
        
        // Download zlib if not already present
        if (!File.Exists(downloadPath) || !ValidateChecksum(downloadPath))
        {
            Console.WriteLine($"Downloading zlib {ZlibVersion}...");
            if (!await DownloadZlib(downloadPath))
            {
                Console.WriteLine("Failed to download zlib");
                return false;
            }
        }
        
        // Extract zlib
        Console.WriteLine("Extracting zlib...");
        if (!ExtractZlib(downloadPath, extractPath))
        {
            Console.WriteLine("Failed to extract zlib");
            return false;
        }
        
        // Set up source files
        SetupSourceFiles(extractPath);
        
        Console.WriteLine("Zlib setup completed successfully.");
        return true;
    }
    
    private string GetDownloadPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ebuild", "zlib");
        Directory.CreateDirectory(tempDir);
        return Path.Combine(tempDir, $"zlib-{ZlibVersion}.tar.gz");
    }
    
    private string GetExtractPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ebuild", "zlib");
        return Path.Combine(tempDir, $"zlib-{ZlibVersion}");
    }
    
    private async Task<bool> DownloadZlib(string downloadPath)
    {
        try
        {
            using var client = new HttpClient();
            var response = await client.GetAsync(ZlibUrl);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to download zlib: {response.StatusCode}");
                return false;
            }
            
            var bytes = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(downloadPath, bytes);
            
            return ValidateChecksum(downloadPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error downloading zlib: {ex.Message}");
            return false;
        }
    }
    
    private bool ValidateChecksum(string filePath)
    {
        try
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha256.ComputeHash(stream);
            var hashString = Convert.ToHexString(hash).ToLowerInvariant();
            
            var isValid = hashString == ZlibSha256;
            if (!isValid)
            {
                Console.WriteLine($"Checksum validation failed. Expected: {ZlibSha256}, Got: {hashString}");
            }
            return isValid;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error validating checksum: {ex.Message}");
            return false;
        }
    }
    
    private bool ExtractZlib(string downloadPath, string extractPath)
    {
        try
        {
            if (Directory.Exists(extractPath))
            {
                Directory.Delete(extractPath, true);
            }
            Directory.CreateDirectory(extractPath);
            
            // Extract tar.gz file
            using var originalFileStream = File.OpenRead(downloadPath);
            using var gzipStream = new GZipStream(originalFileStream, CompressionMode.Decompress);
            using var tarStream = new MemoryStream();
            
            gzipStream.CopyTo(tarStream);
            tarStream.Position = 0;
            
            // Simple tar extraction (basic implementation)
            ExtractTar(tarStream, Path.GetDirectoryName(extractPath)!);
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting zlib: {ex.Message}");
            return false;
        }
    }
    
    private void ExtractTar(Stream tarStream, string destinationDirectory)
    {
        var buffer = new byte[100];
        while (true)
        {
            // Read filename
            var read = tarStream.Read(buffer, 0, 100);
            if (read < 100) break;
            
            var name = Encoding.ASCII.GetString(buffer).TrimEnd('\0');
            if (string.IsNullOrEmpty(name)) break;
            
            // Skip to size field (at offset 124)
            tarStream.Position += 24;
            
            // Read size
            tarStream.Read(buffer, 0, 12);
            var sizeString = Encoding.ASCII.GetString(buffer, 0, 12).TrimEnd('\0', ' ');
            var size = Convert.ToInt32(sizeString, 8);
            
            // Skip to data (at offset 512)
            tarStream.Position = ((tarStream.Position - 1) / 512 + 1) * 512;
            
            // Extract file
            var filePath = Path.Combine(destinationDirectory, name);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            
            if (!name.EndsWith('/') && size > 0)
            {
                using var fileStream = File.Create(filePath);
                var remaining = size;
                while (remaining > 0)
                {
                    var toRead = Math.Min(remaining, buffer.Length);
                    var actualRead = tarStream.Read(buffer, 0, toRead);
                    fileStream.Write(buffer, 0, actualRead);
                    remaining -= actualRead;
                }
            }
            
            // Skip to next 512-byte boundary
            var pos = tarStream.Position;
            tarStream.Position = ((pos - 1) / 512 + 1) * 512;
        }
    }
    
    public void SetupSourceFiles(string extractPath)
    {
        // Add main zlib source files
        var sourceFiles = new[]
        {
            "adler32.c", "compress.c", "crc32.c", "deflate.c", "gzclose.c", "gzlib.c",
            "gzread.c", "gzwrite.c", "infback.c", "inffast.c", "inflate.c", "inftrees.c",
            "trees.c", "uncompr.c", "zutil.c"
        };
        
        foreach (var sourceFile in sourceFiles)
        {
            var fullPath = Path.Combine(extractPath, sourceFile);
            if (File.Exists(fullPath))
            {
                SourceFiles.Add(fullPath);
            }
        }
        
        // Add include directories
        Includes.Add(extractPath);
    }
}