#nullable disable
namespace DMTQ.Parser.Pattern.Q
{
	public interface IScoreHeader
	{
		ushort InsCount { get; }

		ushort TrackCount { get; }

		ushort Tpm { get; }

		float Tempo { get; }

		uint Tick { get; }

		float PlayTime { get; }

		uint EndTick { get; }

		uint TotalEventCount { get; }

		void CopyFrom(IScoreHeader rhs);
	}
}
