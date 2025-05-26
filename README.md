![The Contentless banner](https://raw.githubusercontent.com/Ellpeck/Contentless/main/Banner.png)

**Contentless** is tool for MonoGame that automatically handles adding assets to the Content Pipeline project, so you don't have to use their interface to add every content file manually.

# How to use
To use Contentless, you first have to add it to your project, either through your NuGet package manager or by adding it to your `.csproj` file as follows. Keep in mind to update the `Version` to the most recent one. You can find the package on the [NuGet website](https://www.nuget.org/packages/Contentless/) as well.
```xml

<ItemGroup>
    <PackageReference Include="Contentless" Version="VERSION" />
</ItemGroup>
```
Next, you need to find the reference to your `Content.mgcb` file in your `.csproj` file or create one if there isn't already one present. The reference's type should be `MonoGameContentReference` so that Contentless can identify it correctly. If you're using the [MonoGame Content Builder](https://www.nuget.org/packages/MonoGame.Content.Builder.Task) alongside Contentless, this setting should already be applied.
```xml

<ItemGroup>
    <MonoGameContentReference Include="Content\Content.mgcb" />
</ItemGroup>
```

Contentless will now automatically add any content files from your `Content` directory and subdirectories to your `Content.mgcb` file if they haven't already been added either manually or by Contentless. No existing items' configurations will be overridden, so you can still use the Content Pipeline tool to modify any settings as well.

# Configuring
If you want to change the way Contentless works, you can use a configuration file. To do so, simply create a file named `Contentless.json` in the same directory as your `Content.mgcb` file. You can use the config to change several options:
```json5
{
    // The list of files that should be excluded. 
    // Can use simple glob-style patterns including "*" to match any number of any character, and "?" to match any single character.
    // Default: ["obj/*", "bin/*"]
    "exclude": [
        "obj/*",
        "bin/*"
    ],
    // If any files that were skipped without errors should be logged (Files that already have entries or files that were ignored)
    // Default: true
    "logSkipped": true,
    // The list of files that should use a different importer or processor than the one that Contentless automatically determined. 
    // Can use simple glob-style patterns including "*" to match any number of any character, and "?" to match any single character.
    // Default: {}
    "overrides": {
        // Example: Make all files matching ".json" use the importer "JsonImporter"
        "*/*.json": {
            "importer": "JsonImporter"
        },
        // Example: Specifying both an importer and a processor
        "*/*.ogg": {
            "importer": "OggImporter",
            "processor": "SongProcessor"
        },
        // Example: Only specifying a processor
        "*/*.wav": {
            "processor": "SoundEffectProcessor"
        },
        // Example: Setting a file to the Copy build action
        "*/*.txt": {
            "copy": true
        },
        // Example: Adding processor parameters for files
        "TestFile.png": {
            "processorParams": {
                "TextureFormat": "Compressed"
            }
        }
    },
    // A set of content pipeline library references that should optionally be added to the content files. 
    // The paths of these references in the content file are automatically changed if they don't match the project's package references.
    // Default: []
    "references": ["MonoGame.Extended.Content.Pipeline"]
}
```
For an example of a config in use, see the [test config](https://github.com/Ellpeck/Contentless/blob/main/Test/Content/Contentless.json).

# What it does
When running Contentless and supplying the location of a MonoGame Content Pipeline project (`Content.mgcb`), it scans all the files in the project's directory as well as its subdirectories. For each file, it checks if the `Content.mgcb` file already contains any references to that file. If no references are found, then a new reference to the file is added.

Contentless figures out which importer and processor to register for any given file by generating a list of all the importers and processors that are available, both inside of MonoGame, and inside of References added to the `Content.mgcb` file. This process is similar to what occurs when adding an existing file through MonoGame's Content Pipeline tool. If Contentless sets the wrong importer or processor for any file, the user can simply open `Content.mgcb` in MonoGame's Content Pipeline tool and edit it manually.

As Contentless never changes any existing content of a `Content.mgcb` file, all changes that are made to it by hand or using the Content Pipeline tool will not be overridden.
