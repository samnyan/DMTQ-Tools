#nullable disable

namespace DMTQ.Parser.Pattern.Q
{
	public interface IScoreFormatLoader
	{
		bool LoadFromMemory(byte[] buffer, int offset, int length, bool insertStartNote, out ComposedScore outScore);
	}
}
