using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace IdeaCadConnector.Workspace.BomDiagnostic
{
    public enum BomDiagnosticTraversalDecision
    {
        Entered,
        Cycle,
        MaxDepth,
        MaxNodes
    }

    /// <summary>
    /// A provider-neutral guard for graph traversal. Identity is object reference identity,
    /// so repeated occurrences with the same definition may still be visited independently.
    /// </summary>
    public sealed class BomDiagnosticTraversalGuard
    {
        private readonly int _maximumDepth;
        private readonly int _maximumNodes;
        private readonly ISet<object> _active = new HashSet<object>(ReferenceEqualityComparer.Instance);
        private int _enteredNodes;

        public BomDiagnosticTraversalGuard(int maximumDepth, int maximumNodes)
        {
            if (maximumDepth < 0) throw new ArgumentOutOfRangeException(nameof(maximumDepth));
            if (maximumNodes < 1) throw new ArgumentOutOfRangeException(nameof(maximumNodes));
            _maximumDepth = maximumDepth;
            _maximumNodes = maximumNodes;
        }

        public BomDiagnosticTraversalDecision TryEnter(object identity, int depth)
        {
            if (identity == null) return BomDiagnosticTraversalDecision.Cycle;
            if (depth > _maximumDepth) return BomDiagnosticTraversalDecision.MaxDepth;
            if (_active.Contains(identity)) return BomDiagnosticTraversalDecision.Cycle;
            if (_enteredNodes >= _maximumNodes) return BomDiagnosticTraversalDecision.MaxNodes;
            _active.Add(identity);
            _enteredNodes++;
            return BomDiagnosticTraversalDecision.Entered;
        }

        public void Exit(object identity)
        {
            if (identity != null) _active.Remove(identity);
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            public new bool Equals(object x, object y) { return ReferenceEquals(x, y); }
            public int GetHashCode(object obj) { return RuntimeHelpers.GetHashCode(obj); }
        }
    }
}
