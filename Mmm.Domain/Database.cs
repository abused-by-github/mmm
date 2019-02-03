using System;
using System.Collections.Generic;
using System.Text;

namespace Mmm.Domain
{
    public class Database
    {
        public List<Account> Accounts = new List<Account>();
        public List<Category> Categories = new List<Category>();
        public List<Transaction> Transactions = new List<Transaction>();
    }
}
