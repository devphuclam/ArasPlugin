using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace IdeaCadConnector.Desktop
{
    internal sealed class IronCadExecutableResolver
    {
        private const string ExecutableName = "IRONCAD.exe";
        private readonly Func<IEnumerable<string>> _runningProcessPaths;
        private readonly Func<IEnumerable<string>> _registryPaths;
        private readonly Func<IEnumerable<string>> _installPaths;

        public IronCadExecutableResolver()
            : this(DiscoverRunningProcesses, DiscoverRegistryPaths, DiscoverInstalledVersions)
        {
        }

        internal IronCadExecutableResolver(
            Func<IEnumerable<string>> runningProcessPaths,
            Func<IEnumerable<string>> registryPaths,
            Func<IEnumerable<string>> installPaths)
        {
            _runningProcessPaths = runningProcessPaths ?? throw new ArgumentNullException(nameof(runningProcessPaths));
            _registryPaths = registryPaths ?? throw new ArgumentNullException(nameof(registryPaths));
            _installPaths = installPaths ?? throw new ArgumentNullException(nameof(installPaths));
        }

        public string Resolve(string configuredPath)
        {
            var configured = ValidateCandidate(configuredPath);
            if (configured != null)
                return configured;

            foreach (var provider in new[] { _runningProcessPaths, _registryPaths, _installPaths })
            {
                try
                {
                    foreach (var candidate in provider() ?? Array.Empty<string>())
                    {
                        var resolved = ValidateCandidate(candidate);
                        if (resolved != null)
                            return resolved;
                    }
                }
                catch
                {
                    // Discovery is best-effort. Continue to the next independent source.
                }
            }

            return null;
        }

        internal static IEnumerable<string> DiscoverVersionedInstalls(IEnumerable<string> programFilesRoots)
        {
            var candidates = new List<(Version Version, string Path)>();
            foreach (var root in programFilesRoots ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(root))
                    continue;

                var ironCadRoot = Path.Combine(root, "IronCAD");
                if (!Directory.Exists(ironCadRoot))
                    continue;

                foreach (var versionDirectory in Directory.EnumerateDirectories(ironCadRoot))
                {
                    var versionName = Path.GetFileName(versionDirectory);
                    var majorVersion = 0;
                    if (!Version.TryParse(versionName, out var version) &&
                        (!int.TryParse(versionName, out majorVersion) || majorVersion < 0))
                        continue;
                    if (version == null)
                        version = new Version(majorVersion, 0);

                    var executablePath = Path.Combine(versionDirectory, "bin", ExecutableName);
                    if (File.Exists(executablePath))
                        candidates.Add((version, executablePath));
                }
            }

            return candidates
                .OrderByDescending(candidate => candidate.Version)
                .Select(candidate => candidate.Path)
                .ToArray();
        }

        private static string ValidateCandidate(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) ||
                    !string.Equals(Path.GetFileName(path), ExecutableName, StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(path))
                {
                    return null;
                }

                return Path.GetFullPath(path);
            }
            catch
            {
                return null;
            }
        }

        private static IEnumerable<string> DiscoverRunningProcesses()
        {
            foreach (var process in Process.GetProcessesByName("IRONCAD"))
            {
                using (process)
                {
                    string path = null;
                    try
                    {
                        path = process.MainModule?.FileName;
                    }
                    catch
                    {
                    }

                    if (!string.IsNullOrWhiteSpace(path))
                        yield return path;
                }
            }
        }

        private static IEnumerable<string> DiscoverRegistryPaths()
        {
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                using var appPath = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\IRONCAD.exe");
                var directPath = appPath?.GetValue(null) as string;
                if (!string.IsNullOrWhiteSpace(directPath))
                    yield return directPath;
            }

            using var versions = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\IronCAD\IRONCAD");
            if (versions == null)
                yield break;

            foreach (var versionName in versions.GetSubKeyNames().OrderByDescending(name => name, StringComparer.OrdinalIgnoreCase))
            {
                using var version = versions.OpenSubKey(versionName);
                var installDirectory = version?.GetValue("InstallDir") as string;
                if (!string.IsNullOrWhiteSpace(installDirectory))
                    yield return Path.Combine(installDirectory, "bin", ExecutableName);
            }
        }

        private static IEnumerable<string> DiscoverInstalledVersions()
        {
            return DiscoverVersionedInstalls(new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            });
        }
    }
}
