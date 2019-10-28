# Contentless
A commandline tool for MonoGame that automatically handles adding assets to the Content Pipeline project so you don't have to use their interface to add every content file manually.

# How to use
Clone this repository or download a build from the [Releases](https://github.com/Ellpeck/Contentless/releases) tab. 

Next, add Contentless to your build process by adding the following task to your `.csproj` file. Note that you might have to change the paths to fit your project's setup.
```xml
<Target Name="Contentless" BeforeTargets="BeforeBuild">
    <Exec Command="..\..\Contentless\Build\Contentless.exe Content/Content.mgcb" />
</Target>
```
Contentless will now automatically add any content files from your `Content` directory and subdirectories to your `Content.mgcb` file if they haven't already been added either manually or by Contentless. No existing items' configurations will be overridden, so you can still use the Content Pipeline tool to modify any settings as well.

# Configuring
To add a configuration file to Contentless, simply create a file named `Contentless.json` in the same directory as your `Content.mgcb` file. You can use the config to change several options. For reference, here is a configuration file with the default values that are used if no config is supplied:
```json
{
    // The list of files that should be excluded. Can use regex
    "exclude": [
        "obj/",
        "bin/"
    ],
    // If any files that were skipped without errors should be logged
    // (Files that already have entries or files that were ignored)
    "logSkipped": true
}
```

# What it does
When running Contentless and supplying the location of a MonoGame Content Pipeline project (`Content.mgcb`), it scans all of the files in the project's directory as well as its subdirectories. For each file, it checks if the `Content.mgcb` file already contains any references to that file. If no references are found, then a new reference to the file is added. 

Contentless figures out which importer and processor to register for any given file by generating a list of all of the importers and processors that are available, both inside of MonoGame, and inside of References added to the `Content.mgcb` file. This process is similar to what occurs when adding an existing file through MonoGame's Content Pipeline tool. If Contentless sets the wrong importer or processor for any file, the user can simply open `Content.mgcb` in MonoGame's Content Pipeline tool and edit it manually. 

As Contentless never changes any existing content of a `Content.mgcb` file, all changes that are made to it by hand or using the Content Pipeline tool will not be overridden.