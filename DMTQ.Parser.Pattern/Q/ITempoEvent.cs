#nullable disable
namespace DMTQ.Parser.Pattern.Q
{
	public interface ITempoEvent : IEventData, ISerializable
	{
		float Tempo { get; }
	}
}
