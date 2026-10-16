#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace ImmutableData
{
	public class Result
	{
		public bool IsSuccess => _errors.Count == 0;

		public IReadOnlyList<string> Errors => _errors;

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

		public void AddError(string error)
		{
			ErrorOnTrue(true, error);
		}

		public string GetError()
		{
			return IsSuccess ? string.Empty : _errors.First();
		}

		/// <summary>All errors, one per line.</summary>
		public string GetErrors()
		{
			return string.Join(Environment.NewLine, _errors);
		}

		/// <summary>Prefixes the errors added since <paramref name="fromIndex"/>, such as with the immutable they belong to.</summary>
		internal void PrefixErrors(int fromIndex, string prefix)
		{
			for (var i = fromIndex; i < _errors.Count; ++i)
				_errors[i] = prefix + _errors[i];
		}
	}
}
