using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Mmm.Connectors.BluecoinConnector.Data
{
    [Table(nameof(ACCOUNTINGGROUPTABLE))]
    class ACCOUNTINGGROUPTABLE
    {
        [ExplicitKey]
        public long accountingGroupTableID { get; set; }
        public string accountGroupName { get; set; }
    }
}
