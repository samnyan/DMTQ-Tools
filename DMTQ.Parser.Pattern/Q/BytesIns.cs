#nullable disable
using System.IO;

namespace DMTQ.Parser.Pattern.Q
{
	public class BytesIns : IInstrument, ISerializable
	{
		public byte[] name;

		public const int StructSize = 67;

		public ushort InsNo { get; private set; }

		public byte Stream { get; private set; }

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

		public void CopyFrom(IInstrument rhs)
		{
			if (rhs == null) return;
			InsNo = rhs.InsNo;
			Stream = rhs.Stream;
			name = System.Text.Encoding.ASCII.GetBytes(rhs.Name ?? string.Empty);
		}

		public void Deserialize(BinaryReader reader)
		{
			if (reader == null) throw new System.ArgumentNullException("reader");
			InsNo = reader.ReadUInt16();
			Stream = reader.ReadByte();
			name = reader.ReadBytes(64);
			if (name.Length < 64) System.Array.Resize(ref name, 64);
		}
	}
}
