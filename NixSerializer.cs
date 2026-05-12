using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Mid2Nix
{
    public class NixSerializer
    {
        public string Serialize(MidiData data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            
            // Header
            sb.AppendLine("  header = {");
            sb.AppendLine($"    format = {data.Header.Format};");
            sb.AppendLine($"    ticksPerBeat = {data.Header.TicksPerBeat};");
            sb.AppendLine($"    tempo = {data.Header.Tempo};");
            sb.AppendLine($"    timeSignature = \"{data.Header.TimeSignature}\";");
            sb.AppendLine("  };");
            sb.AppendLine();

            // Tracks
            sb.AppendLine("  tracks = [");
            foreach (var track in data.Tracks)
            {
                sb.AppendLine("    {");
                sb.AppendLine($"      name = \"{track.Name}\";");
                sb.AppendLine($"      channel = {track.Channel};");
                sb.AppendLine($"      instrument = \"{track.Instrument}\";");
                sb.AppendLine();
                
                sb.AppendLine("      notes = [");
                foreach (var note in track.Notes)
                {
                    sb.AppendLine($"        {{ pitch = \"{note.Pitch}\"; start = {note.Start.ToString(CultureInfo.InvariantCulture)}; dur = {note.Duration.ToString(CultureInfo.InvariantCulture)}; vel = {note.Velocity}; }}");
                }
                sb.AppendLine("      ];");

                if (track.CC.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine("      cc = {");
                    foreach (var cc in track.CC)
                    {
                        sb.Append($"        \"{cc.Key}\" = [ ");
                        foreach (var ev in cc.Value)
                        {
                            sb.Append($"{{ time = {ev.Time.ToString(CultureInfo.InvariantCulture)}; value = {ev.Value}; }} ");
                        }
                        sb.AppendLine("];");
                    }
                    sb.AppendLine("      };");
                }

                sb.AppendLine("    }");
            }
            sb.AppendLine("  ];");
            
            sb.AppendLine("}");
            return sb.ToString();
        }

        public MidiData Deserialize(string text)
        {
            var tokens = Tokenize(text);
            var index = 0;
            return ParseMidiData(tokens, ref index);
        }

        private List<string> Tokenize(string text)
        {
            var tokens = new List<string>();
            var pattern = @"(#.*)|(/\*[\s\S]*?\*/)|([\{\}\[\];=])|""([^""]*)""|(-?\d+\.?\d*)|([a-zA-Z_][a-zA-Z0-9_]*)";
            var matches = Regex.Matches(text, pattern);
            foreach (Match m in matches)
            {
                if (m.Groups[1].Success || m.Groups[2].Success) continue;
                if (m.Groups[3].Success) tokens.Add(m.Groups[3].Value);
                else if (m.Groups[4].Success) tokens.Add($"\"{m.Groups[4].Value}\"");
                else if (m.Groups[5].Success) tokens.Add(m.Groups[5].Value);
                else if (m.Groups[6].Success) tokens.Add(m.Groups[6].Value);
            }
            return tokens;
        }

        private MidiData ParseMidiData(List<string> tokens, ref int index)
        {
            var data = new MidiData();
            Expect(tokens, ref index, "{");
            while (index < tokens.Count && tokens[index] != "}")
            {
                var key = tokens[index++];
                Expect(tokens, ref index, "=");
                if (key == "header") data.Header = ParseHeader(tokens, ref index);
                else if (key == "tracks") data.Tracks = ParseTracks(tokens, ref index);
                Expect(tokens, ref index, ";");
            }
            Expect(tokens, ref index, "}");
            return data;
        }

        private MidiHeader ParseHeader(List<string> tokens, ref int index)
        {
            var header = new MidiHeader();
            Expect(tokens, ref index, "{");
            while (tokens[index] != "}")
            {
                var key = tokens[index++];
                Expect(tokens, ref index, "=");
                var value = tokens[index++];
                if (key == "format") header.Format = int.Parse(value);
                else if (key == "ticksPerBeat") header.TicksPerBeat = int.Parse(value);
                else if (key == "tempo") header.Tempo = int.Parse(value);
                else if (key == "timeSignature") header.TimeSignature = value.Trim('"');
                Expect(tokens, ref index, ";");
            }
            index++; // skip }
            return header;
        }

        private List<MidiTrack> ParseTracks(List<string> tokens, ref int index)
        {
            var tracks = new List<MidiTrack>();
            Expect(tokens, ref index, "[");
            while (tokens[index] != "]")
            {
                tracks.Add(ParseTrack(tokens, ref index));
            }
            index++; // skip ]
            return tracks;
        }

        private MidiTrack ParseTrack(List<string> tokens, ref int index)
        {
            var track = new MidiTrack();
            Expect(tokens, ref index, "{");
            while (tokens[index] != "}")
            {
                var key = tokens[index++];
                Expect(tokens, ref index, "=");
                if (key == "name") track.Name = tokens[index++].Trim('"');
                else if (key == "channel") track.Channel = int.Parse(tokens[index++]);
                else if (key == "instrument") track.Instrument = tokens[index++].Trim('"');
                else if (key == "notes") track.Notes = ParseNotes(tokens, ref index);
                else if (key == "cc") track.CC = ParseCCMap(tokens, ref index);
                else index++; // skip unknown
                Expect(tokens, ref index, ";");
            }
            index++; // skip }
            return track;
        }

        private List<MidiNote> ParseNotes(List<string> tokens, ref int index)
        {
            var notes = new List<MidiNote>();
            Expect(tokens, ref index, "[");
            while (tokens[index] != "]")
            {
                var note = new MidiNote();
                Expect(tokens, ref index, "{");
                while (tokens[index] != "}")
                {
                    var key = tokens[index++];
                    Expect(tokens, ref index, "=");
                    var val = tokens[index++];
                    if (key == "pitch") note.Pitch = val.Trim('"');
                    else if (key == "start") note.Start = double.Parse(val, CultureInfo.InvariantCulture);
                    else if (key == "dur") note.Duration = double.Parse(val, CultureInfo.InvariantCulture);
                    else if (key == "vel") note.Velocity = int.Parse(val);
                    Expect(tokens, ref index, ";");
                }
                index++; // skip }
                notes.Add(note);
            }
            index++; // skip ]
            return notes;
        }

        private Dictionary<int, List<ControlChangeEvent>> ParseCCMap(List<string> tokens, ref int index)
        {
            var ccMap = new Dictionary<int, List<ControlChangeEvent>>();
            Expect(tokens, ref index, "{");
            while (tokens[index] != "}")
            {
                var ccNumber = int.Parse(tokens[index++].Trim('"'));
                Expect(tokens, ref index, "=");
                ccMap[ccNumber] = ParseCCList(tokens, ref index);
                Expect(tokens, ref index, ";");
            }
            index++; // skip }
            return ccMap;
        }

        private List<ControlChangeEvent> ParseCCList(List<string> tokens, ref int index)
        {
            var list = new List<ControlChangeEvent>();
            Expect(tokens, ref index, "[");
            while (tokens[index] != "]")
            {
                var ev = new ControlChangeEvent();
                Expect(tokens, ref index, "{");
                while (tokens[index] != "}")
                {
                    var key = tokens[index++];
                    Expect(tokens, ref index, "=");
                    var val = tokens[index++];
                    if (key == "time") ev.Time = double.Parse(val, CultureInfo.InvariantCulture);
                    else if (key == "value") ev.Value = int.Parse(val);
                    Expect(tokens, ref index, ";");
                }
                index++; // skip }
                list.Add(ev);
            }
            index++; // skip ]
            return list;
        }

        private void Expect(List<string> tokens, ref int index, string expected)
        {
            if (index >= tokens.Count || tokens[index] != expected)
                throw new Exception($"Expected '{expected}' at index {index}, but found '{(index < tokens.Count ? tokens[index] : "EOF")}'");
            index++;
        }
    }
}
