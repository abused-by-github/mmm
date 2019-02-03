using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Mmm.Connectors.BluecoinConnector.Data
{
    [Table(nameof(ACCOUNTTYPETABLE))]
    class ACCOUNTTYPETABLE
    {
        [ExplicitKey]
        public long accountTypeTableID { get; set; }
        public string accountTypeName { get; set; }
        public long accountingGroupID { get; set; }
    }
}
