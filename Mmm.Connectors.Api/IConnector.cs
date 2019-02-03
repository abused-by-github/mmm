using Mmm.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace Mmm.Connectors.Api
{
    public interface IConnector
    {
        Database ReadDatabase(string file);
        void WriteDatabase(Database database, string file);
    }
}
