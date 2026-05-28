using System;

namespace ImmutableData
{
	public class CircularReferenceException : Exception
	{
		public CircularReferenceException(string message) : base(message)
		{
		}
	}
        
	public class InvalidReferenceException : Exception
	{
		public InvalidReferenceException(string message) : base(message)
		{
		}
	}

	public class UnhandledTypeException : Exception
	{
		public UnhandledTypeException(string message) : base(message)
		{
		}
	}

	public class RanOutOfResolveStepsException : Exception
	{
		public RanOutOfResolveStepsException(string message) : base(message)
		{
		}
	}
}