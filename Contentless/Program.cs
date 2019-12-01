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
            var importers = GetContentImporters().ToArray();
            Console.WriteLine($"Found possible importer types {string.Join(", ", importers.AsEnumerable())}");
            var processors = GetContentProcessors().ToArray();
            Console.WriteLine($"Found possible processor types {string.Join(", ", processors.AsEnumerable())}");

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

                // override importers
                var over = GetOverrideFor(relative, overrides);
                if (over != null) {
                    // copy special case
                    if (over.Importer == "Copy") {
                        CopyFile(content, relative);
                        changed = true;
                        continue;
                    }

                    importer = Array.Find(importers, i => i.Type.Name == over.Importer);
                    if (importer == null) {
                        Console.WriteLine($"Override importer {over.Importer} not found for file {relative}");
                        continue;
                    }

                    if (over.Processor != null) {
                        processor = Array.Find(processors, p => p == over.Processor);
                        if (processor == null) {
                            Console.WriteLine($"Override processor {over.Processor} not found for file {relative}");
                            continue;
                        }
                    }
                }

                // normal importers
                if (importer == null)
                    importer = GetImporterFor(relative, importers);
                if (importer != null && processor == null)
                    processor = Array.Find(processors, p => p == importer.Importer.DefaultProcessor);

                // no importer found :(
                if (importer == null || processor == null) {
                    Console.WriteLine($"No importer or processor found for file {relative}");
                    continue;
                }

                AddFile(content, relative, importer, processor);
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

        private static IEnumerable<ImporterInfo> GetContentImporters() {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                foreach (var type in assembly.GetTypes()) {
                    var importer = (ContentImporterAttribute) type.GetCustomAttribute(typeof(ContentImporterAttribute), true);
                    if (importer != null)
                        yield return new ImporterInfo(importer, type);
                }
            }
        }

        private static IEnumerable<string> GetContentProcessors() {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                foreach (var type in assembly.GetTypes()) {
                    var processor = type.GetCustomAttribute(typeof(ContentProcessorAttribute), true);
                    if (processor != null)
                        yield return type.Name;
                }
            }
        }

        private static IEnumerable<OverrideInfo> GetOverrides(Dictionary<string, JToken> config) {
            foreach (var entry in config) {
                var regex = MakeFileRegex(entry.Key);
                if (entry.Value.Type == JTokenType.Array) {
                    var arr = (JArray) entry.Value;
                    if (arr.Count != 2) {
                        Console.WriteLine("The override config " + entry.Key + " is invalid: The array needs to contain exactly two entries");
                    } else {
                        yield return new OverrideInfo(regex, arr[0].ToString(), arr[1].ToString());
                    }
                } else if (entry.Value.Type == JTokenType.String) {
                    yield return new OverrideInfo(regex, entry.Value.ToString(), null);
                } else {
                    Console.WriteLine("The override config " + entry.Key + " is invalid: Should be an array or a string");
                }
            }
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

        private static void AddFile(List<string> content, string relative, ImporterInfo importer, string processor) {
            content.Add($"#begin {relative}");
            content.Add($"/importer:{importer}");
            content.Add($"/processor:{processor}");
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