using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GensokyoWPNACC.PacketMode.Attributes
{
    [AttributeUsage(AttributeTargets.Field)]
    public class ProjectileNetFieldAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property)]
    public class ProjectileNetPropertyAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public class ItemNetFieldAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property)]
    public class ItemNetPropertyAttribute : Attribute { }
}
