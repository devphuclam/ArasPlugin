using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Aras;
using Newtonsoft.Json.Linq;

namespace IdeaCadConnector.Tests
{
    internal sealed class AmlCallRecord
    {
        public int CallOrder { get; set; }
        public string MethodKind { get; set; }
        public string MethodName { get; set; }
        public string AmlBody { get; set; }
        public string Action { get; set; }
        public string ItemType { get; set; }
        public string ItemId { get; set; }
        public string SelectFields { get; set; }
    }

    internal sealed class FakeArasAmlClient : IArasAmlClient
    {
        private int _callCounter;

        public Queue<JObject> ApplyMethodResults { get; } = new Queue<JObject>();
        public Queue<JObject> ApplyItemResults { get; } = new Queue<JObject>();
        public Queue<JObject> ApplyAmlResults { get; } = new Queue<JObject>();

        public Queue<System.Exception> ApplyMethodExceptions { get; } = new Queue<System.Exception>();
        public Queue<System.Exception> ApplyItemExceptions { get; } = new Queue<System.Exception>();
        public Queue<System.Exception> ApplyAmlExceptions { get; } = new Queue<System.Exception>();

        public List<AmlCallRecord> Calls { get; } = new List<AmlCallRecord>();

        public IReadOnlyList<AmlCallRecord> CallLog => Calls;

        public Task<JObject> ApplyMethodAsync(string methodName, IDictionary<string, string> parameters, CancellationToken ct)
        {
            var order = Interlocked.Increment(ref _callCounter);
            CheckCancellation(ct);
            Calls.Add(new AmlCallRecord
            {
                CallOrder = order,
                MethodKind = "ApplyMethod",
                MethodName = methodName,
                Action = methodName,
                ItemType = "Method"
            });
            if (ApplyMethodExceptions.Count > 0)
                throw ApplyMethodExceptions.Dequeue();
            return Task.FromResult(ApplyMethodResults.Count > 0 ? ApplyMethodResults.Dequeue() : new JObject());
        }

        public Task<JObject> ApplyItemAsync(string itemType, string itemId, string action, string selectFields, CancellationToken ct)
        {
            var order = Interlocked.Increment(ref _callCounter);
            CheckCancellation(ct);
            Calls.Add(new AmlCallRecord
            {
                CallOrder = order,
                MethodKind = "ApplyItem",
                ItemType = itemType,
                ItemId = itemId,
                Action = action,
                SelectFields = selectFields
            });
            if (ApplyItemExceptions.Count > 0)
                throw ApplyItemExceptions.Dequeue();
            return Task.FromResult(ApplyItemResults.Count > 0 ? ApplyItemResults.Dequeue() : new JObject());
        }

        public Task<JObject> ApplyAmlAsync(string amlBody, string action, string itemType, string itemId, CancellationToken ct)
        {
            var order = Interlocked.Increment(ref _callCounter);
            CheckCancellation(ct);
            Calls.Add(new AmlCallRecord
            {
                CallOrder = order,
                MethodKind = "ApplyAml",
                AmlBody = amlBody,
                Action = action,
                ItemType = itemType,
                ItemId = itemId
            });
            if (ApplyAmlExceptions.Count > 0)
                throw ApplyAmlExceptions.Dequeue();
            return Task.FromResult(ApplyAmlResults.Count > 0 ? ApplyAmlResults.Dequeue() : new JObject());
        }

        public int CountAmlCalls(string itemType = null, string action = null)
        {
            return Calls.Count(c =>
                (itemType == null || string.Equals(c.ItemType, itemType, System.StringComparison.OrdinalIgnoreCase)) &&
                (action == null || string.Equals(c.Action, action, System.StringComparison.OrdinalIgnoreCase)));
        }

        public bool AnyAmlContains(string fragment)
        {
            return Calls.Any(c =>
                (c.AmlBody != null && c.AmlBody.IndexOf(fragment, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                (c.ItemId != null && c.ItemId.IndexOf(fragment, System.StringComparison.OrdinalIgnoreCase) >= 0));
        }

        public AmlCallRecord LastAmlCall()
        {
            return Calls.Count > 0 ? Calls[Calls.Count - 1] : null;
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

        public void EnqueueItemFoundWithState(string id, string itemNumber, string state)
        {
            var item = new JObject
            {
                ["id"] = id,
                ["item_number"] = itemNumber,
                ["name"] = "Test Part",
                ["state"] = state,
                ["config_id"] = "cfg-" + id,
                ["major_rev"] = "A"
            };
            var result = new JObject { ["Items"] = new JArray { item } };
            ApplyAmlResults.Enqueue(result);
        }

        public void EnqueueItemFoundWithConfigRev(string id, string itemNumber, string configId, string majorRev)
        {
            var item = new JObject
            {
                ["id"] = id,
                ["item_number"] = itemNumber,
                ["name"] = "Test Part",
                ["state"] = "Released",
                ["config_id"] = configId,
                ["major_rev"] = majorRev
            };
            var result = new JObject { ["Items"] = new JArray { item } };
            ApplyAmlResults.Enqueue(result);
        }

        public void EnqueueItemNotFound()
        {
            ApplyAmlResults.Enqueue(new JObject());
        }

        public void EnqueueAmlResult(JObject result)
        {
            ApplyAmlResults.Enqueue(result ?? new JObject());
        }

        private static void CheckCancellation(CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
                throw new System.OperationCanceledException(ct);
        }
    }
}
