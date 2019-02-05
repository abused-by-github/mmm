using Dapper;
using Dapper.Contrib.Extensions;
using Mmm.Connectors.Api;
using Mmm.Connectors.BluecoinConnector.Data;
using Mmm.Domain;
using Mmm.Helpers;
using System;
using System.Data;
using System.Data.SQLite;
using System.Linq;

namespace Mmm.Connectors.BluecoinConnector
{
    public class BluecoinConnector : IConnector
    {
        //Amounts in Bluecoin database are stored as integer number of millionths
        private const long TransactionAmountFactor = 1000000;

        private readonly CurrencyExchange _currencyExchange;

        public BluecoinConnector(CurrencyExchange currencyExchange)
        {
            _currencyExchange = currencyExchange;
        }

        public Database ReadDatabase(string file)
        {
            throw new NotImplementedException();
        }

        private void ImportAccounts(IDbConnection connection, Database database, BluecoinDatabase bluecoinDatabase)
        {
            bluecoinDatabase.Accounts = database.Accounts.Select(a => new ACCOUNTSTABLE
            {
                MmmAccount = a,
                accountName = a.Name,
                accountCurrency = a.CurrencyCode,
                accountTypeID = bluecoinDatabase.AccountTypeCashID,
                accountConversionRateNew = _currencyExchange.GetRate(bluecoinDatabase.DefaultCurrency, a.CurrencyCode)
            }).ToList();

            foreach (var account in bluecoinDatabase.Accounts)
            {
                var existingAccount = connection.Query<ACCOUNTSTABLE>(
                    "SELECT * FROM ACCOUNTSTABLE WHERE accountName = @accountName AND accountTypeID = @accountTypeID", account).SingleOrDefault();

                if (existingAccount == null)
                {
                    account.accountsTableID = connection.NextID<ACCOUNTSTABLE>();
                    connection.Insert(account);
                }
                else
                {
                    if (existingAccount.accountCurrency != account.accountCurrency)
                    {
                        throw new Exception($"Currency clash for account {account.accountName}. Source currency: {account.accountCurrency}. Target currency = {existingAccount.accountCurrency}.");
                    }
                    account.accountsTableID = existingAccount.accountsTableID;
                }
            }
        }

        private void ImportCategories(IDbConnection connection, Database database, BluecoinDatabase bluecoinDatabase)
        {
            var parentCategories = database.Categories
                .Where(c => c.Parent == null && database.Categories.Select(cc => cc.Parent).Contains(c))
                .Select(c => new PARENTCATEGORYTABLE
                {
                    categoryGroupId = ResolveParentCategoryGroup(c, database, bluecoinDatabase.SpecialCategories),
                    parentCategoryName = c.Name,
                    MmmCategory = c
                })
                .ToList();

            foreach (var category in parentCategories)
            {
                var existingCategory = connection.Query<PARENTCATEGORYTABLE>(
                    "SELECT * FROM PARENTCATEGORYTABLE WHERE parentCategoryName = @parentCategoryName", category).SingleOrDefault();

                if (existingCategory == null)
                {
                    category.parentCategoryTableId = connection.NextID<PARENTCATEGORYTABLE>();
                    connection.Insert(category);
                }
                else
                {
                    category.parentCategoryTableId = existingCategory.parentCategoryTableId;
                }
            }

            bluecoinDatabase.ChildCategories = database.Categories
                .Where(c => c.Parent != null || !database.Categories.Select(cc => cc.Parent).Contains(c))
                .Select(c => new CHILDCATEGORYTABLE
                {
                    parentCategoryId = ResolveParentCategory(c, database, bluecoinDatabase.SpecialCategories) ?? parentCategories.Where(pc => pc.MmmCategory == c.Parent).Single().parentCategoryTableId,
                    childCategoryName = c.Name,
                    childCategoryIcon = "xxx_more_horiz_black_24dp",
                    MmmCategory = c
                })
                .ToList();

            //Bluecoin does not support using parent categories in transactions
            var parentWithTransactions = parentCategories
                .Where(c => database.Transactions.Select(t => t.Category).Contains(c.MmmCategory));
            foreach (var c in parentWithTransactions)
            {
                var child = new CHILDCATEGORYTABLE
                {
                    parentCategoryId = c.parentCategoryTableId,
                    childCategoryName = c.parentCategoryName,
                    childCategoryIcon = "xxx_more_horiz_black_24dp",
                    MmmCategory = c.MmmCategory
                };
                bluecoinDatabase.ChildCategories.Add(child);
            }

            foreach (var category in bluecoinDatabase.ChildCategories)
            {
                var existingCategory = connection.Query<CHILDCATEGORYTABLE>(
                    "SELECT * FROM CHILDCATEGORYTABLE WHERE childCategoryName = @childCategoryName", category).SingleOrDefault();

                if (existingCategory == null)
                {
                    category.categoryTableID = connection.NextID<CHILDCATEGORYTABLE>();
                    connection.Insert(category);
                }
                else
                {
                    category.categoryTableID = existingCategory.categoryTableID;
                }
            }
        }

        private void ImportTransactions(IDbConnection connection, Database database, BluecoinDatabase bluecoinDatabase)
        {
            foreach (var transaction in database.Transactions)
            {
                if (transaction.Type == TransactionType.Adjustment)
                {
                    var item = new ITEMTABLE { itemTableID = connection.NextID<ITEMTABLE>(), itemName = transaction.FromAccount.Name, MmmTransaction = transaction };
                    connection.Insert(item);

                    var tran = new TRANSACTIONSTABLE
                    {
                        itemID = item.itemTableID,
                        transactionTypeID = bluecoinDatabase.TransactionTypes.NewAccount,
                        MmmTransaction = transaction,
                        categoryID = bluecoinDatabase.SpecialCategories.NewAccountCatID,
                        accountID = bluecoinDatabase.Accounts.Single(a => a.MmmAccount == transaction.FromAccount).accountsTableID,
                        accountPairID = bluecoinDatabase.Accounts.Single(a => a.MmmAccount == transaction.FromAccount).accountsTableID,
                        notes = transaction.Notes,
                        status = 2, //TODO: meaning is not clear
                        deletedTransaction = 6, //TODO: meaning is not clear
                        date = transaction.Date,
                        accountReference = 3,
                    };
                    SetAmount(tran, transaction.FromAmount, transaction.FromAccount.CurrencyCode, bluecoinDatabase.DefaultCurrency);

                    tran.transactionsTableID = tran.uidPairID = connection.NextID<TRANSACTIONSTABLE>();
                    connection.Insert(tran);
                }
                if (transaction.Type == TransactionType.Transfer)
                {
                    var item = new ITEMTABLE { itemName = transaction.FromAccount.Name, MmmTransaction = transaction };
                    var tran = new TRANSACTIONSTABLE
                    {
                        transactionTypeID = bluecoinDatabase.TransactionTypes.Transfer,
                        MmmTransaction = transaction,
                        categoryID = bluecoinDatabase.SpecialCategories.TransferCatID,
                        accountID = bluecoinDatabase.Accounts.Single(a => a.MmmAccount == transaction.FromAccount).accountsTableID,
                        accountPairID = bluecoinDatabase.Accounts.Single(a => a.MmmAccount == transaction.ToAccount).accountsTableID,
                        notes = transaction.Notes,
                        status = 0, //TODO: meaning is not clear
                        deletedTransaction = 6, //TODO: meaning is not clear
                        date = transaction.Date,
                        accountReference = 2 //TODO
                    };
                    SetAmount(tran, -transaction.FromAmount, transaction.FromAccount.CurrencyCode, bluecoinDatabase.DefaultCurrency);

                    tran.itemID = item.itemTableID = connection.NextID<ITEMTABLE>();
                    connection.Insert(item);
                    tran.transactionsTableID = tran.transferGroupID = connection.NextID<TRANSACTIONSTABLE>();
                    connection.Insert(tran);

                    var tran2 = new TRANSACTIONSTABLE
                    {
                        itemID = item.itemTableID,
                        transactionTypeID = bluecoinDatabase.TransactionTypes.Transfer,
                        MmmTransaction = transaction,
                        categoryID = bluecoinDatabase.SpecialCategories.TransferCatID,
                        accountID = bluecoinDatabase.Accounts.Single(a => a.MmmAccount == transaction.ToAccount).accountsTableID,
                        accountPairID = bluecoinDatabase.Accounts.Single(a => a.MmmAccount == transaction.FromAccount).accountsTableID,
                        notes = transaction.Notes,
                        status = 0, //TODO: meaning is not clear
                        deletedTransaction = 6, //TODO: meaning is not clear
                        uidPairID = tran.transactionsTableID,
                        transferGroupID = tran.transactionsTableID,
                        date = transaction.Date,
                        accountReference = 2 //TODO
                    };
                    SetAmount(tran2, transaction.ToAmount, transaction.ToAccount.CurrencyCode, bluecoinDatabase.DefaultCurrency);
                    tran2.transactionsTableID = tran.uidPairID = connection.NextID<TRANSACTIONSTABLE>();
                    connection.Insert(tran2);

                    connection.Update<TRANSACTIONSTABLE>(tran);
                }

                if (transaction.Type == TransactionType.Income)
                {
                    var item = new ITEMTABLE { itemName = transaction.Notes, MmmTransaction = transaction };
                    var tran = new TRANSACTIONSTABLE
                    {
                        transactionTypeID = bluecoinDatabase.TransactionTypes.Income,
                        MmmTransaction = transaction,
                        categoryID = bluecoinDatabase.ChildCategories.Single(c => c.MmmCategory == transaction.Category).categoryTableID,
                        accountID = bluecoinDatabase.Accounts.Single(a => a.MmmAccount == transaction.ToAccount).accountsTableID,
                        accountPairID = bluecoinDatabase.Accounts.Single(a => a.MmmAccount == transaction.ToAccount).accountsTableID,
                        notes = transaction.Notes,
                        status = 0, //TODO: meaning is not clear
                        deletedTransaction = 6, //TODO: meaning is not clear
                        date = transaction.Date,
                        accountReference = 1 //TODO
                    };
                    SetAmount(tran, transaction.ToAmount, transaction.ToAccount.CurrencyCode, bluecoinDatabase.DefaultCurrency);

                    tran.itemID = item.itemTableID = connection.NextID<ITEMTABLE>();
                    connection.Insert(item);
                    tran.transactionsTableID = tran.uidPairID = connection.NextID<TRANSACTIONSTABLE>();
                    connection.Insert(tran);
                }

                if (transaction.Type == TransactionType.Expense)
                {
                    var item = new ITEMTABLE { itemName = transaction.Notes, MmmTransaction = transaction };
                    var tran = new TRANSACTIONSTABLE
                    {
                        transactionTypeID = bluecoinDatabase.TransactionTypes.Expense,
                        MmmTransaction = transaction,
                        categoryID = bluecoinDatabase.ChildCategories.Single(c => c.MmmCategory == transaction.Category).categoryTableID,
                        accountID = bluecoinDatabase.Accounts.Single(a => a.MmmAccount == transaction.FromAccount).accountsTableID,
                        accountPairID = bluecoinDatabase.Accounts.Single(a => a.MmmAccount == transaction.FromAccount).accountsTableID,
                        notes = transaction.Notes,
                        status = 0, //TODO: meaning is not clear
                        deletedTransaction = 6, //TODO: meaning is not clear
                        date = transaction.Date,
                        accountReference = 1 //TODO
                    };
                    SetAmount(tran, -transaction.FromAmount, transaction.FromAccount.CurrencyCode, bluecoinDatabase.DefaultCurrency);

                    tran.itemID = item.itemTableID = connection.NextID<ITEMTABLE>();
                    connection.Insert(item);
                    tran.transactionsTableID = tran.uidPairID = connection.NextID<TRANSACTIONSTABLE>();
                    connection.Insert(tran);
                }
            }
        }

        //TODO: duplicate check
        private void SetAmount(TRANSACTIONSTABLE t, decimal tranAmount, string amountCurrency, string defaultCurrency)
        {
            t.conversionRateNew = _currencyExchange.GetRate(defaultCurrency, amountCurrency);
            //Amount is stored in base currency, regardless of transaction currency
            t.amount = (long)decimal.Round(tranAmount / t.conversionRateNew * TransactionAmountFactor);
            t.transactionCurrency = amountCurrency;
        }

        public void WriteDatabase(Database database, string file)
        {
            var builder = new SQLiteConnectionStringBuilder { DataSource = file };

            using (var connection = new SQLiteConnection(builder.ConnectionString))
            {
                connection.Open();

                using (var sqliteTransaction = connection.BeginTransaction())
                {
                    var bluecoinDb = LoadBluecoinDb(connection);

                    ImportAccounts(connection, database, bluecoinDb);
                    ImportCategories(connection, database, bluecoinDb);
                    ImportTransactions(connection, database, bluecoinDb);

                    sqliteTransaction.Commit();
                }
            }
        }

        private BluecoinDatabase LoadBluecoinDb(IDbConnection connection)
        {
            var result = new BluecoinDatabase();

            var defaultCurrencySetting = connection.Get<SETTINGSTABLE>(1);
            if (defaultCurrencySetting == null || string.IsNullOrWhiteSpace(defaultCurrencySetting.defaultSettings) || defaultCurrencySetting.defaultSettings.Length != 3)
            {
                throw new Exception("Could not find default currency setting.");
            }

            result.DefaultCurrency = defaultCurrencySetting.defaultSettings;

            var accountGroupAssets = connection.Query<ACCOUNTINGGROUPTABLE>(
                "SELECT * FROM ACCOUNTINGGROUPTABLE WHERE accountGroupName = 'Assets'").Single();
            result.AccountTypeCashID = connection.Query<ACCOUNTTYPETABLE>(
                "SELECT * FROM ACCOUNTTYPETABLE WHERE accountTypeName = 'Cash' AND accountingGroupID = @gid",
                new { gid = accountGroupAssets.accountingGroupTableID }).Single().accountTypeTableID;

            result.SpecialCategories = LoadSpecialCategories(connection);
            result.TransactionTypes = LoadTransactionTypes(connection);

            return result;
        }

        private TransactionTypes LoadTransactionTypes(IDbConnection connection)
        {
            var result = new TransactionTypes();
            result.NewAccount = connection.Query<TRANSACTIONTYPETABLE>(
                "SELECT * FROM TRANSACTIONTYPETABLE WHERE transactionTypeName = 'New Account'").Single().transactionTypeTableID;
            result.Expense = connection.Query<TRANSACTIONTYPETABLE>(
                "SELECT * FROM TRANSACTIONTYPETABLE WHERE transactionTypeName = 'Expense'").Single().transactionTypeTableID;
            result.Income = connection.Query<TRANSACTIONTYPETABLE>(
                "SELECT * FROM TRANSACTIONTYPETABLE WHERE transactionTypeName = 'Income'").Single().transactionTypeTableID;
            result.Transfer = connection.Query<TRANSACTIONTYPETABLE>(
                "SELECT * FROM TRANSACTIONTYPETABLE WHERE transactionTypeName = 'Transfer'").Single().transactionTypeTableID;

            return result;
        }

        private SpecialCategories LoadSpecialCategories(IDbConnection connection)
        {
            var result = new SpecialCategories();
            result.CategoryGroupTransferID = connection.Query<long>("SELECT categoryGroupTableID FROM CATEGORYGROUPTABLE WHERE categoryGroupName = 'Transfer'")
                .Single();
            result.CategoryGroupIncomeID = connection.Query<long>("SELECT categoryGroupTableID FROM CATEGORYGROUPTABLE WHERE categoryGroupName = 'Income'")
                .Single();
            result.CategoryGroupExpenseID = connection.Query<long>("SELECT categoryGroupTableID FROM CATEGORYGROUPTABLE WHERE categoryGroupName = 'Expense'")
                .Single();
            result.OthersExpenseParentCatID = connection.Query<long>(
                "SELECT parentCategoryTableID FROM PARENTCATEGORYTABLE WHERE parentCategoryName = 'Others' AND categoryGroupID = @id",
                new { id = result.CategoryGroupExpenseID }).Single();
            result.OthersIncomeParentCatID = connection.Query<long>(
                "SELECT parentCategoryTableID FROM PARENTCATEGORYTABLE WHERE parentCategoryName = 'Others' AND categoryGroupID = @id",
                new { id = result.CategoryGroupIncomeID }).Single();

            result.NewAccountCatID = connection.Query<CHILDCATEGORYTABLE>(
                        "SELECT * FROM CHILDCATEGORYTABLE WHERE childCategoryName = '(New Account)'").Single().categoryTableID;
            result.TransferCatID = connection.Query<CHILDCATEGORYTABLE>(
                "SELECT * FROM CHILDCATEGORYTABLE WHERE childCategoryName = '(Transfer)'").Single().categoryTableID;

            return result;
        }

        private long ResolveChildCategoryGroup(Category category, Database database, SpecialCategories specialCategories)
        {
            var categoryTransactions = database.Transactions
                .Where(t => t.Category == category)
                .Where(t => t.Type != TransactionType.Transfer).
                ToList();

            if (!categoryTransactions.Any())
            {
                return specialCategories.CategoryGroupTransferID;
            }

            var groupsByType = categoryTransactions.GroupBy(t => t.Type).Where(g => g.Any());
            if (groupsByType.Count() != 1)
            {
                throw new Exception($"Child category {category.Name} contains transactions of mixed types. Can't import this category.");
            }

            var transactionType = groupsByType.Single().Key;
            if (transactionType == TransactionType.Adjustment) throw new Exception("Ballance adjustments can't belong to a category.");

            if (transactionType == TransactionType.Expense) return specialCategories.CategoryGroupExpenseID;
            if (transactionType == TransactionType.Income) return specialCategories.CategoryGroupIncomeID;

            return specialCategories.CategoryGroupTransferID;
        }

        private long ResolveParentCategoryGroup(Category category, Database database, SpecialCategories specialCategories)
        {
            var childGroups = database.Categories.Where(c => c.Parent == category).Select(c => ResolveChildCategoryGroup(c, database, specialCategories)).Distinct().ToList();
            if (childGroups.Count == 0) return specialCategories.CategoryGroupTransferID;

            if (childGroups.Count == 1) return childGroups.Single();

            throw new Exception($"Parent category {category.Name} contains transactions of mixed types. Can't import this category.");
        }

        private long? ResolveParentCategory(Category category, Database database, SpecialCategories specialCategories)
        {
            if (category.Parent == null)
            {
                var groupdID = ResolveChildCategoryGroup(category, database, specialCategories);
                if (groupdID == specialCategories.CategoryGroupExpenseID) return specialCategories.OthersExpenseParentCatID;
                if (groupdID == specialCategories.CategoryGroupIncomeID) return specialCategories.OthersIncomeParentCatID;
                throw new Exception($"Category {category} has no parent and it's type could not be resolved basing on transactions.");
            }
            else
            {
                return null;
            }
        }
    }
}
