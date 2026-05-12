using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using Melanchall.DryWetMidi.Core;

namespace Mid2Nix
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            // Support for drag-and-drop: if a single file is provided, auto-convert based on extension
            if (args.Length == 1 && File.Exists(args[0]))
            {
                var inputPath = Path.GetFullPath(args[0]);
                var extension = Path.GetExtension(inputPath).ToLowerInvariant();

                if (extension == ".mid" || extension == ".midi")
                {
                    var outputPath = GetUniquePath(Path.ChangeExtension(inputPath, ".nix"));
                    await ConvertToText(new FileInfo(inputPath), new FileInfo(outputPath));
                    return 0;
                }
                else if (extension == ".nix")
                {
                    var outputPath = GetUniquePath(Path.ChangeExtension(inputPath, ".mid"));
                    await ConvertToMidi(new FileInfo(inputPath), new FileInfo(outputPath));
                    return 0;
                }
            }

            var rootCommand = new RootCommand("MIDI to Nix-like Text Converter");

            var inputArg = new Argument<FileInfo>("input", "The input MIDI file").ExistingOnly();
            var outputArg = new Argument<FileInfo>("output", "The output text file");

            var toTextCommand = new Command("to-text", "Convert a MIDI file to Nix-like text format")
            {
                inputArg,
                outputArg
            };

            toTextCommand.SetHandler(ConvertToText, inputArg, outputArg);

            var inputMidiArg = new Argument<FileInfo>("input", "The input text file").ExistingOnly();
            var outputMidiArg = new Argument<FileInfo>("output", "The output MIDI file");

            var toMidiCommand = new Command("to-midi", "Convert Nix-like text format to MIDI")
            {
                inputMidiArg,
                outputMidiArg
            };

            toMidiCommand.SetHandler(ConvertToMidi, inputMidiArg, outputMidiArg);

            rootCommand.AddCommand(toTextCommand);
            rootCommand.AddCommand(toMidiCommand);

            return await rootCommand.InvokeAsync(args);
        }

        static async Task ConvertToText(FileInfo input, FileInfo output)
        {
            try
            {
                var midiService = new MidiService();
                var nixSerializer = new NixSerializer();

                Console.WriteLine($"Loading MIDI: {input.FullName}");
                var data = midiService.LoadMidi(input.FullName);

                Console.WriteLine("Serializing to Nix format...");
                var text = nixSerializer.Serialize(data);

                await File.WriteAllTextAsync(output.FullName, text);
                Console.WriteLine($"Saved to: {output.FullName}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.Exit(1);
            }
        }

        static async Task ConvertToMidi(FileInfo input, FileInfo output)
        {
            try
            {
                var midiService = new MidiService();
                var nixSerializer = new NixSerializer();

                Console.WriteLine($"Loading Text: {input.FullName}");
                var text = await File.ReadAllTextAsync(input.FullName);

                Console.WriteLine("Deserializing from Nix format...");
                var data = nixSerializer.Deserialize(text);

                Console.WriteLine($"Saving to MIDI: {output.FullName}");
                midiService.SaveMidi(data, output.FullName);
                Console.WriteLine("Success!");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.Exit(1);
            }
        }

        static string GetUniquePath(string path)
        {
            if (!File.Exists(path)) return path;

            string directory = Path.GetDirectoryName(path) ?? "";
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            int count = 1;

            while (File.Exists(path))
            {
                path = Path.Combine(directory, $"{fileNameWithoutExtension}_{count++}{extension}");
            }

            return path;
        }
    }
}
