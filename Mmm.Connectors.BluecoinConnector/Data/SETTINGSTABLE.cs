using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Mmm.Connectors.BluecoinConnector.Data
{
    [Table(nameof(SETTINGSTABLE))]
    class SETTINGSTABLE
    {
        [ExplicitKey]
        public long settingsTableID { get; set; }
        public string defaultSettings { get; set; }
    }
}
