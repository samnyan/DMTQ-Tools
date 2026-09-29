#nullable disable
using System.Collections.Generic;

namespace DMTQ.Parser.Pattern.Q
{
	public class ComposedScore
	{
		public IScoreHeader Header { get; private set; }

		public List<ComposedTrack> Tracks { get; private set; }

		public List<IInstrument> Instruments { get; private set; }

	public ComposedScore(IScoreHeader header, List<ComposedTrack> tracks, List<IInstrument> instruments)
		{
			Header = header;
			Tracks = tracks ?? new List<ComposedTrack>();
			Instruments = instruments ?? new List<IInstrument>();
		}
	}
}
