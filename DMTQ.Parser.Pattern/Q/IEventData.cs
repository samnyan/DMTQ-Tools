#nullable disable
namespace DMTQ.Parser.Pattern.Q
{
	public interface IEventData : ISerializable
	{
		void CopyFrom(IEventData rhs);
	}
}
