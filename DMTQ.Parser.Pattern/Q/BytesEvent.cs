#nullable disable
using System.IO;

namespace DMTQ.Parser.Pattern.Q
{
	public class BytesEvent : IEvent, ISerializable
	{
		public class UnknownTypeData : IEventData, ISerializable
		{
			public uint Data1 { get; private set; }

			public uint Data2 { get; private set; }

			public void CopyFrom(IEventData rhs)
			{
				UnknownTypeData value = rhs as UnknownTypeData;
				if (value == null) return;
				Data1 = value.Data1;
				Data2 = value.Data2;
			}

			public void Deserialize(BinaryReader reader)
			{
				if (reader == null) throw new System.ArgumentNullException("reader");
				Data1 = reader.ReadUInt32();
				Data2 = reader.ReadUInt32();
			}
		}

		public class NoteTypeData : INoteEvent, IEventData, ISerializable
		{
			public ushort Ins { get; private set; }

			public byte Vel { get; private set; }

			public byte Pan { get; private set; }

			public byte Attr { get; private set; }

			public ushort Dur { get; private set; }

			public void CopyFrom(IEventData rhs)
			{
				NoteTypeData value = rhs as NoteTypeData;
				if (value == null) return;
				Ins = value.Ins;
				Vel = value.Vel;
				Pan = value.Pan;
				Attr = value.Attr;
				Dur = value.Dur;
			}

			public void Deserialize(BinaryReader reader)
			{
				if (reader == null) throw new System.ArgumentNullException("reader");
				Ins = reader.ReadUInt16();
				Vel = reader.ReadByte();
				Pan = reader.ReadByte();
				Attr = reader.ReadByte();
				Dur = reader.ReadUInt16();
				reader.ReadByte();
			}
		}

		public class VolumeTypeData : IVolumeEvent, IEventData, ISerializable
		{
			public byte Volume { get; private set; }

			public void CopyFrom(IEventData rhs)
			{
				VolumeTypeData value = rhs as VolumeTypeData;
				if (value != null) Volume = value.Volume;
			}

			public void Deserialize(BinaryReader reader)
			{
				if (reader == null) throw new System.ArgumentNullException("reader");
				Volume = reader.ReadByte();
				reader.ReadBytes(7);
			}
		}

		public class TempoTypeData : ITempoEvent, IEventData, ISerializable
		{
			public float Tempo { get; private set; }

			public void CopyFrom(IEventData rhs)
			{
				TempoTypeData value = rhs as TempoTypeData;
				if (value != null) Tempo = value.Tempo;
			}

			public void Deserialize(BinaryReader reader)
			{
				if (reader == null) throw new System.ArgumentNullException("reader");
				Tempo = reader.ReadSingle();
				reader.ReadBytes(4);
			}
		}

		public class BeatTypeData : IBeatEvent, IEventData, ISerializable
		{
			public byte Beat { get; private set; }

			public void CopyFrom(IEventData rhs)
			{
				BeatTypeData value = rhs as BeatTypeData;
				if (value != null) Beat = value.Beat;
			}

			public void Deserialize(BinaryReader reader)
			{
				if (reader == null) throw new System.ArgumentNullException("reader");
				Beat = reader.ReadByte();
				reader.ReadBytes(7);
			}
		}

		private const int TypeDataSize = 8;

		public const int StructSize = 13;

		public uint Tick { get; private set; }

		public byte Type { get; private set; }

		public IEventData TypeData { get; private set; }

		public byte[] RawData { get; private set; }

		private void DetermineRawEvent()
		{
			switch (Type)
			{
				case 1: TypeData = new NoteTypeData(); break;
				case 2: TypeData = new VolumeTypeData(); break;
				case 3: TypeData = new TempoTypeData(); break;
				case 4: TypeData = new BeatTypeData(); break;
				default: TypeData = new UnknownTypeData(); break;
			}
		}

		public void CopyFrom(IEvent rhs)
		{
			if (rhs == null) return;
			Tick = rhs.Tick;
			Type = rhs.Type;
			DetermineRawEvent();
			if (TypeData != null) TypeData.CopyFrom(rhs.TypeData);
			BytesEvent source = rhs as BytesEvent;
			RawData = source == null || source.RawData == null ? null : (byte[])source.RawData.Clone();
		}

		public void Deserialize(BinaryReader reader)
		{
			if (reader == null) throw new System.ArgumentNullException("reader");
			Tick = reader.ReadUInt32();
			Type = reader.ReadByte();
			DetermineRawEvent();
			long dataOffset = reader.BaseStream.Position;
			TypeData.Deserialize(reader);
			long eventEnd = reader.BaseStream.Position;
			reader.BaseStream.Position = dataOffset;
			RawData = reader.ReadBytes(TypeDataSize);
			reader.BaseStream.Position = eventEnd;
		}
	}
}
