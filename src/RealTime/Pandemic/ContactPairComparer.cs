namespace RealTime.Pandemic
{
    using System.Collections.Generic;

    /// <summary>Distributes canonical pair IDs without the default ulong high/low XOR collision pattern.</summary>
    internal sealed class ContactPairComparer : IEqualityComparer<ulong>
    {
        internal static readonly ContactPairComparer Instance = new ContactPairComparer();
        public bool Equals(ulong left, ulong right) => left == right;
        public int GetHashCode(ulong value)
        {
            unchecked
            {
                value ^= value >> 33;
                value *= 0xff51afd7ed558ccdUL;
                value ^= value >> 33;
                value *= 0xc4ceb9fe1a85ec53UL;
                value ^= value >> 33;
                return (int)value;
            }
        }
    }
}
