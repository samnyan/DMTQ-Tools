#nullable disable
namespace DMTQ.Parser.Pattern.Q
{
	public interface IVolumeEvent : IEventData, ISerializable
	{
		byte Volume { get; }
	}
}
