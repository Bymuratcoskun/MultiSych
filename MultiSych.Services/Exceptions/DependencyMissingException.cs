using System;

namespace MultiSych.Services.Exceptions
{
    public class DependencyMissingException : Exception
    {
        public string DependencyName { get; }
        public string InstallationCommand { get; }

        public DependencyMissingException(string dependencyName, string installationCommand, string message)
            : base(message)
        {
            DependencyName = dependencyName;
            InstallationCommand = installationCommand;
        }
    }
}
