using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace IdeaCadConnector.Aras
{
    internal interface IArasAmlClient
    {
        Task<JObject> ApplyMethodAsync(string methodName, IDictionary<string, string> parameters, CancellationToken ct);

        Task<JObject> ApplyItemAsync(
            string itemType,
            string itemId,
            string action,
            string selectFields,
            CancellationToken ct);

        Task<JObject> ApplyAmlAsync(string amlBody, string action, string itemType, string itemId, CancellationToken ct);
    }
}
