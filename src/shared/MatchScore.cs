using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace shared
{
    public class MatchScore<ContainedType> where ContainedType : class
    {
        public double Hits { get; set; } = 0;
        public ContainedType? Details { get; set; }
    }
}