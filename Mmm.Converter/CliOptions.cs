using CommandLine;

namespace Mmm.Converter
{
    public class CliOptions
    {
        [Option("from-file", HelpText = "Source database file. Must exist before operation.")]
        public string FromFile { get; set; }

        [Option("from-format")]
        public string FromFormat { get; set; }

        [Option("to-file", HelpText = "Target database file. Must exist before operation.")]
        public string ToFile { get; set; }

        [Option("to-format")]
        public string ToFormat { get; set; }
    }
}
