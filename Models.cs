using System;
using System.Collections.Generic;

namespace Mid2Nix
{
    public class MidiData
    {
        public MidiHeader Header { get; set; } = new MidiHeader();
        public List<MidiTrack> Tracks { get; set; } = new List<MidiTrack>();
    }

    public class MidiHeader
    {
        public int Format { get; set; } = 1;
        public int TicksPerBeat { get; set; } = 480;
        public int Tempo { get; set; } = 120;
        public string TimeSignature { get; set; } = "4/4";
    }

    public class MidiTrack
    {
        public string Name { get; set; } = "Track";
        public int Channel { get; set; } = 1;
        public string Instrument { get; set; } = "Piano";
        public List<MidiNote> Notes { get; set; } = new List<MidiNote>();
        public Dictionary<int, List<ControlChangeEvent>> CC { get; set; } = new Dictionary<int, List<ControlChangeEvent>>();
    }

    public class MidiNote
    {
        public string Pitch { get; set; } = "C4";
        public double Start { get; set; }
        public double Duration { get; set; }
        public int Velocity { get; set; } = 100;
    }

    public class ControlChangeEvent
    {
        public double Time { get; set; }
        public int Value { get; set; }
    }
}
