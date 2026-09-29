#nullable disable
using System.IO;

namespace DMTQ.Parser.Pattern.Q
{
	public interface ISerializable
	{
		void Deserialize(BinaryReader reader);
	}
}
