using System;

namespace IdeaCadConnector.IronCAD.NormalizeExport
{
    public readonly struct ElementId : IEquatable<ElementId>
    {
        private readonly int _id;

        public ElementId(int id) => _id = id;

        public bool Equals(ElementId other) => _id == other._id;

        public override bool Equals(object obj) => obj is ElementId other && Equals(other);

        public override int GetHashCode() => _id;

        public static bool operator ==(ElementId left, ElementId right) => left.Equals(right);

        public static bool operator !=(ElementId left, ElementId right) => !left.Equals(right);
    }
}
