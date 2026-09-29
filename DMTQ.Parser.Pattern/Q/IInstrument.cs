#nullable disable
namespace DMTQ.Parser.Pattern.Q
{
	public interface IInstrument
	{
		ushort InsNo { get; }

		byte Stream { get; }

		string Name { get; }

		void CopyFrom(IInstrument rhs);
	}
}
