using NuGet.Configuration;

namespace Contentless;

public class NuGetHelper
{
    private readonly ISettings _settings;
    
    public NuGetHelper(string projectFolder)
    {
        _settings = Settings.LoadDefaultSettings(projectFolder);
    }

    public string PackageFolder => SettingsUtility.GetGlobalPackagesFolder(_settings);
}