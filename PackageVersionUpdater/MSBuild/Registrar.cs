using Microsoft.Build.Locator;
using Microsoft.Extensions.Logging;

namespace PackageVersionUpdater
{
    public class Registrar
    {
        private readonly VisualStudioInstance _instance;
        private readonly ILogger<Registrar> _logger;

        public Registrar(ILogger<Registrar> logger)
        {
            _instance = MSBuildLocator.RegisterDefaults();
            _logger = logger;

            logger.LogInformation("Registered MSBuild at {Path}", _instance.MSBuildPath);
        }

        public bool IsRegistered => MSBuildLocator.IsRegistered;
    }
}
