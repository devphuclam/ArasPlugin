using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdeaCadConnector.Desktop;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class IronCadExecutableResolverTests
    {
        [Fact]
        public void Resolve_ValidConfiguredPath_HasPrecedence()
        {
            using var files = new ResolverFiles();
            var configured = files.CreateExecutable("configured");
            var running = files.CreateExecutable("running");
            var resolver = CreateResolver(new[] { running });

            Assert.Equal(configured, resolver.Resolve(configured));
        }

        [Fact]
        public void Resolve_InvalidConfiguredPath_FallsBackInProviderOrder()
        {
            using var files = new ResolverFiles();
            var running = files.CreateExecutable("running");
            var registry = files.CreateExecutable("registry");
            var install = files.CreateExecutable("install");
            var resolver = new IronCadExecutableResolver(
                () => new[] { running },
                () => new[] { registry },
                () => new[] { install });

            Assert.Equal(running, resolver.Resolve(@"C:\missing\IRONCAD.exe"));
        }

        [Fact]
        public void Resolve_ProviderThrows_ContinuesToNextProvider()
        {
            using var files = new ResolverFiles();
            var registry = files.CreateExecutable("registry");
            var resolver = new IronCadExecutableResolver(
                () => throw new InvalidOperationException("process access denied"),
                () => new[] { registry },
                () => Array.Empty<string>());

            Assert.Equal(registry, resolver.Resolve(null));
        }

        [Fact]
        public void Resolve_WrongExecutableFilename_IsRejected()
        {
            using var files = new ResolverFiles();
            var wrongName = files.CreateFile("running", "other.exe");
            var resolver = CreateResolver(new[] { wrongName });

            Assert.Null(resolver.Resolve(null));
        }

        [Fact]
        public void DiscoverVersionedInstalls_ReturnsHighestVersionFirst()
        {
            using var files = new ResolverFiles();
            var root = Path.Combine(files.Root, "Program Files");
            var version2024 = files.CreateIronCadInstall(root, "2024");
            var version2025 = files.CreateIronCadInstall(root, "2025");
            files.CreateIronCadInstall(root, "not-a-version");

            var candidates = IronCadExecutableResolver
                .DiscoverVersionedInstalls(new[] { root })
                .ToArray();

            Assert.Equal(new[] { version2025, version2024 }, candidates);
        }

        [Fact]
        public void Resolve_AllProvidersFail_ReturnsNull()
        {
            var resolver = new IronCadExecutableResolver(
                () => throw new InvalidOperationException("running"),
                () => throw new InvalidOperationException("registry"),
                () => throw new InvalidOperationException("install"));

            Assert.Null(resolver.Resolve(null));
        }

        private static IronCadExecutableResolver CreateResolver(IEnumerable<string> running)
        {
            return new IronCadExecutableResolver(
                () => running,
                () => Array.Empty<string>(),
                () => Array.Empty<string>());
        }

        private sealed class ResolverFiles : IDisposable
        {
            public ResolverFiles()
            {
                Root = Path.Combine(Path.GetTempPath(), "IronCadResolverTests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Root);
            }

            public string Root { get; }

            public string CreateExecutable(string directory)
            {
                return CreateFile(directory, "IRONCAD.exe");
            }

            public string CreateFile(string directory, string fileName)
            {
                var path = Path.Combine(Root, directory, fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "test");
                return path;
            }

            public string CreateIronCadInstall(string programFilesRoot, string version)
            {
                var path = Path.Combine(programFilesRoot, "IronCAD", version, "bin", "IRONCAD.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "test");
                return path;
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(Root))
                        Directory.Delete(Root, true);
                }
                catch
                {
                }
            }
        }
    }
}
