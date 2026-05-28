using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ImmutableData
{
	public class WeightedList<T> : IEnumerable<T>
	{
		private readonly List<T> _items = new();
		private readonly List<double> _weights = new();
		
		private double _totalWeight = 0;
		
		public T PickRandom(Random random)
		{
			if (_items.Count == 0)
				return default;

			var choice = random.NextDouble()  * _totalWeight;
			var sum = 0.0;

			for (var i = 0; i < _items.Count; ++i)
			{
				var obj = _items[i];
				var weight = _weights[i];
				
				for (var j = sum; j < weight + sum; j++)
					if (j >= choice)
						return obj;
				
				sum += weight;
			}

			return _items.First();
		}
		
		public void Add(T item, double weight)
		{
			_items.Add(item);
			_weights.Add(weight);
			_totalWeight = _weights.Sum();
		}

		public void Clear()
		{
			_items.Clear();
			_weights.Clear();
			_totalWeight = 0;
		}

		public bool Remove(T item)
		{
			var index = _items.IndexOf(item);
			if (index != -1)
			{
				_items.RemoveAt(index);
				_weights.RemoveAt(index);
				_totalWeight = _weights.Sum();
				return true;
			}
			return false;
		}

		public double GetWeight(T item)
		{
			var index = _items.IndexOf(item);
			if (index != -1)
			{
				return _weights[index];
			}
			return -1;
		}

		public int Count => _items.Count;
		public IEnumerator<T> GetEnumerator()
		{
			return _items.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}