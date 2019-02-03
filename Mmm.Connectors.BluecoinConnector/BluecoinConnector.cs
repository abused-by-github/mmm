using Dapper;
using Dapper.Contrib.Extensions;
using Mmm.Connectors.Api;
using Mmm.Connectors.BluecoinConnector.Data;
using Mmm.Domain;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;

namespace Mmm.Connectors.BluecoinConnector
{
    public class BluecoinConnector : IConnector
    {
        private const decimal TransactionAmountFactor = 1000000;

        private readonly CurrencyExchange _currencyExchange;

        public BluecoinConnector(CurrencyExchange currencyExchange)
        {
            _currencyExchange = currencyExchange;
        }

        public Database ReadDatabase(string file)
        {
            throw new NotImplementedException();
        }

        public void WriteDatabase(Database database, string file)
        {
            var builder = new SQLiteConnectionStringBuilder();
            builder.DataSource = file;
            using (var connection = new SQLiteConnection(builder.ConnectionString))
            {
                connection.Open();

                long nextID<T>()
                {
                    var keyProp = typeof(T).GetProperties().Where(prop => Attribute.IsDefined(prop, typeof(ExplicitKeyAttribute))).Single();
                    var maxID = connection.Query<long>($"SELECT MAX({keyProp.Name}) FROM {typeof(T).Name}").Single();
                    return maxID + 1;
                };

                using (var sqliteTransaction = connection.BeginTransaction())
                {
                    var defaultCurrencySetting = connection.Get<SETTINGSTABLE>(1);
                    if (defaultCurrencySetting == null || string.IsNullOrWhiteSpace(defaultCurrencySetting.defaultSettings) || defaultCurrencySetting.defaultSettings.Length != 3)
                    {
                        throw new Exception("Could not find default currency setting.");
                    }

                    var defaultCurrency = defaultCurrencySetting.defaultSettings;

                    var accountGroupAssets = connection.Query<ACCOUNTINGGROUPTABLE>(
                        "SELECT * FROM ACCOUNTINGGROUPTABLE WHERE accountGroupName = 'Assets'").Single();
                    var accountTypeCash = connection.Query<ACCOUNTTYPETABLE>(
                        "SELECT * FROM ACCOUNTTYPETABLE WHERE accountTypeName = 'Cash' AND accountingGroupID = @gid",
                        new { gid = accountGroupAssets.accountingGroupTableID }).Single();

                    #region accounts

                    var accounts = database.Accounts.Select(a => new ACCOUNTSTABLE
                    {
                        MmmAccount = a,
                        accountName = a.Name,
                        accountCurrency = a.CurrencyCode,
                        accountTypeID = accountTypeCash.accountTypeTableID,
                        accountConversionRateNew = _currencyExchange.GetRate(defaultCurrency, a.CurrencyCode)
                    }).ToList();

                    foreach (var account in accounts)
                    {
                        var existingAccount = connection.Query<ACCOUNTSTABLE>(
                            "SELECT * FROM ACCOUNTSTABLE WHERE accountName = @accountName AND accountTypeID = @accountTypeID", account).SingleOrDefault();

                        if (existingAccount == null)
                        {
                            account.accountsTableID = nextID<ACCOUNTSTABLE>();
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

                    #endregion

                    #region categories

                    var categoryGroupTransferID = connection.Query<long>("SELECT categoryGroupTableID FROM CATEGORYGROUPTABLE WHERE categoryGroupName = 'Transfer'").Single();
                    var categoryGroupIncomeID = connection.Query<long>("SELECT categoryGroupTableID FROM CATEGORYGROUPTABLE WHERE categoryGroupName = 'Income'").Single();
                    var categoryGroupExpenseID = connection.Query<long>("SELECT categoryGroupTableID FROM CATEGORYGROUPTABLE WHERE categoryGroupName = 'Expense'").Single();

                    var othersExpenseParentCatID = connection.Query<long>(
                        "SELECT parentCategoryTableID FROM PARENTCATEGORYTABLE WHERE parentCategoryName = 'Others' AND categoryGroupID = @id",
                        new { id = categoryGroupExpenseID }).Single();

                    var othersIncomeParentCatID = connection.Query<long>(
                        "SELECT parentCategoryTableID FROM PARENTCATEGORYTABLE WHERE parentCategoryName = 'Others' AND categoryGroupID = @id",
                        new { id = categoryGroupIncomeID }).Single();

                    long resolveChildCategoryGroup(Category category)
                    {
                        var categoryTransactions = database.Transactions
                            .Where(t => t.Category == category)
                            .Where(t => t.Type != TransactionType.Transfer).
                            ToList();

                        if (!categoryTransactions.Any())
                        {
                            return categoryGroupTransferID;
                        }

                        var groupsByType = categoryTransactions.GroupBy(t => t.Type).Where(g => g.Any());
                        if (groupsByType.Count() != 1)
                        {
                            throw new Exception($"Child category {category.Name} contains transactions of mixed types. Can't import this category.");
                        }

                        var transactionType = groupsByType.Single().Key;
                        if (transactionType == TransactionType.Adjustment) throw new Exception("Ballance adjustments can't belong to a category.");

                        if (transactionType == TransactionType.Expense) return categoryGroupExpenseID;
                        if (transactionType == TransactionType.Income) return categoryGroupIncomeID;

                        return categoryGroupTransferID;
                    };

                    long resolveParentCategoryGroup(Category category)
                    {
                        var childGroups = database.Categories.Where(c => c.Parent == category).Select(resolveChildCategoryGroup).Distinct().ToList();
                        if (childGroups.Count == 0) return categoryGroupTransferID;

                        if (childGroups.Count == 1) return childGroups.Single();

                        throw new Exception($"Parent category {category.Name} contains transactions of mixed types. Can't import this category.");
                    }

                    var parentCategories = database.Categories
                        .Where(c => c.Parent == null && database.Categories.Select(cc => cc.Parent).Contains(c))
                        .Select(c => new PARENTCATEGORYTABLE
                        {
                            categoryGroupId = resolveParentCategoryGroup(c),
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
                            category.parentCategoryTableId = nextID<PARENTCATEGORYTABLE>();
                            connection.Insert(category);
                        }
                        else
                        {
                            category.parentCategoryTableId = existingCategory.parentCategoryTableId;
                        }
                    }

                    long resolveParentCategory(Category category)
                    {
                        if (category.Parent == null)
                        {
                            var groupdID = resolveChildCategoryGroup(category);
                            if (groupdID == categoryGroupExpenseID) return othersExpenseParentCatID;
                            if (groupdID == categoryGroupIncomeID) return othersIncomeParentCatID;
                            throw new Exception($"Category {category} has no parent and it's type could not be resolved basing on transactions.");
                        }
                        else
                        {
                            return parentCategories.Where(pc => pc.MmmCategory == category.Parent).Single().parentCategoryTableId;
                        }
                    }

                    var childCategories = database.Categories
                        .Where(c => c.Parent != null || !database.Categories.Select(cc => cc.Parent).Contains(c))
                        .Select(c => new CHILDCATEGORYTABLE
                        {
                            parentCategoryId = resolveParentCategory(c),
                            childCategoryName = c.Name,
                            childCategoryIcon = "xxx_more_horiz_black_24dp",
                            MmmCategory = c
                        })
                        .ToList();

                    //Bluecoin does not support using parent categories in transations
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
                        childCategories.Add(child);
                    }

                    foreach (var category in childCategories)
                    {
                        var existingCategory = connection.Query<CHILDCATEGORYTABLE>(
                            "SELECT * FROM CHILDCATEGORYTABLE WHERE childCategoryName = @childCategoryName", category).SingleOrDefault();

                        if (existingCategory == null)
                        {
                            category.categoryTableID = nextID<CHILDCATEGORYTABLE>();
                            connection.Insert(category);
                        }
                        else
                        {
                            category.categoryTableID = existingCategory.categoryTableID;
                        }
                    }

                    #endregion

                    #region transactions

                    var newAccountCategory = connection.Query<CHILDCATEGORYTABLE>(
                        "SELECT * FROM CHILDCATEGORYTABLE WHERE childCategoryName = '(New Account)'").Single().categoryTableID;
                    var transferCategory = connection.Query<CHILDCATEGORYTABLE>(
                        "SELECT * FROM CHILDCATEGORYTABLE WHERE childCategoryName = '(Transfer)'").Single().categoryTableID;

                    var tranTypeNewAccount = connection.Query<TRANSACTIONTYPETABLE>(
                        "SELECT * FROM TRANSACTIONTYPETABLE WHERE transactionTypeName = 'New Account'").Single().transactionTypeTableID;
                    var tranTypeExpense = connection.Query<TRANSACTIONTYPETABLE>(
                        "SELECT * FROM TRANSACTIONTYPETABLE WHERE transactionTypeName = 'Expense'").Single().transactionTypeTableID;
                    var tranTypeIncome = connection.Query<TRANSACTIONTYPETABLE>(
                        "SELECT * FROM TRANSACTIONTYPETABLE WHERE transactionTypeName = 'Income'").Single().transactionTypeTableID;
                    var tranTypeTransfer = connection.Query<TRANSACTIONTYPETABLE>("" +
                        "SELECT * FROM TRANSACTIONTYPETABLE WHERE transactionTypeName = 'Transfer'").Single().transactionTypeTableID;


                    foreach (var transaction in database.Transactions)
                    {
                        var dateFrom = transaction.Date.AddSeconds(-2);
                        var dateTo = transaction.Date.AddSeconds(2);
                        var amount = transaction.FromAmount == 0 ? transaction.ToAmount : transaction.FromAmount;
                        amount *= TransactionAmountFactor;
                        var currency = transaction.FromAccount?.CurrencyCode ?? transaction.ToAccount?.CurrencyCode ?? "*";
                        var existingTransaction = connection.Query<TRANSACTIONSTABLE>(
                            "SELECT * FROM TRANSACTIONSTABLE WHERE date BETWEEN @dateFrom AND @dateTo AND amount = @amount AND transactionCurrency = @currency",
                            new { dateFrom, dateTo, amount, currency }).FirstOrDefault();

                        if (existingTransaction != null) continue;

                        if (transaction.Type == TransactionType.Adjustment)
                        {
                            var item = new ITEMTABLE { itemTableID = nextID<ITEMTABLE>(), itemName = transaction.FromAccount.Name, MmmTransaction = transaction };
                            connection.Insert(item);

                            var tran = new TRANSACTIONSTABLE
                            {
                                itemID = item.itemTableID,
                                transactionTypeID = tranTypeNewAccount,
                                MmmTransaction = transaction,
                                categoryID = newAccountCategory,
                                accountID = accounts.Single(a => a.MmmAccount == transaction.FromAccount).accountsTableID,
                                accountPairID = accounts.Single(a => a.MmmAccount == transaction.FromAccount).accountsTableID,
                                notes = transaction.Notes,
                                status = 0, //TODO: meaning is not clear
                                deletedTransaction = 6, //TODO: meaning is not clear
                                conversionRateNew = 1,
                                amount = transaction.FromAmount * TransactionAmountFactor,
                                date = transaction.Date,
                                accountReference = 1
                            };

                            tran.transactionsTableID = tran.uidPairID = nextID<TRANSACTIONSTABLE>();
                            connection.Insert(tran);
                        }
                        if (transaction.Type == TransactionType.Transfer)
                        {
                            var item = new ITEMTABLE { itemName = transaction.FromAccount.Name, MmmTransaction = transaction };
                            var tran = new TRANSACTIONSTABLE
                            {
                                transactionTypeID = tranTypeTransfer,
                                MmmTransaction = transaction,
                                categoryID = transferCategory,
                                accountID = accounts.Single(a => a.MmmAccount == transaction.FromAccount).accountsTableID,
                                accountPairID = accounts.Single(a => a.MmmAccount == transaction.ToAccount).accountsTableID,
                                notes = transaction.Notes,
                                status = 0, //TODO: meaning is not clear
                                deletedTransaction = 6, //TODO: meaning is not clear
                                conversionRateNew = 1,
                                amount = -transaction.FromAmount * TransactionAmountFactor,
                                transactionCurrency = transaction.FromAccount.CurrencyCode,
                                date = transaction.Date,
                                accountReference = 2 //TODO
                            };

                            tran.itemID = item.itemTableID = nextID<ITEMTABLE>();
                            connection.Insert(item);
                            tran.transactionsTableID = tran.uidPairID = tran.transferGroupID = nextID<TRANSACTIONSTABLE>();
                            connection.Insert(tran);

                            var tran2 = new TRANSACTIONSTABLE
                            {
                                itemID = item.itemTableID,
                                transactionTypeID = tranTypeTransfer,
                                MmmTransaction = transaction,
                                categoryID = transferCategory,
                                accountID = accounts.Single(a => a.MmmAccount == transaction.ToAccount).accountsTableID,
                                accountPairID = accounts.Single(a => a.MmmAccount == transaction.FromAccount).accountsTableID,
                                notes = transaction.Notes,
                                status = 0, //TODO: meaning is not clear
                                deletedTransaction = 6, //TODO: meaning is not clear
                                conversionRateNew = transaction.FromAmount / transaction.ToAmount,
                                amount = transaction.FromAmount * TransactionAmountFactor,
                                transactionCurrency = transaction.ToAccount.CurrencyCode,
                                uidPairID = tran.transactionsTableID,
                                transferGroupID = tran.transactionsTableID,
                                date = transaction.Date,
                                accountReference = 2 //TODO
                            };
                            tran2.transactionsTableID = tran.uidPairID = nextID<TRANSACTIONSTABLE>();
                            connection.Insert(tran2);

                            connection.Update<TRANSACTIONSTABLE>(tran);
                        }

                        if (transaction.Type == TransactionType.Income)
                        {
                            var item = new ITEMTABLE { itemName = transaction.Notes, MmmTransaction = transaction };
                            var tran = new TRANSACTIONSTABLE
                            {
                                transactionTypeID = tranTypeIncome,
                                MmmTransaction = transaction,
                                categoryID = childCategories.Single(c => c.MmmCategory == transaction.Category).categoryTableID,
                                accountID = accounts.Single(a => a.MmmAccount == transaction.ToAccount).accountsTableID,
                                accountPairID = accounts.Single(a => a.MmmAccount == transaction.ToAccount).accountsTableID,
                                notes = transaction.Notes,
                                status = 0, //TODO: meaning is not clear
                                deletedTransaction = 6, //TODO: meaning is not clear
                                conversionRateNew = 1,
                                amount = transaction.ToAmount * TransactionAmountFactor,
                                transactionCurrency = transaction.ToAccount.CurrencyCode,
                                date = transaction.Date,
                                accountReference = 1 //TODO
                            };

                            tran.itemID = item.itemTableID = nextID<ITEMTABLE>();
                            connection.Insert(item);
                            tran.transactionsTableID = tran.uidPairID = nextID<TRANSACTIONSTABLE>();
                            connection.Insert(tran);
                        }

                        if (transaction.Type == TransactionType.Expense)
                        {
                            var item = new ITEMTABLE { itemName = transaction.Notes, MmmTransaction = transaction };
                            var tran = new TRANSACTIONSTABLE
                            {
                                transactionTypeID = tranTypeExpense,
                                MmmTransaction = transaction,
                                categoryID = childCategories.Single(c => c.MmmCategory == transaction.Category).categoryTableID,
                                accountID = accounts.Single(a => a.MmmAccount == transaction.FromAccount).accountsTableID,
                                accountPairID = accounts.Single(a => a.MmmAccount == transaction.FromAccount).accountsTableID,
                                notes = transaction.Notes,
                                status = 0, //TODO: meaning is not clear
                                deletedTransaction = 6, //TODO: meaning is not clear
                                conversionRateNew = 1,
                                amount = -transaction.FromAmount * TransactionAmountFactor,
                                transactionCurrency = transaction.FromAccount.CurrencyCode,
                                date = transaction.Date,
                                accountReference = 1 //TODO
                            };

                            tran.itemID = item.itemTableID = nextID<ITEMTABLE>();
                            connection.Insert(item);
                            tran.transactionsTableID = tran.uidPairID = nextID<TRANSACTIONSTABLE>();
                            connection.Insert(tran);
                        }
                    }

                    #endregion

                    sqliteTransaction.Commit();
                }
            }
        }
    }
}
