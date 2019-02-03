using Dapper.Contrib.Extensions;
using Mmm.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace Mmm.Connectors.BluecoinConnector.Data
{
    [Table(nameof(ACCOUNTSTABLE))]
    class ACCOUNTSTABLE
    {
        [ExplicitKey]
        public long accountsTableID { get; set; }
        public string accountName { get; set; }
        public long accountTypeID { get; set; }
        public string accountCurrency { get; set; }
        public decimal accountConversionRateNew { get; set; } = 1;

        public int creditLimit { get; set; } = 0;
        public int cutOffDa { get; set; } = 0;
        public int creditCardDueDate { get; set; } = 0;
        public int cashBasedAccounts { get; set; } = 0;
        public int accountHidden { get; set; } = 0;

        [Computed]
        public Account MmmAccount { get; set; }
    }
}
