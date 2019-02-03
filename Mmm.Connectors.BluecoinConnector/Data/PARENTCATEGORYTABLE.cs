using Dapper.Contrib.Extensions;
using Mmm.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace Mmm.Connectors.BluecoinConnector.Data
{
    [Table(nameof(PARENTCATEGORYTABLE))]
    class PARENTCATEGORYTABLE
    {
        [ExplicitKey]
        public long parentCategoryTableId { get; set; }
        public string parentCategoryName { get; set; }
        public long categoryGroupId { get; set; }

        [Computed]
        public Category MmmCategory { get; set; }
    }
}
