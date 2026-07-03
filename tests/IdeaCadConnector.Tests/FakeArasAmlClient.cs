using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Aras;
using Newtonsoft.Json.Linq;

namespace IdeaCadConnector.Tests
{
    internal sealed class FakeArasAmlClient : IArasAmlClient
    {
        public Queue<JObject> ApplyMethodResults { get; } = new Queue<JObject>();
        public Queue<JObject> ApplyItemResults { get; } = new Queue<JObject>();
        public Queue<JObject> ApplyAmlResults { get; } = new Queue<JObject>();

        public Queue<System.Exception> ApplyMethodExceptions { get; } = new Queue<System.Exception>();
        public Queue<System.Exception> ApplyItemExceptions { get; } = new Queue<System.Exception>();
        public Queue<System.Exception> ApplyAmlExceptions { get; } = new Queue<System.Exception>();

        public IReadOnlyList<string> CalledMethods => _calledMethods;
        private readonly List<string> _calledMethods = new List<string>();

        public Task<JObject> ApplyMethodAsync(string methodName, IDictionary<string, string> parameters, CancellationToken ct)
        {
            _calledMethods.Add($"ApplyMethod:{methodName}");
            if (ApplyMethodExceptions.Count > 0)
                throw ApplyMethodExceptions.Dequeue();
            return Task.FromResult(ApplyMethodResults.Count > 0 ? ApplyMethodResults.Dequeue() : new JObject());
        }

        public Task<JObject> ApplyItemAsync(string itemType, string itemId, string action, string selectFields, CancellationToken ct)
        {
            _calledMethods.Add($"ApplyItem:{itemType}:{action}");
            if (ApplyItemExceptions.Count > 0)
                throw ApplyItemExceptions.Dequeue();
            return Task.FromResult(ApplyItemResults.Count > 0 ? ApplyItemResults.Dequeue() : new JObject());
        }

        public Task<JObject> ApplyAmlAsync(string amlBody, string action, string itemType, string itemId, CancellationToken ct)
        {
            _calledMethods.Add($"ApplyAml:{itemType}:{action}");
            if (ApplyAmlExceptions.Count > 0)
                throw ApplyAmlExceptions.Dequeue();
            return Task.FromResult(ApplyAmlResults.Count > 0 ? ApplyAmlResults.Dequeue() : new JObject());
        }

        public void EnqueueItemFound(string id, string itemNumber)
        {
            var item = new JObject
            {
                ["id"] = id,
                ["item_number"] = itemNumber,
                ["name"] = "Test Part",
                ["state"] = "Released",
                ["config_id"] = "cfg-" + id,
                ["major_rev"] = "A"
            };
            var result = new JObject { ["Items"] = new JArray { item } };
            ApplyAmlResults.Enqueue(result);
        }

        public void EnqueueItemNotFound()
        {
            ApplyAmlResults.Enqueue(new JObject());
        }
    }
}
