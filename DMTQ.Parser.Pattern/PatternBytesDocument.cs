using System.Collections.Generic;

namespace DMTQ.Parser.Pattern
{
	/// <summary>Pattern data decoded according to the game's BytesLoader implementation.</summary>
	public sealed class PatternBytesDocument
	{
		public uint VersionAndSignature { get; set; }
		public PatternBytesHeader Header { get; set; }
		public List<PatternBytesSound> Sounds { get; } = new List<PatternBytesSound>();
		public List<PatternBytesTrack> Tracks { get; } = new List<PatternBytesTrack>();
	}

	public sealed class PatternBytesHeader
	{
		public ushort InstrumentCount { get; set; }
		public ushort TrackCount { get; set; }
		public ushort TicksPerMeasure { get; set; }
		public float Tempo { get; set; }
		public uint Tick { get; set; }
		public float PlayTime { get; set; }
		public uint EndTick { get; set; }
		public uint TotalEventCount { get; set; }
	}

	public sealed class PatternBytesSound
	{
		public ushort Id { get; set; }
		public byte Stream { get; set; }
		public string Name { get; set; }
	}

	public sealed class PatternBytesTrack
	{
		public ushort Type { get; set; }
		public string Name { get; set; }
		public uint Length { get; set; }
		public uint EventCount { get; set; }
		public List<PatternBytesEvent> Events { get; } = new List<PatternBytesEvent>();
	}

	public sealed class PatternBytesEvent
	{
		public uint Tick { get; set; }
		public byte Type { get; set; }
		public byte[] RawData { get; set; }
		public ushort? InstrumentId { get; set; }
		public byte? Velocity { get; set; }
		public byte? Pan { get; set; }
		public byte? Attribute { get; set; }
		public ushort? Duration { get; set; }
		public byte? Volume { get; set; }
		public float? Tempo { get; set; }
		public byte? Beat { get; set; }
		public uint? UnknownData1 { get; set; }
		public uint? UnknownData2 { get; set; }
	}
}
