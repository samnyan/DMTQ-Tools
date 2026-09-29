#nullable disable
using System.IO;

namespace DMTQ.Parser.Pattern.Q
{
	public class BytesFile : ISerializable
	{
		public BytesIns[] Instruments;

		public BytesTrack[] Tracks;

		public uint VerAndSig { get; private set; }

		public BytesHeader Header { get; private set; }

		public void Deserialize(BinaryReader reader)
		{
			if (reader == null) throw new System.ArgumentNullException("reader");
			long origin = reader.BaseStream.Position;
			VerAndSig = reader.ReadUInt32();
			uint headerOffset = reader.ReadUInt32();
			if (headerOffset > reader.BaseStream.Length - origin)
				throw new InvalidDataException("Bytes header offset is outside the pattern buffer");
			reader.BaseStream.Seek(origin + headerOffset, SeekOrigin.Begin);
			Header = new BytesHeader();
			Header.Deserialize(reader);
			reader.BaseStream.Seek(origin + 8L, SeekOrigin.Begin);
			Instruments = new BytesIns[Header.InsCount];
			for (int i = 0; i < Instruments.Length; ++i)
			{
				Instruments[i] = new BytesIns();
				Instruments[i].Deserialize(reader);
			}
			Tracks = new BytesTrack[Header.TrackCount];
			for (int i = 0; i < Tracks.Length; ++i)
			{
				Tracks[i] = new BytesTrack();
				Tracks[i].Deserialize(reader);
			}
		}
	}
}
