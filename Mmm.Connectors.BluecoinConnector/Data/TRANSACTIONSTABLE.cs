using Dapper.Contrib.Extensions;
using Mmm.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace Mmm.Connectors.BluecoinConnector.Data
{
    [Table(nameof(TRANSACTIONSTABLE))]
    class TRANSACTIONSTABLE
    {
        [ExplicitKey]
        public long transactionsTableID { get; set; }
        public long itemID { get; set; }
        public decimal amount { get; set; }
        public string transactionCurrency { get; set; }
        public decimal conversionRateNew { get; set; } = 0;
        public DateTime date { get; set; }
        public long transactionTypeID { get; set; }
        public long categoryID { get; set; }
        public long accountID { get; set; }
        public string notes { get; set; } = "";
        public int status { get; set; }
        public int accountReference { get; set; } = 1;
        public long accountPairID { get; set; }
        public int deletedTransaction { get; set; }
        public long uidPairID { get; set; }
        public long transferGroupID { get; set; }

        public long newSplitTransactionID { get; set; } = 0;

        [Computed]
        public Transaction MmmTransaction { get; set; }
    }
}
