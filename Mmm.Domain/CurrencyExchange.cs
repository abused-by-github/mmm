using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Mmm.Domain
{
    public class CurrencyExchange
    {
        private Dictionary<string, decimal> _rates;

        public CurrencyExchange()
        {
            var feed = XElement.Load("https://www.ecb.europa.eu/stats/eurofxref/eurofxref-daily.xml");
            _rates = feed
                .Descendants(XName.Get("Cube", "http://www.ecb.int/vocabulary/2002-08-01/eurofxref"))
                .Where(e => e.Attribute("currency") != null)
                .ToDictionary(e => e.Attribute("currency").Value, e => decimal.Parse(e.Attribute("rate").Value));
        }

        public decimal GetRate(string currencyFrom, string currencyTo)
        {
            if (!_rates.ContainsKey(currencyFrom))
            {
                throw new Exception($"Unknown currency {currencyFrom}");
            }

            if (!_rates.ContainsKey(currencyTo))
            {
                throw new Exception($"Unknown currency {currencyTo}");
            }

            return _rates[currencyTo] / _rates[currencyFrom];
        }
    }
}
