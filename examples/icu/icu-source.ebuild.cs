namespace thirdparty.icu;

using ebuild.api;

public class IcuSource : ModuleBase
{
    private static readonly string sourceUrl = "https://github.com/unicode-org/icu/releases/download/release-77-1/icu4c-77_1-src.zip";
    private static readonly string littleEndianDataUrl = "https://github.com/unicode-org/icu/releases/download/release-77-1/icu4c-77_1-data-bin-l.zip";
    private static readonly string bigEndianDataUrl = "https://github.com/unicode-org/icu/releases/download/release-77-1/icu4c-77_1-data-bin-b.zip";
    public IcuSource(ModuleContext context) : base(context)
    {
        this.Type = ModuleType.LibraryLoader;
        this.Name = "icu-source";
        var sourceDir = Path.Join(context.ModuleDirectory.FullName, "source");
        if (!Directory.Exists(sourceDir))
        {
            this.PreBuildSteps.Add(new ModuleBuildStep("Download ICU sources", (workerType, cancellationToken) =>
            {
                if (!ModuleUtilities.GetAndExtractSourceFromArchiveUrl(sourceUrl, sourceDir, "D5CF533CF70CD49044D89EDA3E74880328EB9426E6FD2B3CC8F9A963D2AD480E"))
                    throw new Exception("Couldn't download ICU sources");
                return Task.CompletedTask;
            }));
        }
        var dataDir = Path.Join(context.ModuleDirectory.FullName, "data");
        var hash = string.Empty;
        var fileName = $"icudt77{(BitConverter.IsLittleEndian ? "l" : "b")}.dat";
        var dataUrl = string.Empty;
        if (BitConverter.IsLittleEndian)
        {
            dataDir = Path.Join(dataDir, "little");
            hash = "0913674FF673C585F8BC08370916B6A6CCC30FFB6408A5C1BC3EDBF5A687FD96";
            dataUrl = littleEndianDataUrl;
        }
        else
        {
            dataDir = Path.Join(dataDir, "big");
            hash = "D8BE12E03F782DA350508B15354738ED97A3289008A787B6BD2A85434374BFF4";
            dataUrl = bigEndianDataUrl;
        }
        var filePath = Path.Join(dataDir, fileName);
        var includeDir = Path.Join(context.ModuleDirectory.FullName, "include", "unicode");
        var copyToPath = Path.Join(context.ModuleDirectory.FullName, "source", "icu", "source", "data", "in", fileName);
        // if the data file doesn't exist, download and extract it
        // if the data file exists but the copy to path doesn't, copy it
        if (!File.Exists(filePath) || !File.Exists(copyToPath))
        {
            this.PreBuildSteps.Add(new ModuleBuildStep("Fetch and prepare ICU Data Zip", (workerType, cancellationToken) =>
            {
                if (!ModuleUtilities.GetAndExtractSourceFromArchiveUrl(littleEndianDataUrl, dataDir, hash))
                    throw new Exception("Couldn't download ICU little endian data.");
                // copy the data file to the source/data/in directory
                File.Copy(filePath, copyToPath, true);
                return Task.CompletedTask;
            }));
        }
        if (!File.Exists(includeDir))
        {
            this.PreBuildSteps.Add(new ModuleBuildStep("Prepare ICU include files", (workerType, cancellationToken) =>
            {
                Directory.CreateDirectory(includeDir);
                var copyFromDir = Path.Join(context.ModuleDirectory.FullName, "source", "icu", "source", "common", "unicode");
                foreach (var file in Directory.GetFiles(copyFromDir, "*.h"))
                {
                    var destFile = Path.Join(includeDir, Path.GetFileName(file));
                    File.Copy(file, destFile, true);
                }
                copyFromDir = Path.Join(context.ModuleDirectory.FullName, "source", "icu", "source", "i18n", "unicode");
                foreach (var file in Directory.GetFiles(copyFromDir, "*.h"))
                {
                    var destFile = Path.Join(includeDir, Path.GetFileName(file));
                    File.Copy(file, destFile, true);
                }
                return Task.CompletedTask;
            }));
        }

    }
}