using Dapper.Contrib.Extensions;
using Mmm.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace Mmm.Connectors.BluecoinConnector.Data
{
    [Table(nameof(CHILDCATEGORYTABLE))]
    class CHILDCATEGORYTABLE
    {
        [ExplicitKey]
        public long categoryTableID { get; set; }
        public string childCategoryName { get; set; }
        public long parentCategoryId { get; set; }
        public string childCategoryIcon { get; set; }

        [Computed]
        public Category MmmCategory { get; set; }
    }
}
