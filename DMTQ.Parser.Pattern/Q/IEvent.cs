#nullable disable
namespace DMTQ.Parser.Pattern.Q
{
	public interface IEvent
	{
		uint Tick { get; }

		byte Type { get; }

		IEventData TypeData { get; }

		void CopyFrom(IEvent rhs);
	}
}
