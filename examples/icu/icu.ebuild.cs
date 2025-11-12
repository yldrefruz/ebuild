/**
*
* This file itself is licensed under the MIT License. But the dependencies have their own licenses:
* 
* Since this module includes ICU and Unicode Data Files,
* After running of the icu-source.ebuild.cs script, compiling and using this module, 
* you agree to the ICU and Unicode Data Files license terms:
* -ICU: https://raw.githubusercontent.com/unicode-org/icu/refs/heads/main/LICENSE
* -Unicode Data Files: https://web.archive.org/web/20251011214721/http://www.unicode.org/copyright.html
*/


namespace thirdparty.icu;

using System.Diagnostics;
using ebuild.api;

public class Icu : ModuleBase
{
    [ModuleOption(ChangesResultBinary = true, Description = "The data mode for the ICU module. Can be 'static', 'shared or 'common'. Default is 'static'.")]
    string DataMode = "static";
    public Icu(ModuleContext context) : base(context)
    {
        if (DataMode is not "static" and not "shared" and not "common")
        {
            throw new Exception("Invalid DataMode for Icu module. Must be either 'static', 'shared' or 'common'.");
        }

        // download the sources.
        {
            // this is not a very good solution since the module instancing params are not passed,
            // but since this only downloads the sources of the ICU library, it's acceptable.
            var psi = new ProcessStartInfo()
            {
                FileName = "dotnet",
                Arguments = $"{typeof(ModuleBase).Assembly.Location.Replace("ebuild.api.dll", "ebuild.dll")} build {Path.Join(context.ModuleDirectory.FullName, "icu-source.ebuild.cs")}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            var proc = Process.Start(psi) ?? throw new Exception("Failed to start process to download ICU sources.");
            var output = proc.StandardOutput.ReadToEnd();
            var error = proc.StandardError.ReadToEnd();
            Console.WriteLine(output);
            if (!string.IsNullOrWhiteSpace(error))
            {
                Console.Error.WriteLine(error);
            }
            proc.WaitForExit();
            if (proc.ExitCode != 0)
            {
                throw new Exception($"Failed to download ICU sources with exit code {proc.ExitCode}.");
            }
        }

        this.Type = ModuleType.StaticLibrary;
        this.Name = "icu";
        this.UseVariants = false;

        var mode = context.SelfReference.GetOutput() switch
        {
            "static" => "static",
            "shared" => "shared",
            _ => throw new Exception($"Invalid output type for Icu module. Must be either static or shared. Input was: {context.SelfReference.GetOutput()}"),
        };


        this.Includes.Public.Add("include");

        this.Dependencies.Public.Add(new ModuleReference(mode, Path.Join(context.ModuleDirectory.FullName, "icu-i18n"), "latest", []));
        this.Dependencies.Public.Add(new ModuleReference(mode, Path.Join(context.ModuleDirectory.FullName, "icu-common"), "latest", []));
        this.Dependencies.Public.Add(new ModuleReference(DataMode, Path.Join(context.ModuleDirectory.FullName, "icu-data"), "latest", []));

    }
}