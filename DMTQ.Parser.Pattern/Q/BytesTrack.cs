#nullable disable
using System.IO;

namespace DMTQ.Parser.Pattern.Q
{
	public class BytesTrack : ISerializable
	{
		public BytesTrackHeader Header;

		public BytesEvent[] Events;

		public void Deserialize(BinaryReader reader)
		{
			if (reader == null) throw new System.ArgumentNullException("reader");
			Header = new BytesTrackHeader();
			Header.Deserialize(reader);
			Events = new BytesEvent[Header.EventCount];
			for (int i = 0; i < Events.Length; ++i)
			{
				Events[i] = new BytesEvent();
				Events[i].Deserialize(reader);
			}
		}
	}
}
