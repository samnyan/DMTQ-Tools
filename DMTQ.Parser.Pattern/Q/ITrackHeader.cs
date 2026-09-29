#nullable disable
namespace DMTQ.Parser.Pattern.Q
{
	public interface ITrackHeader
	{
		ushort Type { get; }

		string Name { get; }

		uint Length { get; }

		uint EventCount { get; }

		void CopyFrom(ITrackHeader rhs);
	}
}
