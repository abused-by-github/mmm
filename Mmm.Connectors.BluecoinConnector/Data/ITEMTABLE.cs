using Dapper.Contrib.Extensions;
using Mmm.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace Mmm.Connectors.BluecoinConnector.Data
{
    [Table(nameof(ITEMTABLE))]
    class ITEMTABLE
    {
        [ExplicitKey]
        public long itemTableID { get; set; }

        public string itemName { get; set; }

        [Computed]
        public Transaction MmmTransaction { get; set; }
    }
}
