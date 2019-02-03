using System;
using System.Collections.Generic;
using System.Text;

namespace Mmm.Domain
{
    public class Category
    {
        public string Name;
        public Category Parent;

        public override string ToString() => Parent == null ? Name : $"{Parent.Name} / {Name}";
    }
}
