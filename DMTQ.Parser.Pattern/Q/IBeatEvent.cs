#nullable disable
namespace DMTQ.Parser.Pattern.Q
{
	public interface IBeatEvent : IEventData, ISerializable
	{
		byte Beat { get; }
	}
}
