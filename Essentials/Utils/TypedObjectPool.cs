using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Essentials.Utils
{

    public class TypedObjectPool
    {
        private readonly Dictionary<Type, IList> _store;
        private readonly int _defaultCapacity;

        public TypedObjectPool() : this(0)
        { }

        public TypedObjectPool(int capacity)
        {
            _defaultCapacity = capacity;
            _store = new Dictionary<Type, IList>();
        }

        public bool Allocate<T>(out T result) where T : class, new()
        {
            if (_store.TryGetValue(typeof(T), out IList store))
            {
                if (store.Count > 0)
                {
                    result = (T)store[store.Count - 1];
                    store.RemoveAt(store.Count - 1);
                    return true;
                }
            }

            result = default(T);
            return false;
        }

        public T AllocateOrCreate<T>() where T : class, new()
        {
            if (_store.TryGetValue(typeof(T), out IList store))
            {
                if (store.Count > 0)
                {
                    var result = (T)store[store.Count - 1];
                    store.RemoveAt(store.Count - 1);
                    return result;
                }
                else
                {
                    return new T();
                }
            }
            else
            {
                store = new List<T>(_defaultCapacity);
                _store.Add(typeof(T), store);
                return new T();
            }
        }

        public bool Deallocate<T>(T element) where T : class, new()
        {
            if (!_store.TryGetValue(typeof(T), out IList store))
                return false;

            store.Add(element);
            return true;
        }

        public bool DeallocateCollection<T>(IEnumerable<T> source) where T : class, new()
        {
            if (!_store.TryGetValue(typeof(T), out IList store))
                return false;

            foreach (var e in source)
                store.Add(e);

            return true;
        }

        public bool DeallocateAndClear<T>(IList<T> source) where T : class, new()
        {
            if (!_store.TryGetValue(typeof(T), out IList store))
                return false;

            foreach (var e in source)
                store.Add(e);

            source.Clear();

            return true;
        }
    }
}
