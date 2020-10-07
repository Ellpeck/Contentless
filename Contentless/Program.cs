using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework.Content.Pipeline;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Contentless {
    public static class Program {

        public static void Main(string[] args) {
            if (args.Length != 1) {
                Console.WriteLine("Please specify the location of the content file you want to use");
                return;
            }

            var contentFile = new FileInfo(Path.Combine(Environment.CurrentDirectory, args[0]));
            if (!contentFile.Exists) {
                Console.WriteLine($"Unable to find content file {contentFile}");
                return;
            }

            Console.WriteLine($"Using content file {contentFile}");
            var content = ReadContent(contentFile);

            // load config
            var config = new Config();
            var configFile = new FileInfo(Path.Combine(contentFile.DirectoryName, "Contentless.json"));
            if (configFile.Exists) {
                using (var stream = configFile.OpenText()) {
                    try {
                        config = JsonConvert.DeserializeObject<Config>(stream.ReadToEnd());
                        Console.WriteLine($"Using config from {configFile}");
                    } catch (Exception e) {
                        Console.WriteLine($"Error loading config from {configFile}: {e}");
                    }
                }
            } else {
                Console.WriteLine("Using default config");
            }
            var excluded = config.ExcludedFiles.Select(MakeFileRegex).ToArray();
            var overrides = GetOverrides(config.Overrides).ToArray();

            // load any references to be able to include custom content types as well
            foreach (var line in content) {
                if (!line.StartsWith("/reference:"))
                    continue;
                var reference = line.Substring(11);
                var refPath = Path.Combine(contentFile.DirectoryName, reference);
                try {
                    Assembly.LoadFrom(refPath);
                    Console.WriteLine($"Using reference {refPath}");
                } catch (Exception e) {
                    Console.WriteLine($"Error loading reference {refPath}: {e}");
                }
            }

            // load content importers
            var (importers, processors) = GetContentData();
            Console.WriteLine($"Found possible importer types {string.Join(", ", importers)}");
            Console.WriteLine($"Found possible processor types {string.Join(", ", processors)}");

            var changed = false;
            foreach (var file in contentFile.Directory.EnumerateFiles("*", SearchOption.AllDirectories)) {
                // is the file the content or config file?
                if (file.Name == contentFile.Name || file.Name == configFile.Name)
                    continue;
                var relative = GetRelativePath(contentFile.DirectoryName, file.FullName).Replace("\\", "/");

                // is the file in an excluded directory?
                if (excluded.Any(e => e.IsMatch(relative))) {
                    if (config.LogSkipped)
                        Console.WriteLine($"Skipping excluded file {relative}");
                    continue;
                }

                // is the file already in the content file?
                if (HasEntry(content, relative)) {
                    if (config.LogSkipped)
                        Console.WriteLine($"Skipping file {relative} as it is already part of the content file");
                    continue;
                }

                ImporterInfo importer = null;
                string processor = null;
                Dictionary<string, string> processorParams = null;

                // override importers
                var over = GetOverrideFor(relative, overrides);
                if (over != null) {
                    processorParams = over.Override.ProcessorParams;

                    // copy special case
                    if (over.Override.Copy) {
                        CopyFile(content, relative);
                        changed = true;
                        continue;
                    }

                    if (!string.IsNullOrEmpty(over.Override.Importer)) {
                        importer = importers.Find(i => i.Type.Name == over.Override.Importer);
                        if (importer == null) {
                            Console.WriteLine($"Override importer {over.Override.Importer} not found for file {relative}");
                            continue;
                        }
                    }

                    if (!string.IsNullOrEmpty(over.Override.Processor)) {
                        processor = processors.Find(p => p == over.Override.Processor);
                        if (processor == null) {
                            Console.WriteLine($"Override processor {over.Override.Processor} not found for file {relative}");
                            continue;
                        }
                    }
                }

                // normal importers
                if (importer == null)
                    importer = GetImporterFor(relative, importers);
                if (importer != null && processor == null)
                    processor = processors.Find(p => p == importer.Importer.DefaultProcessor);

                // no importer found :(
                if (importer == null || processor == null) {
                    Console.WriteLine($"No importer or processor found for file {relative}");
                    continue;
                }

                AddFile(content, relative, importer.Type.Name, processor, processorParams);
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
            Console.Write("Done");
        }

        private static (List<ImporterInfo>, List<string>) GetContentData() {
            var importers = new List<ImporterInfo>();
            var processors = new List<string>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                try {
                    foreach (var type in assembly.GetTypes()) {
                        var importer = (ContentImporterAttribute) type.GetCustomAttribute(typeof(ContentImporterAttribute), true);
                        if (importer != null)
                            importers.Add(new ImporterInfo(importer, type));
                        var processor = type.GetCustomAttribute(typeof(ContentProcessorAttribute), true);
                        if (processor != null)
                            processors.Add(type.Name);
                    }
                } catch {
                    // ignored
                }
            }
            return (importers, processors);
        }

        private static IEnumerable<OverrideInfo> GetOverrides(Dictionary<string, Override> config) {
            foreach (var entry in config)
                yield return new OverrideInfo(MakeFileRegex(entry.Key), entry.Value);
        }

        private static OverrideInfo GetOverrideFor(string file, IEnumerable<OverrideInfo> overrides) {
            foreach (var over in overrides) {
                if (over.Regex.IsMatch(file))
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
            using (var stream = file.OpenText()) {
                string line;
                while ((line = stream.ReadLine()) != null) {
                    content.Add(line);
                }
            }
            return content;
        }

        private static void AddFile(List<string> content, string relative, string importer, string processor, Dictionary<string, string> processorParams) {
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

        private static void CopyFile(List<string> content, string relative) {
            content.Add($"#begin {relative}");
            content.Add($"/copy:{relative}");
            content.Add("");
            Console.WriteLine($"Adding file {relative} with the Copy build action");
        }

        private static string GetRelativePath(string relativeTo, string path) {
            if (!relativeTo.EndsWith(Path.DirectorySeparatorChar.ToString()))
                relativeTo += Path.DirectorySeparatorChar;
            return path.Replace(relativeTo, "");
        }

        private static Regex MakeFileRegex(string s) {
            return new Regex(s.Replace(".", "[.]").Replace("*", ".*").Replace("?", "."));
        }

    }
}