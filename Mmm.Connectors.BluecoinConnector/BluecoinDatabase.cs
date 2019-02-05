using Mmm.Connectors.BluecoinConnector.Data;
using System.Collections.Generic;

namespace Mmm.Connectors.BluecoinConnector
{
    class BluecoinDatabase
    {
        public string DefaultCurrency { get; set; }
        public long AccountTypeCashID { get; set; }
        public SpecialCategories SpecialCategories { get; set; }
        public TransactionTypes TransactionTypes { get; set; }
        public List<ACCOUNTSTABLE> Accounts { get; set; }
        public List<CHILDCATEGORYTABLE> ChildCategories { get; set; }
    }
}
