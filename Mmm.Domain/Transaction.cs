using System;

namespace Mmm.Domain
{
    public class Transaction
    {
        public Account FromAccount;
        public Account ToAccount;
        public decimal FromAmount;
        public decimal ToAmount;
        public Category Category;
        public string Notes;
        public DateTime Date;

        public TransactionType Type
        {
            get
            {
                if (FromAccount == null) return TransactionType.Income;
                if (ToAccount == null) return TransactionType.Expense;
                if (FromAccount != null && ToAccount != null && FromAccount != ToAccount) return TransactionType.Transfer;
                if (FromAccount != null && ToAccount != null && FromAccount == ToAccount) return TransactionType.Adjustment;

                throw new Exception("Can't determine transaction type.");
            }
        }
    }
}
