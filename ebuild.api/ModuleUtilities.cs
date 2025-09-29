using System;
using System.IO.Compression;
using System.Security.Cryptography;

namespace ebuild.api
{
    public static class ModuleUtilities
    {
        public static string[] GetAllSourceFiles(this ModuleBase module, string root, params string[] extensions)
        {
            List<string> files = [];
            string findAt = Path.GetFullPath(root, module.Context.ModuleDirectory!.FullName);
            foreach (var extension in extensions)
            {
                files.AddRange(Directory.GetFiles(findAt, "*." + extension, SearchOption.AllDirectories));
            }

            return files.ToArray();
        }

        public static bool GetAndExtractSourceFromArchiveUrl(string Url, string ExtractDirectory, string? ExpectedHash)
        {

            using var client = new HttpClient();
            var archiveDir = Path.Join(ExtractDirectory, "source.zip");
            if (File.Exists(archiveDir))
            {
                var content = File.ReadAllBytes(archiveDir);
                var hash = SHA256.HashData(content);
                if (ExpectedHash == null || (ExpectedHash != null && hash.SequenceEqual(Convert.FromHexString(ExpectedHash))))
                {
                    return true;
                }
            }
            var response = client.GetAsync(Url).Result;
            if (response.IsSuccessStatusCode)
            {
                var content = response.Content.ReadAsByteArrayAsync().Result;
                Directory.CreateDirectory(ExtractDirectory);
                File.WriteAllBytes(archiveDir, content);

                var hash = SHA256.HashData(content);
                if (ExpectedHash != null && !hash.SequenceEqual(Convert.FromHexString(ExpectedHash)))
                {
                    return false;
                }
                ZipArchive archive = new(new MemoryStream(content));
                archive.ExtractToDirectory(ExtractDirectory);
                return true;
            }
            return false;
        }

    }
}