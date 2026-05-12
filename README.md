# Mid2Nix

A bidirectional MIDI to Nix-like text format converter. Compose MIDI in your favorite text editor using a structured, human-readable format, or convert existing MIDI files to text for analysis and modification.

## Features

- **Bidirectional Conversion**: Convert `.mid` to `.nix` and `.nix` back to `.mid`.
- **Nix-like Syntax**: Clean, declarative format for musical data.
- **Support for Multiple Tracks**: Handle complex compositions with independent channels and instruments.
- **Control Changes**: Support for CC events like Sustain Pedal, Volume, etc.
- **Comments Support**: Use `#` for single-line and `/* */` for multi-line comments in your Nix files.
- **High Fidelity**: Preserves MIDI format (SingleTrack vs MultiTrack) during round-trips.

## Installation

Ensure you have the .NET SDK installed.

```bash
git clone https://github.com/memoryapi/mid2nix
cd mid2nix
dotnet build
```

## Usage

### Convert MIDI to Text

```bash
dotnet run -- to-text input.mid output.nix
```

### Convert Text to MIDI

```bash
dotnet run -- to-midi input.nix output.mid
```

## Example Format

```nix
{
  header = {
    format = 1;
    ticksPerBeat = 480;
    tempo = 120;
    timeSignature = "4/4";
  };

  tracks = [
    {
      name = "Piano";
      channel = 1;
      instrument = "0"; # Grand Piano
      notes = [
        { pitch = "C4"; start = 0.0; dur = 1.0; vel = 100; }
        { pitch = "E4"; start = 1.0; dur = 1.0; vel = 100; }
        { pitch = "G4"; start = 2.0; dur = 2.0; vel = 110; }
      ];
    }
  ];
}
```

## License

This project is licensed under the GPLv3 License - see the [LICENSE](LICENSE) file for details.
