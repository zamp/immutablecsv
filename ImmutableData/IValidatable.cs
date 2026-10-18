namespace ImmutableData
{
	/// <summary>An [Ordered] value that checks itself at load, after its fields are read; errors go into the result.</summary>
	public interface IValidatable
	{
		bool Validate(Result result);
	}
}
