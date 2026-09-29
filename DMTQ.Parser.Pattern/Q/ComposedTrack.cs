#nullable disable
using System.Collections.Generic;

namespace DMTQ.Parser.Pattern.Q
{
	public class ComposedTrack
	{
		public ITrackHeader Header { get; private set; }

		public List<IEvent> Events { get; private set; }

		public ComposedTrack(ITrackHeader header, List<IEvent> events)
		{
			Header = header;
			Events = events ?? new List<IEvent>();
		}
	}
}
