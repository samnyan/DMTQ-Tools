#nullable disable
using System.IO;

namespace DMTQ.Parser.Pattern.Q
{
	public class BytesTrackHeader : ITrackHeader, ISerializable
	{
		public byte[] name;

		public const int StructSize = 74;

		public ushort Type { get; private set; }

		public uint Length { get; private set; }

		public uint EventCount { get; private set; }

		public string Name
		{
			get
			{
				if (name == null) return string.Empty;
				string value = System.Text.Encoding.ASCII.GetString(name);
				int end = value.IndexOf('\0');
				return (end < 0 ? value : value.Substring(0, end)).ToLowerInvariant();
			}
		}

		public void CopyFrom(ITrackHeader rhs)
		{
			if (rhs == null) return;
			Type = rhs.Type;
			name = System.Text.Encoding.ASCII.GetBytes(rhs.Name ?? string.Empty);
			Length = rhs.Length;
			EventCount = rhs.EventCount;
		}

		public void Deserialize(BinaryReader reader)
		{
			if (reader == null) throw new System.ArgumentNullException("reader");
			Type = reader.ReadUInt16();
			name = reader.ReadBytes(64);
			if (name.Length < 64) System.Array.Resize(ref name, 64);
			Length = reader.ReadUInt32();
			EventCount = reader.ReadUInt32();
		}
	}
}
