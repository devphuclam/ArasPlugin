using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Aras;
using IdeaCadConnector.Core.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using IdeaCadConnector.Workspace.NormalizeExport;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public class PdmCloneRoundTripTests
    {
        [Fact]
        public void CloneClient_AcceptsInjectedVaultDownloader()
        {
            var aml = new CloneAmlClient();
            var vault = new CloneVaultClient();
            var options = new ArasClientOptions { BaseUri = new Uri("http://fake/"), Database = "db" };

            using var client = new HttpPdmRepositoryClient(
                options, aml, vault, NullLogger<HttpPdmRepositoryClient>.Instance);

            Assert.NotNull(client);
        }

        [Fact]
        public void SetSession_DoesNotReplaceInjectedVaultDownloader()
        {
            var aml = new CloneAmlClient();
            var vault = new CloneVaultClient();
            var options = new ArasClientOptions { BaseUri = new Uri("http://fake/"), Database = "db" };

            using var client = new HttpPdmRepositoryClient(
                options, aml, vault, NullLogger<HttpPdmRepositoryClient>.Instance);

            client.SetSession("token", "Bearer", "db");

            var vaultField = typeof(HttpPdmRepositoryClient).GetField(
                "_vault",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var fieldValue = vaultField.GetValue(client);

            Assert.Same(vault, fieldValue);
        }

        [Fact]
        public async Task CloneLatestToWorkspaceAsync_BuildsNormalizedPackageFromLiveArasData()
        {
            using var folder = new TempFolder();
            var aml = CloneAmlClient.CreateRoundTrip();
            var vault = new CloneVaultClient(new Dictionary<string, string>
            {
                ["file-root"] = "PDM-STUDYCASE__ROOT__PDM-STUDYCASE.ics",
                ["file-child"] = "PDM-STUDYCASE__A01__BASE.ics"
            });
            var options = new ArasClientOptions { BaseUri = new Uri("http://fake/"), Database = "db" };

            using var client = new HttpPdmRepositoryClient(
                options, aml, vault, NullLogger<HttpPdmRepositoryClient>.Instance);

            var result = await client.CloneLatestToWorkspaceAsync(new PdmCloneRequest
            {
                RepositoryCode = "PDM-STUDYCASE",
                TargetFolder = folder.Path,
                BranchName = "main"
            }, CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(Path.Combine(folder.Path, "cad"), result.ResolvedCadFolder);
            Assert.Equal(Path.Combine(folder.Path, "cad", "PDM-STUDYCASE__ROOT__PDM-STUDYCASE.ics"), result.RootCadFilePath);
            Assert.Equal(2, result.DownloadedCadFileCount);
            Assert.Equal(0, result.PlaceholderDocumentCount);
            Assert.False(Directory.Exists(Path.Combine(folder.Path, "ARAS01")));
            Assert.True(File.Exists(Path.Combine(folder.Path, "pdm-bom-manifest.json")));
            var package = new PdmPackageImportReader().Read(folder.Path);
            Assert.True(package.Validation.IsValid);
            Assert.Equal(2m, package.Manifest.BomV2.Single().Quantity);
            Assert.Equal("ASM", package.Manifest.Definitions.Single(definition => definition.ItemCode == "ROOT").ItemType);
            Assert.Equal("B", package.Manifest.Definitions.Single(definition => definition.ItemCode == "A01").Revision);
            Assert.Contains("select=\"id,related_id,quantity,sort_order\"", aml.LastPartBomAml);
        }

        [Fact]
        public async Task CloneLatestToWorkspaceAsync_FailsAtomicallyWhenRootNativeFileIsMissing()
        {
            using var folder = new TempFolder();
            var aml = CloneAmlClient.CreateRoundTrip();
            aml.SetCadNativeFileId("cad-root", null);
            var vault = CreateRoundTripVault();

            var result = await CloneAsync(folder.Path, aml, vault);

            AssertFailedClone(result, folder.Path, vault);
            Assert.Contains("no native file", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CloneLatestToWorkspaceAsync_FailsAtomicallyWhenChildNativeFileIsMissing()
        {
            using var folder = new TempFolder();
            var aml = CloneAmlClient.CreateRoundTrip();
            aml.SetCadNativeFileId("cad-child", null);
            var vault = CreateRoundTripVault();

            var result = await CloneAsync(folder.Path, aml, vault);

            AssertFailedClone(result, folder.Path, vault);
            Assert.Contains("no native file", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CloneLatestToWorkspaceAsync_FailsAtomicallyWhenVaultDownloadThrows()
        {
            using var folder = new TempFolder();
            var vault = CreateRoundTripVault();
            vault.ThrowForFileId = "file-child";

            var result = await CloneAsync(folder.Path, CloneAmlClient.CreateRoundTrip(), vault);

            AssertFailedClone(result, folder.Path, vault);
            Assert.Contains("vault exploded", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CloneLatestToWorkspaceAsync_FailsAtomicallyForUnsafeReturnedFilename()
        {
            using var folder = new TempFolder();
            var vault = CreateRoundTripVault();
            vault.SetFileName("file-root", "..\\escape.ics");

            var result = await CloneAsync(folder.Path, CloneAmlClient.CreateRoundTrip(), vault);

            AssertFailedClone(result, folder.Path, vault);
            Assert.Contains("outside", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CloneLatestToWorkspaceAsync_FailsAtomicallyForDuplicateNativeFilename()
        {
            using var folder = new TempFolder();
            var aml = CloneAmlClient.CreateRoundTrip();
            aml.SetCadName("cad-child", "PDM-STUDYCASE__ROOT__PDM-STUDYCASE.ics");
            var vault = CreateRoundTripVault();
            vault.SetFileName("file-child", "PDM-STUDYCASE__ROOT__PDM-STUDYCASE.ics");

            var result = await CloneAsync(folder.Path, aml, vault);

            AssertFailedClone(result, folder.Path, vault);
            Assert.Contains("duplicate", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CloneLatestToWorkspaceAsync_FailsAtomicallyForBlankCadName()
        {
            using var folder = new TempFolder();
            var aml = CloneAmlClient.CreateRoundTrip();
            aml.SetCadName("cad-child", null);
            var vault = CreateRoundTripVault();

            var result = await CloneAsync(folder.Path, aml, vault);

            AssertFailedClone(result, folder.Path, vault);
            Assert.Contains("CAD Name", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CloneLatestToWorkspaceAsync_FailsAtomicallyForNonCanonicalCadName()
        {
            using var folder = new TempFolder();
            var aml = CloneAmlClient.CreateRoundTrip();
            aml.SetCadName("cad-child", "Base.ics");
            var vault = CreateRoundTripVault();

            var result = await CloneAsync(folder.Path, aml, vault);

            AssertFailedClone(result, folder.Path, vault);
            Assert.Contains("CAD Name", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CloneLatestToWorkspaceAsync_FailsAtomicallyForInvalidBomCycle()
        {
            using var folder = new TempFolder();
            var aml = CloneAmlClient.CreateRoundTrip();
            aml.AddBomEdge("part-child", "part-root", "1", "20", "bom-child-root");
            var vault = CreateRoundTripVault();

            var result = await CloneAsync(folder.Path, aml, vault);

            AssertFailedClone(result, folder.Path, vault);
            Assert.Contains("cycle", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CloneLatestToWorkspaceAsync_RejectsDestinationWithExistingCadWithoutDeletingIt()
        {
            using var folder = new TempFolder();
            var existingCad = Path.Combine(folder.Path, "cad");
            Directory.CreateDirectory(existingCad);
            var marker = Path.Combine(existingCad, "keep.txt");
            File.WriteAllText(marker, "user data");
            var vault = CreateRoundTripVault();

            var result = await CloneAsync(folder.Path, CloneAmlClient.CreateRoundTrip(), vault);

            Assert.False(result.Success);
            Assert.True(File.Exists(marker));
            Assert.False(File.Exists(Path.Combine(folder.Path, "pdm-bom-manifest.json")));
            Assert.False(Directory.Exists(Path.Combine(folder.Path, ".idea-pdm")));
            Assert.Empty(Directory.GetDirectories(folder.Path, ".pending-*", SearchOption.TopDirectoryOnly));
            Assert.Empty(vault.TargetDirectories);
        }

        [Fact]
        public async Task CloneLatestToWorkspaceAsync_CleansTempTreeWhenBuilderThrowsIoException()
        {
            using var folder = new TempFolder();
            var vault = CreateRoundTripVault();
            vault.CreateManifestDirectoryInTemp = true;

            var result = await CloneAsync(folder.Path, CloneAmlClient.CreateRoundTrip(), vault);

            AssertFailedClone(result, folder.Path, vault);
            Assert.Contains("Clone failed", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CloneLatestToWorkspaceAsync_RejectsCanonicalCadNameMismatchWithoutRenaming()
        {
            using var folder = new TempFolder();
            var aml = CloneAmlClient.CreateRoundTrip();
            aml.SetCadName("cad-child", "PDM-STUDYCASE__B01__OTHER.ics");
            var vault = CreateRoundTripVault();

            var result = await CloneAsync(folder.Path, aml, vault);

            AssertFailedClone(result, folder.Path, vault);
            Assert.Contains("does not match CAD Name", result.ErrorMessage, StringComparison.Ordinal);
        }

        [Fact]
        public async Task CloneLatestToWorkspaceAsync_KeepsRepeatedBomEdgesAndDefaultsInvalidRelationshipValues()
        {
            using var folder = new TempFolder();
            var aml = CloneAmlClient.CreateRoundTrip();
            aml.AddBomEdge("part-root", "part-child", "invalid", null, "bom-root-child-repeat");
            var vault = CreateRoundTripVault();

            var result = await CloneAsync(folder.Path, aml, vault);

            Assert.True(result.Success, result.ErrorMessage);
            var package = new PdmPackageImportReader().Read(folder.Path);
            Assert.Equal(new[] { 2m, 1m }, package.Manifest.BomV2.Select(edge => edge.Quantity));
            Assert.Equal(new[] { 10, 20 }, package.Manifest.Occurrences
                .Where(occurrence => occurrence.ParentOccurrenceId != null)
                .Select(occurrence => occurrence.FindNumber));
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-5")]
        public async Task CloneLatestToWorkspaceAsync_DefaultsNonPositiveBomSortOrder(string sortOrder)
        {
            using var folder = new TempFolder();
            var aml = CloneAmlClient.CreateRoundTrip();
            aml.AddBomEdge("part-root", "part-child", "1", sortOrder, "bom-root-child-nonpositive");
            var vault = CreateRoundTripVault();

            var result = await CloneAsync(folder.Path, aml, vault);

            Assert.True(result.Success, result.ErrorMessage);
            var package = new PdmPackageImportReader().Read(folder.Path);
            Assert.Equal(new[] { 10, 20 }, package.Manifest.Occurrences
                .Where(occurrence => occurrence.ParentOccurrenceId != null)
                .Select(occurrence => occurrence.FindNumber));
        }

        [Fact]
        public async Task CloneLatestToWorkspaceAsync_PreservesLateExternalContentAndRollsBackOwnedEntries()
        {
            using var parent = new TempFolder();
            var targetFolder = Path.Combine(parent.Path, "target");
            Directory.CreateDirectory(targetFolder);
            var lateManifest = Path.Combine(targetFolder, "pdm-bom-manifest.json");
            var vault = CreateRoundTripVault();
            vault.ExternalManifestPath = lateManifest;

            var result = await CloneAsync(targetFolder, CloneAmlClient.CreateRoundTrip(), vault);

            Assert.False(result.Success);
            Assert.Contains("publication failed", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("external", File.ReadAllText(lateManifest));
            Assert.False(Directory.Exists(Path.Combine(targetFolder, "cad")));
            Assert.False(Directory.Exists(Path.Combine(targetFolder, ".idea-pdm")));
            Assert.Empty(Directory.GetDirectories(targetFolder, ".pending-*", SearchOption.TopDirectoryOnly));
            Assert.Empty(Directory.GetDirectories(parent.Path, ".idea-pdm-clone-*", SearchOption.TopDirectoryOnly));
            Assert.All(vault.TargetDirectories, directory =>
                Assert.False(Directory.Exists(Directory.GetParent(directory).FullName)));
        }

        [Fact]
        public async Task CloneLatestToWorkspaceAsync_RollsBackPartialCadWhenRecursiveCopyFails()
        {
            using var folder = new TempFolder();
            var vault = CreateRoundTripVault();
            vault.LockFileIdAgainstCopy = "file-child";

            PdmCloneResult result;
            try
            {
                result = await CloneAsync(folder.Path, CloneAmlClient.CreateRoundTrip(), vault);
            }
            finally
            {
                vault.ReleaseFileLocks();
            }

            AssertFailedClone(result, folder.Path, vault);
            Assert.Contains("publication failed", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ClonePublication_StagesCopiesBeforeAtomicMovesAndRegistersOwnershipAfterMoves()
        {
            var flags = BindingFlags.NonPublic | BindingFlags.Static;
            var publish = typeof(HttpPdmRepositoryClient).GetMethod("PublishClonePackage", flags);
            var stage = typeof(HttpPdmRepositoryClient).GetMethod("StageClonePackage", flags);

            Assert.NotNull(publish);
            Assert.NotNull(stage);

            var stageCalls = GetCalledMethods(stage).ToList();
            var copyDirectoryIndex = stageCalls.FindIndex(method => method.Name == "CopyDirectory");
            var copyManifestIndex = stageCalls.FindIndex(method =>
                method.DeclaringType == typeof(File) && method.Name == nameof(File.Copy));
            Assert.True(copyDirectoryIndex >= 0);
            Assert.True(copyManifestIndex > copyDirectoryIndex);

            var publishCalls = GetCalledMethods(publish).ToList();
            var stageIndex = publishCalls.FindIndex(method => method.Name == "StageClonePackage");
            var directoryMoveIndex = publishCalls.FindIndex(method =>
                method.DeclaringType == typeof(Directory) && method.Name == nameof(Directory.Move));
            var directoryOwnershipIndex = publishCalls.FindIndex(
                directoryMoveIndex + 1,
                method => IsPublishedPathRegistration(method));
            var fileMoveIndex = publishCalls.FindIndex(method =>
                method.DeclaringType == typeof(File) && method.Name == nameof(File.Move));
            var fileOwnershipIndex = publishCalls.FindIndex(
                fileMoveIndex + 1,
                method => IsPublishedPathRegistration(method));

            Assert.True(stageIndex >= 0);
            Assert.True(directoryMoveIndex > stageIndex);
            Assert.True(directoryOwnershipIndex > directoryMoveIndex);
            Assert.True(fileMoveIndex > directoryOwnershipIndex);
            Assert.True(fileOwnershipIndex > fileMoveIndex);
        }

        private static CloneVaultClient CreateRoundTripVault()
        {
            return new CloneVaultClient(new Dictionary<string, string>
            {
                ["file-root"] = "PDM-STUDYCASE__ROOT__PDM-STUDYCASE.ics",
                ["file-child"] = "PDM-STUDYCASE__A01__BASE.ics"
            });
        }

        private static async Task<PdmCloneResult> CloneAsync(
            string targetFolder,
            CloneAmlClient aml,
            CloneVaultClient vault)
        {
            var options = new ArasClientOptions { BaseUri = new Uri("http://fake/"), Database = "db" };
            using var client = new HttpPdmRepositoryClient(
                options, aml, vault, NullLogger<HttpPdmRepositoryClient>.Instance);
            return await client.CloneLatestToWorkspaceAsync(new PdmCloneRequest
            {
                RepositoryCode = "PDM-STUDYCASE",
                TargetFolder = targetFolder,
                BranchName = "main"
            }, CancellationToken.None);
        }

        private static void AssertFailedClone(PdmCloneResult result, string targetFolder, CloneVaultClient vault)
        {
            Assert.False(result.Success);
            Assert.Equal(0, result.PlaceholderDocumentCount);
            Assert.False(File.Exists(Path.Combine(targetFolder, "pdm-bom-manifest.json")));
            Assert.False(Directory.Exists(Path.Combine(targetFolder, "cad")));
            Assert.False(Directory.Exists(Path.Combine(targetFolder, ".idea-pdm")));
            Assert.Empty(Directory.GetDirectories(targetFolder, ".pending-*", SearchOption.TopDirectoryOnly));
            Assert.All(vault.TargetDirectories, directory =>
                Assert.False(Directory.Exists(Directory.GetParent(directory).FullName)));
        }

        private static IReadOnlyList<MethodBase> GetCalledMethods(MethodInfo method)
        {
            var calls = new List<MethodBase>();
            var body = method.GetMethodBody();
            var bytes = body?.GetILAsByteArray() ?? Array.Empty<byte>();
            var opCodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.FieldType == typeof(OpCode))
                .Select(field => (OpCode)field.GetValue(null))
                .ToDictionary(opCode => unchecked((ushort)opCode.Value));

            for (var index = 0; index < bytes.Length;)
            {
                ushort value = bytes[index++];
                if (value == 0xfe)
                    value = (ushort)(0xfe00 | bytes[index++]);
                var opCode = opCodes[value];
                if (opCode.OperandType == OperandType.InlineMethod)
                {
                    var token = BitConverter.ToInt32(bytes, index);
                    calls.Add(method.Module.ResolveMethod(token));
                }
                index += GetOperandSize(opCode.OperandType, bytes, index);
            }

            return calls;
        }

        private static int GetOperandSize(OperandType operandType, byte[] bytes, int operandIndex)
        {
            switch (operandType)
            {
                case OperandType.InlineNone:
                    return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    return 1;
                case OperandType.InlineVar:
                    return 2;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return 8;
                case OperandType.InlineSwitch:
                    return 4 + BitConverter.ToInt32(bytes, operandIndex) * 4;
                default:
                    return 4;
            }
        }

        private static bool IsPublishedPathRegistration(MethodBase method)
        {
            return method.Name == "Add" && method.DeclaringType != null &&
                method.DeclaringType.IsGenericType &&
                method.DeclaringType.GetGenericTypeDefinition() == typeof(List<>);
        }

        private sealed class CloneAmlClient : IArasAmlClient
        {
            private readonly IDictionary<string, JObject> _parts;
            private readonly IDictionary<string, JObject> _cads;
            private readonly IDictionary<string, JArray> _bomByParent;
            private readonly IDictionary<string, JArray> _cadIdsByPart;

            public string LastPartBomAml { get; private set; }

            public CloneAmlClient()
                : this(
                    new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase),
                    new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase),
                    new Dictionary<string, JArray>(StringComparer.OrdinalIgnoreCase),
                    new Dictionary<string, JArray>(StringComparer.OrdinalIgnoreCase))
            {
            }

            private CloneAmlClient(
                IDictionary<string, JObject> parts,
                IDictionary<string, JObject> cads,
                IDictionary<string, JArray> bomByParent,
                IDictionary<string, JArray> cadIdsByPart)
            {
                _parts = parts;
                _cads = cads;
                _bomByParent = bomByParent;
                _cadIdsByPart = cadIdsByPart;
            }

            public static CloneAmlClient CreateRoundTrip()
            {
                var parts = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase)
                {
                    ["part-root"] = Item("part-root", "PDM-STUDYCASE", "Study Case", "A"),
                    ["part-child"] = Item("part-child", "PDM-STUDYCASE-01-01", "Base", "B")
                };
                var cads = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase)
                {
                    ["cad-root"] = Cad("cad-root", "PDM-STUDYCASE-CAD-ASM", "PDM-STUDYCASE__ROOT__PDM-STUDYCASE.ics", "Mechanical/Assembly", "file-root"),
                    ["cad-child"] = Cad("cad-child", "PDM-STUDYCASE-CAD-01-01", "PDM-STUDYCASE__A01__BASE.ics", "Mechanical/Part", "file-child")
                };
                var bom = new Dictionary<string, JArray>(StringComparer.OrdinalIgnoreCase)
                {
                    ["part-root"] = new JArray(new JObject
                    {
                        ["id"] = "bom-root-child",
                        ["related_id"] = "part-child",
                        ["quantity"] = "2",
                        ["sort_order"] = "10"
                    })
                };
                var partCad = new Dictionary<string, JArray>(StringComparer.OrdinalIgnoreCase)
                {
                    ["part-root"] = new JArray(new JObject { ["related_id"] = "cad-root" }),
                    ["part-child"] = new JArray(new JObject { ["related_id"] = "cad-child" })
                };
                return new CloneAmlClient(parts, cads, bom, partCad);
            }

            public void SetCadNativeFileId(string cadId, string nativeFileId)
            {
                _cads[cadId]["native_file"] = nativeFileId == null ? JValue.CreateNull() : new JValue(nativeFileId);
            }

            public void SetCadName(string cadId, string name)
            {
                _cads[cadId]["name"] = name == null ? JValue.CreateNull() : new JValue(name);
            }

            public void AddBomEdge(string parentPartId, string childPartId, string quantity, string sortOrder, string relationshipId)
            {
                if (!_bomByParent.TryGetValue(parentPartId, out var edges))
                {
                    edges = new JArray();
                    _bomByParent[parentPartId] = edges;
                }
                edges.Add(new JObject
                {
                    ["id"] = relationshipId,
                    ["related_id"] = childPartId,
                    ["quantity"] = quantity,
                    ["sort_order"] = sortOrder
                });
            }

            public Task<JObject> ApplyMethodAsync(
                string methodName,
                IDictionary<string, string> parameters,
                CancellationToken ct)
            {
                return Task.FromResult(new JObject());
            }

            public Task<JObject> ApplyItemAsync(
                string itemType,
                string itemId,
                string action,
                string selectFields,
                CancellationToken ct)
            {
                if (string.Equals(itemType, "Part", StringComparison.OrdinalIgnoreCase) && _parts.TryGetValue(itemId, out var part))
                    return Task.FromResult((JObject)part.DeepClone());
                if (string.Equals(itemType, "CAD", StringComparison.OrdinalIgnoreCase) && _cads.TryGetValue(itemId, out var cad))
                    return Task.FromResult((JObject)cad.DeepClone());
                return Task.FromResult(new JObject());
            }

            public Task<JObject> ApplyAmlAsync(
                string amlBody,
                string action,
                string itemType,
                string itemId,
                CancellationToken ct)
            {
                if (string.Equals(itemType, "Part", StringComparison.OrdinalIgnoreCase))
                {
                    var root = _parts.ContainsKey("part-root") ? _parts["part-root"] : null;
                    return Task.FromResult(new JObject { ["Items"] = root == null ? new JArray() : new JArray(root.DeepClone()) });
                }
                if (string.Equals(itemType, "Part BOM", StringComparison.OrdinalIgnoreCase))
                {
                    LastPartBomAml = amlBody;
                    return Task.FromResult(new JObject { ["Items"] = CloneItems(_bomByParent, ExtractSourceId(amlBody)) });
                }
                if (string.Equals(itemType, "Part CAD", StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(new JObject { ["Items"] = CloneItems(_cadIdsByPart, ExtractSourceId(amlBody)) });
                return Task.FromResult(new JObject());
            }

            private static JObject Item(string id, string itemNumber, string name, string revision)
            {
                return new JObject { ["id"] = id, ["item_number"] = itemNumber, ["name"] = name, ["major_rev"] = revision };
            }

            private static JObject Cad(string id, string itemNumber, string name, string classification, string nativeFileId)
            {
                return new JObject
                {
                    ["id"] = id,
                    ["item_number"] = itemNumber,
                    ["name"] = name,
                    ["classification"] = classification,
                    ["authoring_tool"] = "IronCAD",
                    ["native_file"] = nativeFileId
                };
            }

            private static JArray CloneItems(IDictionary<string, JArray> itemsBySource, string sourceId)
            {
                return itemsBySource.TryGetValue(sourceId ?? string.Empty, out var items)
                    ? (JArray)items.DeepClone()
                    : new JArray();
            }

            private static string ExtractSourceId(string aml)
            {
                const string startTag = "<source_id>";
                const string endTag = "</source_id>";
                var start = aml?.IndexOf(startTag, StringComparison.OrdinalIgnoreCase) ?? -1;
                if (start < 0)
                    return null;
                start += startTag.Length;
                var end = aml.IndexOf(endTag, start, StringComparison.OrdinalIgnoreCase);
                return end < 0 ? null : aml.Substring(start, end - start);
            }
        }

        private sealed class CloneVaultClient : IVaultFileClient
        {
            private readonly IDictionary<string, string> _fileNames;
            private readonly IList<FileStream> _fileLocks = new List<FileStream>();

            public CloneVaultClient()
                : this(new Dictionary<string, string>())
            {
            }

            public CloneVaultClient(IDictionary<string, string> fileNames)
            {
                _fileNames = fileNames;
            }

            public string ThrowForFileId { get; set; }
            public string LockFileIdAgainstCopy { get; set; }
            public bool CreateManifestDirectoryInTemp { get; set; }
            public string ExternalManifestPath { get; set; }
            public IList<string> TargetDirectories { get; } = new List<string>();

            public void SetFileName(string fileId, string fileName)
            {
                _fileNames[fileId] = fileName;
            }

            public void ReleaseFileLocks()
            {
                foreach (var fileLock in _fileLocks)
                    fileLock.Dispose();
                _fileLocks.Clear();
            }

            public Task<string> UploadFileAsync(
                string filePath,
                string fileName,
                CancellationToken ct)
            {
                throw new NotSupportedException();
            }

            public Task<string> DownloadFileAsync(
                string fileId,
                string targetDirectory,
                CancellationToken ct)
            {
                TargetDirectories.Add(targetDirectory);
                if (string.Equals(fileId, ThrowForFileId, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Vault exploded while downloading " + fileId + ".");

                var fileName = _fileNames[fileId];
                var path = Path.Combine(targetDirectory, fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, fileId);
                if (string.Equals(fileId, LockFileIdAgainstCopy, StringComparison.OrdinalIgnoreCase))
                    _fileLocks.Add(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Delete));
                if (CreateManifestDirectoryInTemp)
                    Directory.CreateDirectory(Path.Combine(Directory.GetParent(targetDirectory).FullName, "pdm-bom-manifest.json"));
                if (!string.IsNullOrWhiteSpace(ExternalManifestPath))
                    File.WriteAllText(ExternalManifestPath, "external");
                return Task.FromResult(path);
            }
        }

        private sealed class TempFolder : IDisposable
        {
            public TempFolder()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pdm-clone-round-trip-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public void Dispose()
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, true);
            }
        }
    }
}
