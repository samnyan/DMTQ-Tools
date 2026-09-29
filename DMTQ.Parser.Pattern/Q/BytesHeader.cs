#nullable disable
using System.IO;

namespace DMTQ.Parser.Pattern.Q
{
	public class BytesHeader : IScoreHeader, ISerializable
	{
		public ushort InsCount { get; private set; }

		public ushort TrackCount { get; private set; }

		public ushort Tpm { get; private set; }

		public float Tempo { get; private set; }

		public uint Tick { get; private set; }

		public float PlayTime { get; private set; }

		public uint EndTick { get; private set; }

		public uint TotalEventCount { get; private set; }

	public void CopyFrom(IScoreHeader rhs)
		{
			if (rhs == null) return;
			InsCount = rhs.InsCount;
			TrackCount = rhs.TrackCount;
			Tpm = rhs.Tpm;
			Tempo = rhs.Tempo;
			Tick = rhs.Tick;
			PlayTime = rhs.PlayTime;
			EndTick = rhs.EndTick;
			TotalEventCount = rhs.TotalEventCount;
		}

		public void Deserialize(BinaryReader reader)
		{
			if (reader == null) throw new System.ArgumentNullException("reader");
			InsCount = reader.ReadUInt16();
			TrackCount = reader.ReadUInt16();
			Tpm = reader.ReadUInt16();
			Tempo = reader.ReadSingle();
			Tick = reader.ReadUInt32();
			PlayTime = reader.ReadSingle();
			EndTick = reader.ReadUInt32();
			TotalEventCount = reader.ReadUInt32();
		}
	}
}
