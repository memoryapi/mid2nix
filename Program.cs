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
            var rootCommand = new RootCommand("MIDI to Nix-like Text Converter");

            var inputArg = new Argument<FileInfo>("input", "The input MIDI file").ExistingOnly();
            var outputArg = new Argument<FileInfo>("output", "The output text file");

            var toTextCommand = new Command("to-text", "Convert a MIDI file to Nix-like text format")
            {
                inputArg,
                outputArg
            };

            toTextCommand.SetHandler(async (FileInfo input, FileInfo output) =>
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
            }, inputArg, outputArg);

            var inputMidiArg = new Argument<FileInfo>("input", "The input text file").ExistingOnly();
            var outputMidiArg = new Argument<FileInfo>("output", "The output MIDI file");

            var toMidiCommand = new Command("to-midi", "Convert Nix-like text format to MIDI")
            {
                inputMidiArg,
                outputMidiArg
            };

            toMidiCommand.SetHandler(async (FileInfo input, FileInfo output) =>
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
            }, inputMidiArg, outputMidiArg);

            rootCommand.AddCommand(toTextCommand);
            rootCommand.AddCommand(toMidiCommand);

            return await rootCommand.InvokeAsync(args);
        }
    }
}
