using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;

namespace Mid2Nix
{
    public class MidiService
    {
        public MidiData LoadMidi(string filePath)
        {
            var midiFile = MidiFile.Read(filePath);
            var tempoMap = midiFile.GetTempoMap();
            var data = new MidiData();

            data.Header.Format = (int)midiFile.OriginalFormat;
            if (midiFile.TimeDivision is TicksPerQuarterNoteTimeDivision ticksDiv)
                data.Header.TicksPerBeat = ticksDiv.TicksPerQuarterNote;
            
            var tempo = tempoMap.GetTempoAtTime(new MidiTimeSpan(0));
            data.Header.Tempo = (int)tempo.BeatsPerMinute;

            var timeSignature = tempoMap.GetTimeSignatureAtTime(new MidiTimeSpan(0));
            data.Header.TimeSignature = $"{timeSignature.Numerator}/{timeSignature.Denominator}";

            foreach (var trackChunk in midiFile.GetTrackChunks())
            {
                var track = new MidiTrack();
                
                var nameEvent = trackChunk.Events.OfType<SequenceTrackNameEvent>().FirstOrDefault();
                if (nameEvent != null) track.Name = nameEvent.Text;

                var noteOnEvent = trackChunk.Events.OfType<NoteOnEvent>().FirstOrDefault();
                if (noteOnEvent != null) track.Channel = noteOnEvent.Channel + 1;

                var programChangeEvent = trackChunk.Events.OfType<ProgramChangeEvent>().FirstOrDefault();
                if (programChangeEvent != null) track.Instrument = programChangeEvent.ProgramNumber.ToString();

                // Notes
                var notes = trackChunk.GetNotes();
                foreach (var note in notes)
                {
                    var startBeats = note.TimeAs<MusicalTimeSpan>(tempoMap).ToFractionalBeats();
                    var durationBeats = note.LengthAs<MusicalTimeSpan>(tempoMap).ToFractionalBeats();

                    track.Notes.Add(new MidiNote
                    {
                        Pitch = note.NoteName.ToString() + note.Octave,
                        Start = Math.Round(startBeats, 6),
                        Duration = Math.Round(durationBeats, 6),
                        Velocity = note.Velocity
                    });
                }

                // CC Events
                var timedEvents = trackChunk.GetTimedEvents();
                var ccEvents = timedEvents.Where(te => te.Event is Melanchall.DryWetMidi.Core.ControlChangeEvent);
                foreach (var group in ccEvents.GroupBy(te => (int)((Melanchall.DryWetMidi.Core.ControlChangeEvent)te.Event).ControlNumber))
                {
                    var list = new List<Mid2Nix.ControlChangeEvent>();
                    foreach (var te in group)
                    {
                        var e = (Melanchall.DryWetMidi.Core.ControlChangeEvent)te.Event;
                        var timeBeats = te.TimeAs<MusicalTimeSpan>(tempoMap).ToFractionalBeats();
                        list.Add(new Mid2Nix.ControlChangeEvent
                        {
                            Time = Math.Round(timeBeats, 6),
                            Value = e.ControlValue
                        });
                    }
                    track.CC[(int)group.Key] = list;
                }

                if (track.Notes.Any() || track.CC.Any() || (nameEvent != null))
                {
                    data.Tracks.Add(track);
                }
            }

            return data;
        }

        public void SaveMidi(MidiData data, string filePath)
        {
            var timeDivision = new TicksPerQuarterNoteTimeDivision((short)data.Header.TicksPerBeat);
            var tsParts = data.Header.TimeSignature.Split('/');
            var tsNumerator = byte.Parse(tsParts[0]);
            var tsDenominator = byte.Parse(tsParts[1]);

            var tempoMap = TempoMap.Create(timeDivision, 
                Tempo.FromBeatsPerMinute(data.Header.Tempo),
                new TimeSignature(tsNumerator, tsDenominator));

            var chunks = new List<TrackChunk>();

            // For Format 1, create a separate tempo track
            if (data.Header.Format != 0)
            {
                var tempoTrack = new TrackChunk();
                using (var manager = tempoTrack.ManageTimedEvents())
                {
                    manager.Objects.Add(new TimedEvent(new SetTempoEvent(Tempo.FromBeatsPerMinute(data.Header.Tempo).MicrosecondsPerQuarterNote), 0));
                    manager.Objects.Add(new TimedEvent(new TimeSignatureEvent(tsNumerator, (byte)tsDenominator), 0));
                }
                chunks.Add(tempoTrack);
            }

            foreach (var trackData in data.Tracks)
            {
                var trackChunk = new TrackChunk();
                
                using (var timedEventsManager = trackChunk.ManageTimedEvents())
                {
                    if (!string.IsNullOrEmpty(trackData.Name))
                        timedEventsManager.Objects.Add(new TimedEvent(new SequenceTrackNameEvent(trackData.Name), 0));

                    if (data.Header.Format == 0 && chunks.Count == 0)
                    {
                        timedEventsManager.Objects.Add(new TimedEvent(new SetTempoEvent(Tempo.FromBeatsPerMinute(data.Header.Tempo).MicrosecondsPerQuarterNote), 0));
                        timedEventsManager.Objects.Add(new TimedEvent(new TimeSignatureEvent(tsNumerator, (byte)tsDenominator), 0));
                    }

                    if (int.TryParse(trackData.Instrument, out int program))
                    {
                        timedEventsManager.Objects.Add(new TimedEvent(new ProgramChangeEvent((SevenBitNumber)program) { Channel = (FourBitNumber)(trackData.Channel - 1) }, 0));
                    }

                    foreach (var ccGroup in trackData.CC)
                    {
                        foreach (var ccData in ccGroup.Value)
                        {
                            var timeTicks = TimeConverter.ConvertFrom(TimeSpanExtensions.FromFractionalBeats(ccData.Time), tempoMap);
                            var ccEvent = new Melanchall.DryWetMidi.Core.ControlChangeEvent((SevenBitNumber)ccGroup.Key, (SevenBitNumber)ccData.Value)
                            {
                                Channel = (FourBitNumber)(trackData.Channel - 1)
                            };
                            timedEventsManager.Objects.Add(new TimedEvent(ccEvent, timeTicks));
                        }
                    }
                }

                using (var notesManager = trackChunk.ManageNotes())
                {
                    foreach (var noteData in trackData.Notes)
                    {
                        var noteNameStr = Regex.Replace(noteData.Pitch, @"-?\d", "");
                        var octaveStr = Regex.Match(noteData.Pitch, @"-?\d+").Value;
                        
                        if (!Enum.TryParse<NoteName>(noteNameStr, out var noteName)) noteName = NoteName.C;
                        if (!int.TryParse(octaveStr, out var octave)) octave = 4;
                        
                        var startTicks = TimeConverter.ConvertFrom(TimeSpanExtensions.FromFractionalBeats(noteData.Start), tempoMap);
                        var lengthTicks = TimeConverter.ConvertFrom(TimeSpanExtensions.FromFractionalBeats(noteData.Duration), tempoMap);

                        notesManager.Objects.Add(new Melanchall.DryWetMidi.Interaction.Note(
                            Melanchall.DryWetMidi.MusicTheory.Note.Get(noteName, octave).NoteNumber,
                            lengthTicks,
                            startTicks)
                        {
                            Velocity = (SevenBitNumber)noteData.Velocity,
                            Channel = (FourBitNumber)(trackData.Channel - 1)
                        });
                    }
                }

                if (trackChunk.Events.Any())
                {
                    chunks.Add(trackChunk);
                }
            }

            var midiFile = new MidiFile(chunks);
            midiFile.TimeDivision = timeDivision;
            midiFile.Write(filePath, overwriteFile: true, format: (MidiFileFormat)data.Header.Format);
        }
    }

    public static class TimeSpanExtensions
    {
        public static double ToFractionalBeats(this MusicalTimeSpan timeSpan)
        {
            return (double)timeSpan.Numerator / timeSpan.Denominator * 4.0;
        }

        public static MusicalTimeSpan FromFractionalBeats(double beats)
        {
            long numerator = (long)Math.Round(beats * 1000000);
            return new MusicalTimeSpan(numerator, 4000000);
        }
    }
}
