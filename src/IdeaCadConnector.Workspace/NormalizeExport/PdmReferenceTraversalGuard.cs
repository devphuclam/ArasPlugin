using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace IdeaCadConnector.Workspace.NormalizeExport
{
    public sealed class PdmReferenceTraversalGuard<T> where T : class
    {
        private readonly PdmNormalizationLimits _limits;
        private readonly ISet<T> _active = new HashSet<T>(ReferenceComparer.Instance);
        private int _nodeCount;

        public PdmReferenceTraversalGuard(PdmNormalizationLimits limits)
        {
            _limits = limits ?? throw new ArgumentNullException(nameof(limits));
        }

        public void Enter(T node, int depth)
        {
            if (node == null) throw new PdmNormalizeExportException("ROUND_TRIP_VALIDATION_FAILED", "Không thể đọc occurrence rỗng.");
            if (depth > _limits.MaxDepth || _nodeCount >= _limits.MaxNodeCount)
                throw new PdmNormalizeExportException("REFERENCE_TRAVERSAL_LIMIT_EXCEEDED", "Cây liên kết ngoài vượt giới hạn an toàn.");
            if (!_active.Add(node))
                throw new PdmNormalizeExportException("REFERENCE_TRAVERSAL_CYCLE", "Phát hiện vòng lặp trong cây liên kết ngoài.");
            _nodeCount++;
        }

        public void Exit(T node) { if (node != null) _active.Remove(node); }

        private sealed class ReferenceComparer : IEqualityComparer<T>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public bool Equals(T x, T y) { return object.ReferenceEquals(x, y); }
            public int GetHashCode(T obj) { return RuntimeHelpers.GetHashCode(obj); }
        }
    }
}
