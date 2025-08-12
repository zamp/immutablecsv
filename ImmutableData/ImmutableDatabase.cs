using System;
using System.Collections.Generic;
using System.Linq;

namespace ImmutableData
{
    public class ImmutableDatabase
    {
        private readonly Dictionary<string, Immutable> _database = new();

        /// <summary>
        /// Add a single model to the database
        /// </summary>
        /// <param name="model"></param>
        /// <typeparam name="T"></typeparam>
        /// <exception cref="Exception"></exception>
        internal void Add<T>(T model) where T : Immutable
        {
            Add(model.GetType(), model);
        }

        private void Add(Type type, Immutable immutable)
        {
            if (!_database.TryAdd(immutable.Name, immutable))
                throw new Exception($"Model with name {immutable.Name} already exists.");
        }
        
        /// <summary>
        /// Add a range of models
        /// </summary>
        /// <param name="models"></param>
        internal void AddRange(List<Immutable> models)
        {
            foreach (var model in models)
            {
                Add(model);
            }
        }

        /// <summary>
        /// Gets a model based on the name
        /// </summary>
        /// <param name="name"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T Get<T>(string name) where T : Immutable
        {
            return (T)Get(typeof(T), name);
        }
        
        /// <summary>
        /// Gets model based on name
        /// </summary>
        /// <param name="type"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public Immutable Get(Type type, string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new Exception($"Name is null or empty for type {type.Name}");
            }
            return _database.GetValueOrDefault(name);
        }
        
        /// <summary>
        /// Gets models of type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public IEnumerable<T> GetAll<T>() where T : Immutable
        {
            return _database.Values.Where(x => x is T).Cast<T>();
        }

        public bool Exists(string name)
        {
            return _database.ContainsKey(name);
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