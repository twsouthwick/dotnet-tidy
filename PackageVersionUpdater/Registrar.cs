using Microsoft.Build.Locator;

namespace PackageVersionUpdater
{
    public class Registrar
    {
        private readonly VisualStudioInstance _instance;

        public Registrar()
        {
            _instance = MSBuildLocator.RegisterDefaults();
        }

        public bool IsRegistered => MSBuildLocator.IsRegistered;

        public string Instance => _instance.MSBuildPath;
    }
}
