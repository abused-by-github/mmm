using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Mmm.Connectors.BluecoinConnector.Data
{
    class TRANSACTIONTYPETABLE
    {
        [ExplicitKey]
        public long transactionTypeTableID { get; set; }
        public string transactionTypeName { get; set; }
    }
}
