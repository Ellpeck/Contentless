# Contentless
A commandline tool for MonoGame that automatically handles adding assets to the Content Pipeline project so you don't have to use their horrible interface.

# How to use
Clone this repository or download its `Build` folder which contains a build of Contentless. Then, add Contentless to your build process by adding the following task to your `.csproj` file. Note that you might have to change the paths to fit your project's setup.
```xml
<Target Name="BeforeBuild">
    <Exec Command="..\..\Contentless\Build\Contentless.exe Content/Content.mgcb" />
</Target>
```
Contentless will now automatically add any content files from your `Content` directory and subdirectories (excluding `bin` and `obj`) to your `Content.mgcb` file if they haven't already been added either manually or by Contentless. No existing items' configurations will be overridden, so you can still use the Content Pipeline tool to modify any settings as well.