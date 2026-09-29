#nullable disable
namespace DMTQ.Parser.Pattern.Q
{
	public interface INoteEvent : IEventData, ISerializable
	{
		ushort Ins { get; }

		byte Vel { get; }

		byte Pan { get; }

		byte Attr { get; }

		ushort Dur { get; }
	}
}
