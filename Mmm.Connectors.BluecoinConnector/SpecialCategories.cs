namespace Mmm.Connectors.BluecoinConnector
{
    class SpecialCategories
    {
        public long CategoryGroupTransferID { get; set; }
        public long CategoryGroupIncomeID { get; set; }
        public long CategoryGroupExpenseID { get; set; }
        public long OthersExpenseParentCatID { get; set; }
        public long OthersIncomeParentCatID { get; set; }

        public long NewAccountCatID { get; set; }
        public long TransferCatID { get; set; }
    }
}
