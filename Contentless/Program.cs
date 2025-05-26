using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Construction;
using Microsoft.Xna.Framework.Content.Pipeline;
using Newtonsoft.Json;
using NuGet.Configuration;

namespace Contentless;

public static class Program {

    public static int Main(string[] args) {
        if (args.Length < 1) {
            Console.Error.WriteLine("Please specify the location of the content file you want to use");
            return 1;
        }

        // find global packages folder and installed packages if we supplied a project file
        string packagesFolder = null;
        Dictionary<string, string> installedPackages = null;
        if (args.Length >= 2) {
            var projectPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, args[1]));
            if (!File.Exists(projectPath) || Path.GetExtension(projectPath) != ".csproj") {
                Console.Error.WriteLine($"Unable to find valid project file at {projectPath}");
                return 1;
            }

            installedPackages = Program.ExtractPackagesFromProject(projectPath);
            Console.WriteLine($"Found package dependencies {string.Join(", ", installedPackages.Select(kv => $"{kv.Key}@{kv.Value}"))} in project {projectPath}");

            var settings = Settings.LoadDefaultSettings(Path.GetDirectoryName(projectPath));
            packagesFolder = SettingsUtility.GetGlobalPackagesFolder(settings);
            Console.WriteLine($"Using global packages folder {packagesFolder}");
        }

        // iterate all project files (semicolon-delimited) and process them
        var contentFiles = args[0].Split(';').Select(p => Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, p.Trim()))).Distinct();
        foreach (var file in contentFiles) {
            var ret = Program.ProcessContentFile(new FileInfo(file), packagesFolder, installedPackages);
            if (ret != 0)
                return ret;
        }

        return 0;
    }

    private static int ProcessContentFile(FileInfo contentFile, string packagesFolder, Dictionary<string, string> installedPackages) {
        if (!contentFile.Exists || contentFile.Extension != ".mgcb") {
            Console.Error.WriteLine($"Unable to find valid content file at {contentFile}");
            return 1;
        }

        Console.WriteLine($"Using content file {contentFile}");
        var content = Program.ReadContent(contentFile);

        // load config
        var config = new Config();
        var configFile = new FileInfo(Path.Combine(contentFile.DirectoryName!, "Contentless.json"));
        if (configFile.Exists) {
            using var stream = configFile.OpenText();
            try {
                config = JsonConvert.DeserializeObject<Config>(stream.ReadToEnd());
                Console.WriteLine($"Using config from {configFile}");
            } catch (Exception e) {
                Console.Error.WriteLine($"Error loading config from {configFile}: {e}");
                return 1;
            }
        } else {
            Console.WriteLine("Using default config");
        }
        if (config.References.Length > 0 && packagesFolder == null)
            Console.Error.WriteLine("The config file contains references, but no valid project data was found. Please specify the location of the project file you want to use for gathering references as the second argument.");

        var changed = false;

        // load refereces and replace reference paths if they're mismatched
        var referencesNotInContentFile = config.References.ToHashSet();
        const string referenceHeader = "/reference:";
        for (var i = 0; i < content.Count; i++) {
            var line = content[i];
            if (!line.StartsWith(referenceHeader))
                continue;
            var reference = line[referenceHeader.Length..];
            var libraryName = Path.GetFileName(reference)[..^4];

            if (referencesNotInContentFile.Remove(libraryName)) {
                if (installedPackages.TryGetValue(libraryName, out var version)) {
                    var fullLibraryPath = Program.CalculateFullPathToLibrary(packagesFolder, libraryName, version);
                    if (reference != fullLibraryPath) {
                        Console.WriteLine($"Changing reference from {reference} to {fullLibraryPath}");
                        reference = fullLibraryPath;
                        content[i] = referenceHeader + fullLibraryPath;
                        changed = true;
                    } else {
                        if (config.LogSkipped)
                            Console.WriteLine($"Skipping reference replacement for {fullLibraryPath} which already matched");
                    }
                } else {
                    Console.Error.WriteLine($"Unable to find existing reference {libraryName} in project file");
                }
            }

            var refPath = Path.GetFullPath(Path.Combine(contentFile.DirectoryName, reference));
            Program.SafeAssemblyLoad(refPath);
            Console.WriteLine($"Using reference {refPath}");
        }

        // find the line we want to start adding new references from
        var lastReferenceLine = 0;
        for (var i = 0; i < content.Count; i++) {
            var line = content[i];
            if (line.StartsWith(referenceHeader)) {
                lastReferenceLine = i + 1;
            } else if (line.StartsWith("/importer:") || line.StartsWith("/processor:") || line.StartsWith("/build:") || line.Contains("-- Content --")) {
                if (lastReferenceLine == 0)
                    lastReferenceLine = i;
                break;
            }
        }
        // add references that aren't in the content file yet
        foreach (var reference in referencesNotInContentFile) {
            if (installedPackages.TryGetValue(reference, out var version)) {
                try {
                    var path = Program.CalculateFullPathToLibrary(packagesFolder, reference, version);
                    content.Insert(lastReferenceLine++, referenceHeader + path);
                    changed = true;
                    Program.SafeAssemblyLoad(path);
                    Console.WriteLine($"Adding reference {path}");
                } catch (Exception e) {
                    Console.Error.WriteLine($"Error adding reference {reference}: {e}");
                }
            } else {
                Console.Error.WriteLine($"Unable to find configured reference {reference} in project file");
            }
        }

        // load content importers
        var (importers, processors) = Program.GetContentData();
        Console.WriteLine($"Found possible importer types {string.Join(", ", importers)}");
        Console.WriteLine($"Found possible processor types {string.Join(", ", processors)}");

        var overrides = Program.GetOverrides(config.Overrides).ToArray();
        foreach (var file in contentFile.Directory.EnumerateFiles("*", SearchOption.AllDirectories)) {
            // is the file the content or config file?
            if (file.Name == contentFile.Name || file.Name == configFile.Name)
                continue;
            var relative = Path.GetRelativePath(contentFile.DirectoryName, file.FullName).Replace('\\', '/');

            // is the file in an excluded directory?
            if (config.ExcludedFiles.Any(e => FileSystemName.MatchesSimpleExpression(e, relative))) {
                if (config.LogSkipped)
                    Console.WriteLine($"Skipping excluded file {relative}");
                continue;
            }

            // is the file already in the content file?
            if (Program.HasEntry(content, relative)) {
                if (config.LogSkipped)
                    Console.WriteLine($"Skipping file {relative} as it is already part of the content file");
                continue;
            }

            ImporterInfo importer = null;
            string processor = null;
            Dictionary<string, string> processorParams = null;

            // override importers
            var over = Program.GetOverrideFor(relative, overrides);
            if (over != null) {
                processorParams = over.Override.ProcessorParams;

                // copy special case
                if (over.Override.Copy) {
                    Program.CopyFile(content, relative);
                    changed = true;
                    continue;
                }

                if (!string.IsNullOrEmpty(over.Override.Importer)) {
                    importer = importers.Find(i => i.Type.Name == over.Override.Importer);
                    if (importer == null) {
                        Console.Error.WriteLine($"Override importer {over.Override.Importer} not found for file {relative}");
                        continue;
                    }
                }

                if (!string.IsNullOrEmpty(over.Override.Processor)) {
                    processor = processors.Find(p => p == over.Override.Processor);
                    if (processor == null) {
                        Console.Error.WriteLine($"Override processor {over.Override.Processor} not found for file {relative}");
                        continue;
                    }
                }
            }

            // normal importers
            importer ??= Program.GetImporterFor(relative, importers);
            if (importer != null && processor == null)
                processor = processors.Find(p => p == importer.Importer.DefaultProcessor);

            // no importer found :(
            if (importer == null || processor == null) {
                Console.Error.WriteLine($"No importer or processor found for file {relative}");
                continue;
            }

            Program.AddFile(content, relative, importer.Type.Name, processor, processorParams);
            changed = true;
        }

        if (changed) {
            contentFile.Delete();
            using (var stream = contentFile.CreateText()) {
                foreach (var line in content)
                    stream.WriteLine(line);
            }
            Console.WriteLine("Wrote changes to content file");
        }
        Console.WriteLine("Done");
        return 0;
    }

    private static void SafeAssemblyLoad(string refPath) {
        try {
            Assembly.LoadFrom(refPath);
        } catch (Exception e) {
            Console.Error.WriteLine($"Error loading reference {refPath}: {e}");
        }
    }

    private static Dictionary<string, string> ExtractPackagesFromProject(string csprojPath) {
        var ret = new Dictionary<string, string>();
        var projectRootElement = ProjectRootElement.Open(csprojPath);
        foreach (var property in projectRootElement.AllChildren.Where(x => x.ElementName == "PackageReference").Select(x => x as ProjectItemElement)) {
            var libraryName = property.Include;
            if (property.Children.FirstOrDefault(x => x.ElementName == "Version") is not ProjectMetadataElement versionElement)
                continue;
            ret.Add(libraryName, versionElement.Value);
        }
        return ret;
    }

    private static string CalculateFullPathToLibrary(string packageFolder, string libraryName, string referencesVersion) {
        return Path.Combine(packageFolder, libraryName.ToLowerInvariant(), referencesVersion, "tools", libraryName + ".dll").Replace('\\', '/');
    }

    private static (List<ImporterInfo>, List<string>) GetContentData() {
        var importers = new List<ImporterInfo>();
        var processors = new List<string>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
            try {
                if (assembly.IsDynamic)
                    continue;
                foreach (var type in assembly.GetExportedTypes()) {
                    var importer = (ContentImporterAttribute) type.GetCustomAttribute(typeof(ContentImporterAttribute), true);
                    if (importer != null)
                        importers.Add(new ImporterInfo(importer, type));
                    var processor = type.GetCustomAttribute(typeof(ContentProcessorAttribute), true);
                    if (processor != null)
                        processors.Add(type.Name);
                }
            } catch (Exception e) {
                Console.Error.WriteLine($"Error gathering types in reference {assembly}: {e}");
            }
        }
        return (importers, processors);
    }

    private static IEnumerable<OverrideInfo> GetOverrides(Dictionary<string, Override> config) {
        foreach (var entry in config)
            yield return new OverrideInfo(entry.Key, entry.Value);
    }

    private static OverrideInfo GetOverrideFor(string file, IEnumerable<OverrideInfo> overrides) {
        foreach (var over in overrides) {
            if (FileSystemName.MatchesSimpleExpression(over.Expression, file))
                return over;
        }
        return null;
    }

    private static ImporterInfo GetImporterFor(string file, IEnumerable<ImporterInfo> importers) {
        var extension = Path.GetExtension(file);
        foreach (var importer in importers) {
            if (importer.Importer.FileExtensions.Contains(extension))
                return importer;
        }
        return null;
    }

    private static bool HasEntry(IEnumerable<string> content, string relativeFile) {
        foreach (var line in content) {
            if (line.StartsWith($"#begin {relativeFile}"))
                return true;
        }
        return false;
    }

    private static List<string> ReadContent(FileInfo file) {
        var content = new List<string>();
        using var stream = file.OpenText();
        while (stream.ReadLine() is {} line)
            content.Add(line);
        return content;
    }

    private static void AddFile(ICollection<string> content, string relative, string importer, string processor, Dictionary<string, string> processorParams) {
        content.Add($"#begin {relative}");
        content.Add($"/importer:{importer}");
        content.Add($"/processor:{processor}");
        if (processorParams != null) {
            foreach (var kv in processorParams)
                content.Add($"/processorParam:{kv.Key}={kv.Value}");
        }
        content.Add($"/build:{relative}");
        content.Add("");
        Console.WriteLine($"Adding file {relative} with importer {importer} and processor {processor}");
    }

    private static void CopyFile(ICollection<string> content, string relative) {
        content.Add($"#begin {relative}");
        content.Add($"/copy:{relative}");
        content.Add("");
        Console.WriteLine($"Adding file {relative} with the Copy build action");
    }

}
