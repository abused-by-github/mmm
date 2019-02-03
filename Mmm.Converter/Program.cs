using CommandLine;
using Grace.DependencyInjection;
using Mmm.Connectors.Api;
using Mmm.Connectors.BluecoinConnector;
using Mmm.Connectors.MmexConnector;
using Mmm.Domain;
using System;

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

            Parser.Default.ParseArguments<CliOptions>(args)
                   .WithParsed<CliOptions>(o =>
                   {
                       var fromConnector = container.Locate<IConnector>(withKey: o.FromFormat);
                       var toConnector = container.Locate<IConnector>(withKey: o.ToFormat);

                       var db = fromConnector.ReadDatabase(o.FromFile);
                       toConnector.WriteDatabase(db, o.ToFile);
                   });
        }
    }
}
