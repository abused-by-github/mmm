# My Money Manager
My Money Manager - imports data from [MoneyManagerEx](https://www.moneymanagerex.org/) database file to [Bluecoins](https://www.bluecoinsapp.com/) database file.
Including:
- Accounts
- Categories and sub-categories
- Transactions

Labels and payees are not supported.

Usage:

`dotnet run -- --from-file "path\to\MoneyManagerEx.mmb" --from-format mmex --to-file "path\to\Bluecoins.fydb" --to-format bcoin`

__Create a backup of both files first!__
