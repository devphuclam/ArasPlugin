using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace IdeaCadConnector.Workspace.NormalizeExport
{
    public sealed class PdmPackageManifestWriter
    {
        public string Serialize(PdmPackageManifest manifest)
        {
            return JsonConvert.SerializeObject(manifest, Formatting.Indented,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });
        }
    }
}
