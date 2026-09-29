using System;
using System.IO;
using DMTQ.Parser.Pattern.Q;

namespace DMTQ.Parser.Pattern
{
	/// <summary>Reads Bytes files using the same loader logic used by the game.</summary>
	public sealed class PatternBytesParser
	{
		public PatternBytesDocument Parse(byte[] data)
		{
			if (data == null) throw new ArgumentNullException("data");
			if (data.Length == 0) throw new InvalidDataException("Pattern data is empty.");

			var loader = new BytesLoader();
			ComposedScore score;
			if (!loader.LoadFromMemory(data, 0, data.Length, false, out score) || score == null || score.Header == null)
				throw new InvalidDataException("The Unity Bytes loader rejected this pattern.");

			var sourceHeader = score.Header;
			var document = new PatternBytesDocument
			{
				VersionAndSignature = ReadUInt32LittleEndian(data, 0),
				Header = new PatternBytesHeader
				{
					InstrumentCount = sourceHeader.InsCount,
					TrackCount = sourceHeader.TrackCount,
					TicksPerMeasure = sourceHeader.Tpm,
					Tempo = sourceHeader.Tempo,
					Tick = sourceHeader.Tick,
					PlayTime = sourceHeader.PlayTime,
					EndTick = sourceHeader.EndTick,
					TotalEventCount = sourceHeader.TotalEventCount
				}
			};

			foreach (var sourceSound in score.Instruments)
			{
				document.Sounds.Add(new PatternBytesSound
				{
					Id = sourceSound.InsNo,
					Stream = sourceSound.Stream,
					Name = sourceSound.Name
				});
			}

			foreach (var sourceTrack in score.Tracks)
			{
				var sourceTrackHeader = sourceTrack.Header;
				var track = new PatternBytesTrack
				{
					Type = sourceTrackHeader == null ? (ushort)0 : sourceTrackHeader.Type,
					Name = sourceTrackHeader == null ? string.Empty : sourceTrackHeader.Name,
					Length = sourceTrackHeader == null ? 0 : sourceTrackHeader.Length,
					EventCount = sourceTrackHeader == null ? 0 : sourceTrackHeader.EventCount
				};
				foreach (var sourceEvent in sourceTrack.Events)
					track.Events.Add(MapEvent(sourceEvent));
				document.Tracks.Add(track);
			}
			return document;
		}

		private static PatternBytesEvent MapEvent(IEvent sourceEvent)
		{
			var bytesEvent = (BytesEvent)sourceEvent;
			var result = new PatternBytesEvent
			{
				Tick = sourceEvent.Tick,
				Type = sourceEvent.Type,
				RawData = bytesEvent.RawData == null ? new byte[8] : (byte[])bytesEvent.RawData.Clone()
			};
			var note = sourceEvent.TypeData as BytesEvent.NoteTypeData;
			if (note != null)
			{
				result.InstrumentId = note.Ins;
				result.Velocity = note.Vel;
				result.Pan = note.Pan;
				result.Attribute = note.Attr;
				result.Duration = note.Dur;
			}
			var volume = sourceEvent.TypeData as BytesEvent.VolumeTypeData;
			if (volume != null) result.Volume = volume.Volume;
			var tempo = sourceEvent.TypeData as BytesEvent.TempoTypeData;
			if (tempo != null) result.Tempo = tempo.Tempo;
			var beat = sourceEvent.TypeData as BytesEvent.BeatTypeData;
			if (beat != null) result.Beat = beat.Beat;
			var unknown = sourceEvent.TypeData as BytesEvent.UnknownTypeData;
			if (unknown != null)
			{
				result.UnknownData1 = unknown.Data1;
				result.UnknownData2 = unknown.Data2;
			}
			return result;
		}

		private static uint ReadUInt32LittleEndian(byte[] data, int offset)
		{
			if (data.Length < offset + 4) return 0;
			return (uint)(data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16 | data[offset + 3] << 24);
		}
	}
}
