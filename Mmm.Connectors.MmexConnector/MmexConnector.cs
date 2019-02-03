using Dapper;
using Mmm.Connectors.Api;
using Mmm.Connectors.MmexConnector.Data;
using Mmm.Domain;
using System;
using System.Data.SQLite;
using System.Linq;

namespace Mmm.Connectors.MmexConnector
{
    public class MmexConnector : IConnector
    {
        public Database ReadDatabase(string file)
        {
            var result = new Database();

            var builder = new SQLiteConnectionStringBuilder();
            builder.DataSource = file;
            using (var connection = new SQLiteConnection(builder.ConnectionString))
            {
                var accounts = connection.Query<ACCOUNTLIST>("SELECT * FROM ACCOUNTLIST_V1");
                var transactions = connection.Query<CHECKINGACCOUNT>("SELECT * FROM CHECKINGACCOUNT_V1");
                var subcategories = connection.Query<SUBCATEGORY>("SELECT * FROM SUBCATEGORY_V1");
                var categories = connection.Query<CATEGORY>("SELECT * FROM CATEGORY_V1");
                var currencies = connection.Query<CURRENCYFORMATS>("SELECT * FROM CURRENCYFORMATS_V1");

                result.Accounts = accounts.Select(a => a.MmmAccount = new Account
                {
                    Name = a.ACCOUNTNAME,
                    CurrencyCode = currencies.Single(c => c.CURRENCYID == a.CURRENCYID).CURRENCY_SYMBOL
                }).ToList();

                result.Categories = categories.Select(c => new Category { Name = c.CATEGNAME }).ToList();

                result.Categories.AddRange(from sc in subcategories
                                           let pc = categories.Single(c => c.CATEGID == sc.CATEGID)
                                           let p = result.Categories.Single(c => c.Name == pc.CATEGNAME)
                                           select new Category { Name = sc.SUBCATEGNAME, Parent = p });

                Category getCategory (int categid, int subcategid)
                {
                    if (categid <= 0) return null;

                    var category = categories.Single(c => c.CATEGID == categid);

                    if (subcategid <= 0) return result.Categories.Single(c => c.Name == category.CATEGNAME && c.Parent == null);

                    var parent = result.Categories.Single(c => c.Name == category.CATEGNAME && c.Parent == null);
                    var subcategory = subcategories.Single(c => c.SUBCATEGID == subcategid);

                    return result.Categories.Single(c => c.Name == subcategory.SUBCATEGNAME && c.Parent == parent);
                };

                Account getAccount(int accountid)
                {
                    if (accountid <= 0) return null;

                    var account = accounts.Single(a => a.ACCOUNTID == accountid);
                    return result.Accounts.Single(a => a.Name == account.ACCOUNTNAME);
                }

                Transaction getTransaction(CHECKINGACCOUNT t)
                {
                    if (t.TRANSCODE == "Deposit")
                    {
                        return new Transaction
                        {
                            Category = getCategory(t.CATEGID, t.SUBCATEGID),
                            ToAccount = getAccount(t.ACCOUNTID),
                            ToAmount = t.TRANSAMOUNT,
                            Notes = t.NOTES,
                            Date = t.TRANSDATE
                        };
                    }

                    if (t.TRANSCODE == "Withdrawal")
                    {
                        return new Transaction
                        {
                            Category = getCategory(t.CATEGID, t.SUBCATEGID),
                            FromAccount = getAccount(t.ACCOUNTID),
                            FromAmount = t.TRANSAMOUNT,
                            Notes = t.NOTES,
                            Date = t.TRANSDATE
                        };
                    }

                    if (t.TRANSCODE == "Transfer")
                    {
                        return new Transaction
                        {
                            Category = getCategory(t.CATEGID, t.SUBCATEGID),
                            FromAccount = getAccount(t.ACCOUNTID),
                            ToAccount = getAccount(t.TOACCOUNTID),
                            FromAmount = t.TRANSAMOUNT,
                            ToAmount = t.TOTRANSAMOUNT,
                            Notes = t.NOTES,
                            Date = t.TRANSDATE
                        };
                    }

                    throw new Exception($"Unknown TRANSCODE {t.TRANSCODE}");
                }

                result.Transactions = transactions.Select(getTransaction).ToList();

                foreach (var account in accounts)
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
