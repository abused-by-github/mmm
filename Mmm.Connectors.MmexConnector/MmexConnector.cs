using Dapper;
using Mmm.Connectors.Api;
using Mmm.Connectors.MmexConnector.Data;
using Mmm.Domain;
using System;
using System.Data;
using System.Data.SQLite;
using System.Linq;

namespace Mmm.Connectors.MmexConnector
{
    public class MmexConnector : IConnector
    {
        private MmexDatabase LoadMmxDatabase(IDbConnection connection)
        {
            var result = new MmexDatabase();
            result.Accounts = connection.Query<ACCOUNTLIST>("SELECT * FROM ACCOUNTLIST_V1").ToList();
            result.Transactions = connection.Query<CHECKINGACCOUNT>("SELECT * FROM CHECKINGACCOUNT_V1").ToList();
            result.Subcategories = connection.Query<SUBCATEGORY>("SELECT * FROM SUBCATEGORY_V1").ToList();
            result.Categories = connection.Query<CATEGORY>("SELECT * FROM CATEGORY_V1").ToList();
            result.Currencies = connection.Query<CURRENCYFORMATS>("SELECT * FROM CURRENCYFORMATS_V1").ToList();

            return result;
        }

        private Category GetCategory(Database database, MmexDatabase mmexDatabase, int categid, int subcategid)
        {
            if (categid <= 0) return null;

            var category = mmexDatabase.Categories.Single(c => c.CATEGID == categid);

            if (subcategid <= 0) return database.Categories.Single(c => c.Name == category.CATEGNAME && c.Parent == null);

            var parent = database.Categories.Single(c => c.Name == category.CATEGNAME && c.Parent == null);
            var subcategory = mmexDatabase.Subcategories.Single(c => c.SUBCATEGID == subcategid);

            return database.Categories.Single(c => c.Name == subcategory.SUBCATEGNAME && c.Parent == parent);
        }

        private Account GetAccount(Database database, MmexDatabase mmexDatabase, int accountid)
        {
            if (accountid <= 0) return null;

            var account = mmexDatabase.Accounts.Single(a => a.ACCOUNTID == accountid);
            return database.Accounts.Single(a => a.Name == account.ACCOUNTNAME);
        }

        private Transaction GetTransaction(Database database, MmexDatabase mmexDatabase, CHECKINGACCOUNT t)
        {
            if (t.TRANSCODE == "Deposit")
            {
                return new Transaction
                {
                    Category = GetCategory(database, mmexDatabase, t.CATEGID, t.SUBCATEGID),
                    ToAccount = GetAccount(database, mmexDatabase, t.ACCOUNTID),
                    ToAmount = t.TRANSAMOUNT,
                    Notes = t.NOTES,
                    Date = t.TRANSDATE
                };
            }

            if (t.TRANSCODE == "Withdrawal")
            {
                return new Transaction
                {
                    Category = GetCategory(database, mmexDatabase, t.CATEGID, t.SUBCATEGID),
                    FromAccount = GetAccount(database, mmexDatabase, t.ACCOUNTID),
                    FromAmount = t.TRANSAMOUNT,
                    Notes = t.NOTES,
                    Date = t.TRANSDATE
                };
            }

            if (t.TRANSCODE == "Transfer")
            {
                return new Transaction
                {
                    Category = GetCategory(database, mmexDatabase, t.CATEGID, t.SUBCATEGID),
                    FromAccount = GetAccount(database, mmexDatabase, t.ACCOUNTID),
                    ToAccount = GetAccount(database, mmexDatabase, t.TOACCOUNTID),
                    FromAmount = t.TRANSAMOUNT,
                    ToAmount = t.TOTRANSAMOUNT,
                    Notes = t.NOTES,
                    Date = t.TRANSDATE
                };
            }

            throw new Exception($"Unknown TRANSCODE {t.TRANSCODE}");
        }

        public Database ReadDatabase(string file)
        {
            var result = new Database();

            var builder = new SQLiteConnectionStringBuilder();
            builder.DataSource = file;

            using (var connection = new SQLiteConnection(builder.ConnectionString))
            {
                var mmexDb = LoadMmxDatabase(connection);

                result.Accounts = mmexDb.Accounts.Select(a => a.MmmAccount = new Account
                {
                    Name = a.ACCOUNTNAME,
                    CurrencyCode = mmexDb.Currencies.Single(c => c.CURRENCYID == a.CURRENCYID).CURRENCY_SYMBOL
                }).ToList();

                result.Categories = mmexDb.Categories.Select(c => new Category { Name = c.CATEGNAME }).ToList();

                result.Categories.AddRange(from sc in mmexDb.Subcategories
                                           let pc = mmexDb.Categories.Single(c => c.CATEGID == sc.CATEGID)
                                           let p = result.Categories.Single(c => c.Name == pc.CATEGNAME)
                                           select new Category { Name = sc.SUBCATEGNAME, Parent = p });

                result.Transactions = mmexDb.Transactions.Select(t => GetTransaction(result, mmexDb, t)).ToList();

                foreach (var account in mmexDb.Accounts)
                {
                    if (account.INITIALBAL > 0)
                    {
                        var firstTranDate = result.Transactions
                            .Where(t => t.FromAccount == account.MmmAccount || t.ToAccount == account.MmmAccount)
                            .OrderBy(t => t.Date)
                            .FirstOrDefault()
                            ?.Date;
                        var adjustmentDate = firstTranDate.HasValue ? firstTranDate.Value.AddHours(-1) : DateTime.Now;
                        result.Transactions.Add(new Transaction
                        {
                            Date = adjustmentDate,
                            FromAccount = account.MmmAccount,
                            ToAccount = account.MmmAccount,
                            FromAmount = account.INITIALBAL,
                            ToAmount = account.INITIALBAL,
                            Notes = "(Initial ballance)"
                        });
                    }
                }
            }

            return result;
        }

        public void WriteDatabase(Database database, string file)
        {
            throw new NotImplementedException();
        }
    }
}
