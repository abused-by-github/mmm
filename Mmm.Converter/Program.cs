using CommandLine;
using Grace.DependencyInjection;
using Mmm.Connectors.Api;
using Mmm.Connectors.BluecoinConnector;
using Mmm.Connectors.MmexConnector;
using Mmm.Domain;
using System;
using System.IO;

namespace Mmm.Converter
{
    class Program
    {
        static void Main(string[] args)
        {
            var container = new DependencyInjectionContainer();

            container.Configure(c => c.Export<MmexConnector>().AsKeyed<IConnector>("mmex"));
            container.Configure(c => c.Export<BluecoinConnector>().AsKeyed<IConnector>("bcoin"));
            container.Configure(c => c.Export<CurrencyExchange>().Lifestyle.Singleton());

            Parser.Default.ParseArguments<CliOptions>(args).WithParsed(o =>
            {
                Console.WriteLine($"Importing data from {o.FromFile} to {o.ToFile}");

                try
                {
                    if (!container.CanLocate(typeof(IConnector), key: o.FromFormat))
                    {
                        throw new Exception($"Unsupported format: {o.FromFormat}");
                    }

                    if (!container.CanLocate(typeof(IConnector), key: o.ToFormat))
                    {
                        throw new Exception($"Unsupported format: {o.ToFormat}");
                    }

                    if (!File.Exists(o.FromFile))
                    {
                        throw new Exception($"File not found: {o.FromFile}");
                    }

                    if (!File.Exists(o.ToFile))
                    {
                        throw new Exception($"File not found: {o.ToFile}");
                    }

                    var fromConnector = container.Locate<IConnector>(withKey: o.FromFormat);
                    var toConnector = container.Locate<IConnector>(withKey: o.ToFormat);

                    var db = fromConnector.ReadDatabase(o.FromFile);
                    toConnector.WriteDatabase(db, o.ToFile);

                    Console.WriteLine("Imported completed successfully.");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Import failed: {e.Message}");
                }
            });
        }
    }
}
