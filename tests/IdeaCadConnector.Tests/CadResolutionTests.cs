using System;
using IdeaCadConnector.Aras;
using Xunit;
using Newtonsoft.Json.Linq;

namespace IdeaCadConnector.Tests
{
    public sealed class CadResolutionTests
    {
        // ---- PartSearchClient.ReadIronCadPartCad tests -----------------------

        [Fact]
        public void ReadIronCadPartCad_PlaceholderAndRealIronCad_SelectsRealWithNativeFile()
        {
            var partEntry = new JObject
            {
                ["Part_CAD"] = new JArray
                {
                    RelationshipWithCad(new JObject
                    {
                        ["id"] = "PLACEHOLDER-1",
                        ["classification"] = "Mechanical/Part",
                        ["authoring_tool"] = "IronCAD",
                        ["item_number"] = "DEMO-A02-ICS",
                        ["native_file"] = null
                    }),
                    RelationshipWithCad(new JObject
                    {
                        ["id"] = "REAL-CAD-1",
                        ["classification"] = "Mechanical/Assembly",
                        ["authoring_tool"] = "IronCAD",
                        ["item_number"] = "DEMO-CAD-A02",
                        ["native_file"] = "FILE-123"
                    })
                }
            };

            var result = PartSearchClient.ReadIronCadPartCad(partEntry);

            Assert.NotNull(result);
            Assert.Equal("REAL-CAD-1", result.Id);
            Assert.True(result.HasNativeFile);
            Assert.Equal("Mechanical/Assembly", result.Classification);
            Assert.Equal("DEMO-CAD-A02", result.CadNumber);
        }

        [Fact]
        public void ReadIronCadPartCad_OnlyPlaceholderWithoutNativeFile_ReturnsNull()
        {
            var partEntry = new JObject
            {
                ["Part_CAD"] = new JArray
                {
                    RelationshipWithCad(new JObject
                    {
                        ["id"] = "PLACEHOLDER-1",
                        ["classification"] = "Mechanical/Part",
                        ["authoring_tool"] = "IronCAD",
                        ["item_number"] = "DEMO-A02-ICS",
                        ["native_file"] = null
                    })
                }
            };

            var result = PartSearchClient.ReadIronCadPartCad(partEntry);

            Assert.Null(result);
        }

        [Fact]
        public void ReadIronCadPartCad_InventorAndIronCadWithNativeFile_SelectsIronCad()
        {
            var partEntry = new JObject
            {
                ["Part_CAD"] = new JArray
                {
                    RelationshipWithCad(new JObject
                    {
                        ["id"] = "INVENTOR-1",
                        ["classification"] = "Mechanical/Part",
                        ["authoring_tool"] = "Inventor",
                        ["item_number"] = "DEMO-INV-A02",
                        ["native_file"] = "INV-FILE-456"
                    }),
                    RelationshipWithCad(new JObject
                    {
                        ["id"] = "IRONCAD-1",
                        ["classification"] = "Mechanical/Assembly",
                        ["authoring_tool"] = "IronCAD",
                        ["item_number"] = "DEMO-CAD-A02",
                        ["native_file"] = "ICS-FILE-789"
                    })
                }
            };

            var result = PartSearchClient.ReadIronCadPartCad(partEntry);

            Assert.NotNull(result);
            Assert.Equal("IRONCAD-1", result.Id);
            Assert.Equal("Mechanical/Assembly", result.Classification);
            Assert.True(result.HasNativeFile);
        }

        [Fact]
        public void ReadIronCadPartCad_MechanicalAssemblyClassification_Accepted()
        {
            var partEntry = new JObject
            {
                ["Part_CAD"] = new JArray
                {
                    RelationshipWithCad(new JObject
                    {
                        ["id"] = "CAD-ASM-1",
                        ["classification"] = "Mechanical/Assembly",
                        ["authoring_tool"] = "IronCAD",
                        ["item_number"] = "DEMO-CAD-ASM",
                        ["native_file"] = "FILE-ASM-1"
                    })
                }
            };

            var result = PartSearchClient.ReadIronCadPartCad(partEntry);

            Assert.NotNull(result);
            Assert.Equal("CAD-ASM-1", result.Id);
            Assert.True(result.HasNativeFile);
            Assert.Equal("Mechanical/Assembly", result.Classification);
        }

        [Fact]
        public void ReadIronCadPartCad_CaseInsensitiveAuthoringTool_Accepted()
        {
            var partEntry = new JObject
            {
                ["Part_CAD"] = new JArray
                {
                    RelationshipWithCad(new JObject
                    {
                        ["id"] = "CAD-CI-1",
                        ["classification"] = "Mechanical/Part",
                        ["authoring_tool"] = "ironcad",
                        ["item_number"] = "DEMO-CAD-CI",
                        ["native_file"] = "FILE-CI-1"
                    })
                }
            };

            var result = PartSearchClient.ReadIronCadPartCad(partEntry);

            Assert.NotNull(result);
            Assert.Equal("CAD-CI-1", result.Id);
            Assert.True(result.HasNativeFile);
        }

        [Fact]
        public void ReadIronCadPartCad_NoValidIronCadWithNativeFile_ReturnsNull()
        {
            var partEntry = new JObject
            {
                ["Part_CAD"] = new JArray
                {
                    RelationshipWithCad(new JObject
                    {
                        ["id"] = "CAD-INV-1",
                        ["classification"] = "Mechanical/Part",
                        ["authoring_tool"] = "Inventor",
                        ["item_number"] = "DEMO-INV",
                        ["native_file"] = "INV-FILE-1"
                    }),
                    RelationshipWithCad(new JObject
                    {
                        ["id"] = "CAD-NO-FILE",
                        ["classification"] = "Mechanical/Part",
                        ["authoring_tool"] = "IronCAD",
                        ["item_number"] = "DEMO-NO-FILE",
                        ["native_file"] = null
                    })
                }
            };

            var result = PartSearchClient.ReadIronCadPartCad(partEntry);

            Assert.Null(result);
        }

        [Fact]
        public void ReadIronCadPartCad_NoPartCadRelationship_ReturnsNull()
        {
            var partEntry = new JObject
            {
                ["id"] = "PART-1",
                ["item_number"] = "DEMO-PART"
            };

            var result = PartSearchClient.ReadIronCadPartCad(partEntry);

            Assert.Null(result);
        }

        // ---- CadResolutionHelper tests (covers HttpArasCadClient path) -------

        [Theory]
        [InlineData("IronCAD", "FILE-1", true)]
        [InlineData("ironcad", "FILE-1", true)]
        [InlineData("IRONCAD", "FILE-1", true)]
        [InlineData("IronCAD", null, false)]
        [InlineData("IronCAD", "", false)]
        [InlineData("IronCAD", "   ", false)]
        [InlineData("Inventor", "FILE-1", false)]
        [InlineData(null, "FILE-1", false)]
        [InlineData("", "FILE-1", false)]
        [InlineData(null, null, false)]
        public void IsIronCadWithValidNativeFile_VariousCombinations(string authoringTool, string nativeFile, bool expected)
        {
            var result = CadResolutionHelper.IsIronCadWithValidNativeFile(authoringTool, nativeFile);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ResolvePrimaryIronCadPartCad_PlaceholderAndReal_SelectsRealWithNativeFile()
        {
            var cadToken = new JObject
            {
                ["id"] = "CAD-REAL-1",
                ["classification"] = "Mechanical/Assembly",
                ["authoring_tool"] = "IronCAD",
                ["item_number"] = "DEMO-CAD-A02",
                ["major_rev"] = "A",
                ["state"] = "Thiet ke chi tiet",
                ["generation"] = 1,
                ["native_file"] = "FILE-REAL-1"
            };

            Assert.True(CadResolutionHelper.IsIronCadWithValidNativeFile(
                cadToken["authoring_tool"]?.Value<string>(),
                cadToken["native_file"]?.Value<string>()));
        }

        [Fact]
        public void ResolvePrimaryIronCadPartCad_PlaceholderWithoutNativeFile_Rejected()
        {
            var cadToken = new JObject
            {
                ["id"] = "CAD-PLACEHOLDER",
                ["classification"] = "Mechanical/Part",
                ["authoring_tool"] = "IronCAD",
                ["item_number"] = "DEMO-A02-ICS",
                ["major_rev"] = "A",
                ["state"] = "Khoi tao",
                ["generation"] = 1,
                ["native_file"] = null
            };

            Assert.False(CadResolutionHelper.IsIronCadWithValidNativeFile(
                cadToken["authoring_tool"]?.Value<string>(),
                cadToken["native_file"]?.Value<string>()));
        }

        [Fact]
        public void ResolvePrimaryIronCadPartCad_MechanicalAssemblyClassification_Accepted()
        {
            var cadToken = new JObject
            {
                ["id"] = "CAD-ASM-1",
                ["classification"] = "Mechanical/Assembly",
                ["authoring_tool"] = "IronCAD",
                ["item_number"] = "DEMO-CAD-ASM",
                ["native_file"] = "FILE-ASM-1"
            };

            Assert.True(CadResolutionHelper.IsIronCadWithValidNativeFile(
                cadToken["authoring_tool"]?.Value<string>(),
                cadToken["native_file"]?.Value<string>()));
        }

        [Fact]
        public void ResolvePrimaryIronCadPartCad_CaseInsensitiveAuthoringTool_Accepted()
        {
            var cadToken = new JObject
            {
                ["id"] = "CAD-CI-1",
                ["classification"] = "Mechanical/Part",
                ["authoring_tool"] = "ironcad",
                ["item_number"] = "DEMO-CAD-CI",
                ["native_file"] = "FILE-CI-1"
            };

            Assert.True(CadResolutionHelper.IsIronCadWithValidNativeFile(
                cadToken["authoring_tool"]?.Value<string>(),
                cadToken["native_file"]?.Value<string>()));
        }

        [Fact]
        public void ResolvePrimaryIronCadPartCad_InventorWithNativeFile_Rejected()
        {
            var cadToken = new JObject
            {
                ["id"] = "CAD-INV-1",
                ["classification"] = "Mechanical/Part",
                ["authoring_tool"] = "Inventor",
                ["item_number"] = "DEMO-INV",
                ["native_file"] = "FILE-INV-1"
            };

            Assert.False(CadResolutionHelper.IsIronCadWithValidNativeFile(
                cadToken["authoring_tool"]?.Value<string>(),
                cadToken["native_file"]?.Value<string>()));
        }

        // ---- helpers ---------------------------------------------------------

        private static JObject RelationshipWithCad(JObject cadProperties)
        {
            return new JObject
            {
                ["related_id"] = cadProperties
            };
        }
    }
}
