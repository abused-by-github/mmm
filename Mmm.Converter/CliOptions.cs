using CommandLine;
using System;
using System.Collections.Generic;
using System.Text;

namespace Mmm.Converter
{
    public class CliOptions
    {
        [Option("from-file")]
        public string FromFile { get; set; }

        [Option("from-format")]
        public string FromFormat { get; set; }

        [Option("to-file")]
        public string ToFile { get; set; }

        [Option("to-format")]
        public string ToFormat { get; set; }
    }
}
