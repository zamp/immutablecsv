using System;
using System.Collections.Generic;
using System.Linq;

namespace ImmutableData
{
    public class ImmutableDatabase
    {
        private readonly Dictionary<string, Immutable> _database = new();

        private string GetIdentifier(Type type, string name)
        {
            if (type == null)
                throw new ArgumentNullException($"Type is null");

            type = GetRootBaseType(type);

            return $"{type.Name}{name}";
        }

        private static Type GetRootBaseType(Type type)
        {
            var current = type;

            while (current.BaseType != null && current.BaseType != typeof(object) && current.BaseType != typeof(Immutable))
                current = current.BaseType;

            // If the resolved root is marked with [NotImmutableRoot], skip past it
            // and use the original concrete type as the root instead.
            if (current.IsDefined(typeof(NotImmutableRoot), false))
            {
                current = type;
            }

            return current;
        }

        /// <summary>
        /// Add a single model to the database
        /// </summary>
        /// <param name="immutable"></param>
        /// <typeparam name="T"></typeparam>
        /// <exception cref="Exception"></exception>
        internal void Add<T>(T immutable) where T : Immutable
        {
            Add(immutable.GetType(), immutable);
        }

        private void Add(Type type, Immutable immutable)
        {
            if (!_database.TryAdd(GetIdentifier(type, immutable.Name), immutable))
                throw new Exception($"Immutable with name {immutable.Name} already exists.");
        }

        /// <summary>
        /// Gets immutable based on the name
        /// </summary>
        /// <param name="name"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T Get<T>(string name = "") where T : Immutable
        {
            return (T)Get(typeof(T), name);
        }

        /// <summary>
        /// Gets immutable based on name
        /// </summary>
        /// <param name="type"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public Immutable Get(Type type, string name)
        {
            if (_database.TryGetValue(GetIdentifier(type,name), out var immutable))
                return immutable;
            throw new Exception($"No immutable with name {name} of type {type.Name} exists.");
        }

        /// <summary>
        /// Checks if immutable exists
        /// </summary>
        /// <param name="name"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public bool Exists<T>(string name) where T : Immutable
        {
            return Exists(typeof(T), name);
        }

        /// <summary>
        /// Checks if immutable exists
        /// </summary>
        /// <param name="type"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool Exists(Type type, string name)
        {
            return _database.ContainsKey(GetIdentifier(type, name));
        }

        /// <summary>
        /// Gets immutables of type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public IEnumerable<T> GetAll<T>() where T : Immutable
        {
            return _database.Values.OfType<T>();
        }

        public bool Validate(Result result)
        {
            foreach (var immutable in _database)
            {
                if (!immutable.Value.Validate(result))
                {
                    return false;
                }
            }

            return true;
        }

        public void PrePostImport()
        {
            foreach (var immutable in _database)
                immutable.Value.PrePostImport(this);
        }

        public void PostImport()
        {
            foreach (var immutable in _database)
                immutable.Value.PostImport(this);
        }

        public void PostImport<T>(T gameState)
        {
            foreach (var immutable in _database)
                immutable.Value.PostImport(this, gameState);
        }

        public bool TryGet<T>(string name, out T data) where T : Immutable
        {
            if (string.IsNullOrEmpty(name))
            {
                data = null;
                return false;
            }

            data = Get<T>(name);
            return data != null;
        }
    }
}
