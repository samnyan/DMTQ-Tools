#nullable disable
using System;
using System.Collections.Generic;
using System.IO;

namespace DMTQ.Parser.Pattern.Q
{
	public class BytesLoader : IScoreFormatLoader
	{
		public bool LoadFromMemory(byte[] buffer, int offset, int length, bool insertStartNote, out ComposedScore outScore)
		{
			outScore = null;
			if (buffer == null || offset < 0 || length < 0 || offset > buffer.Length - length) return false;
			try
			{
				BytesFile file = new BytesFile();
				using (MemoryStream stream = new MemoryStream(buffer, offset, length, false))
				using (BinaryReader reader = new BinaryReader(stream))
				{
					file.Deserialize(reader);
				}
				if (file.Header == null) return false;
				List<IInstrument> instruments = new List<IInstrument>();
				if (file.Instruments != null)
				{
					for (int i = 0; i < file.Instruments.Length; ++i) instruments.Add(file.Instruments[i]);
				}
				List<ComposedTrack> tracks = new List<ComposedTrack>();
				if (file.Tracks != null)
				{
					for (int i = 0; i < file.Tracks.Length; ++i)
					{
						BytesTrack track = file.Tracks[i];
						List<IEvent> events = new List<IEvent>();
						if (track != null && track.Events != null)
						{
							for (int j = 0; j < track.Events.Length; ++j) events.Add(track.Events[j]);
						}
						tracks.Add(new ComposedTrack(track == null ? null : track.Header, events));
					}
				}
				outScore = new ComposedScore(file.Header, tracks, instruments);
				return true;
			}
			catch (Exception)
			{
				outScore = null;
				return false;
			}
		}
	}
}
