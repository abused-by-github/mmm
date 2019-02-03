using Mmm.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace Mmm.Connectors.MmexConnector.Data
{
    class ACCOUNTLIST
    {
        public int ACCOUNTID;
        public string ACCOUNTNAME;
        public decimal INITIALBAL;
        public int CURRENCYID;

        public Account MmmAccount;
    }
}
