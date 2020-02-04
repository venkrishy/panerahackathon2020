using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Csharp_3d_viewer.Model
{
    public class ItemLocation
    {
        public string Item { get; set; }
        public int Container { get; set; }
        public Vector3 Location { get; set; }
    }
}
