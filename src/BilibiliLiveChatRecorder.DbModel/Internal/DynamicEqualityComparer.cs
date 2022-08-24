// ReSharper disable once CheckNamespace

namespace System.Collections.Generic
{
    public sealed class DynamicEqualityComparer<T> : IEqualityComparer<T>
    {
        private readonly Func<T?, T?, bool> _equals = (a, b) => a?.Equals(b) ?? false;
        private readonly Func<T, int> _hash = (a) => a!.GetHashCode();

        public DynamicEqualityComparer(Func<T?, T?, bool>? equals = null, Func<T, int>? hash = null)
        {
            if (equals != null)
            {
                _equals = equals;
            }

            if (hash != null)
            {
                _hash = hash;
            }
        }
        public static IEqualityComparer<T> Create(Func<T?, T?, bool>? equals = null, Func<T, int>? hash = null)
        {
            return new DynamicEqualityComparer<T>(equals, hash);
        }
        public bool Equals(T? x, T? y)
        {
            return _equals(x, y);
        }

        public int GetHashCode(T obj)
        {
            return _hash(obj);
        }
    }
}
