using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IEDCollector.Lib
{
    internal interface IParsable<T>
    {
        T Parse(object o);
    }
}
