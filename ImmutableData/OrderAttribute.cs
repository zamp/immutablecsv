using System;

namespace ImmutableData
{
	[AttributeUsage(AttributeTargets.Property)]
	public sealed class OrderAttribute : Attribute
	{
		public int Order { get; }
		
		public OrderAttribute(int order = 0)
		{
			Order = order;
		}
	}
	
	[AttributeUsage(AttributeTargets.Class)]
	public sealed class OrderedAttribute : Attribute
	{
	}
}