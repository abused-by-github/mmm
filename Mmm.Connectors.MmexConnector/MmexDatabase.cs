using Mmm.Connectors.MmexConnector.Data;
using System.Collections.Generic;

namespace Mmm.Connectors.MmexConnector
{
    class MmexDatabase
    {
        public List<ACCOUNTLIST> Accounts { get; set; }
        public List<CHECKINGACCOUNT> Transactions { get; set; }
        public List<SUBCATEGORY> Subcategories { get; set; }
        public List<CATEGORY> Categories { get; set; }
        public List<CURRENCYFORMATS> Currencies { get; set; }
    }
}
