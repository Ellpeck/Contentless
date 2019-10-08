using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework.Content.Pipeline;

namespace Contentless {
    public static class Program {

        private static readonly ImporterInfo[] Importers = GetContentImporters().ToArray();
        private static readonly string[] ExcludedFolders = {"bin", "obj"};

        public static void Main(string[] args) {
            var contentFile = new FileInfo(Path.Combine(Environment.CurrentDirectory, args[0]));
            if (!contentFile.Exists) {
                Console.WriteLine($"Unable to find content file {contentFile}");
                return;
            }
            var content = ReadContent(contentFile);

            var changed = false;
            foreach (var file in contentFile.Directory.EnumerateFiles("*", SearchOption.AllDirectories)) {
                // is the file the content file?
                if (file.Name == contentFile.Name)
                    continue;
                var relative = Path.GetRelativePath(contentFile.Directory.FullName, file.FullName).Replace("\\", "/");

                // is the file in an excluded directory?
                var dirName = file.DirectoryName.Replace("\\", "/");
                if (ExcludedFolders.Any(e => dirName.Contains(e))) {
                    continue;
                }

                // is the file already in the content file?
                if (HasEntry(content, relative)) {
                    Console.WriteLine($"Skipping file {relative} as it is already part of the content file");
                    continue;
                }

                var importer = GetImporterFor(relative);
                if (importer == null) {
                    Console.WriteLine($"No importer found for file {relative}, please add the file manually");
                    continue;
                }

                AddFile(content, relative, importer);
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

        private static ImporterInfo GetImporterFor(string file) {
            var extension = Path.GetExtension(file);
            foreach (var importer in Importers) {
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

        private static void AddFile(List<string> content, string relative, ImporterInfo importer) {
            content.Add($"#begin {relative}");
            content.Add($"/importer:{importer.Type.Name}");
            content.Add($"/processor:{importer.Importer.DefaultProcessor}");
            content.Add($"/build:{relative}");
            content.Add("");

            Console.WriteLine($"Adding file {relative} with importer {importer.Type.Name} and processor {importer.Importer.DefaultProcessor}");
        }

        private class ImporterInfo {

            public readonly ContentImporterAttribute Importer;
            public readonly Type Type;

            public ImporterInfo(ContentImporterAttribute importer, Type type) {
                this.Importer = importer;
                this.Type = type;
            }

        }

    }
}