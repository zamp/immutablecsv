using System;
using System.Collections.Generic;
using System.Linq;

namespace ImmutableData
{
	public class Result
	{
		public bool IsSuccess => _errors.Count == 0;
		
		private readonly List<string> _errors = new List<string>();
		
		public bool ErrorOnTrue(bool value, string error)
		{
			if (string.IsNullOrEmpty(error))
				throw new ArgumentException($"{nameof(error)} is null or empty.");
			if (value)
				_errors.Add(error);
			return value;
		}
		
		public bool ErrorOnFalse(bool value, string error)
		{
			return ErrorOnTrue(!value, error);
		}

		public string GetError()
		{
			return IsSuccess ? string.Empty : _errors.First();
		}
	}
}