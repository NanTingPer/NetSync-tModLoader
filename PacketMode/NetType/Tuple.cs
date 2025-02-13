using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace GensokyoWPNACC.PacketMode.NetType
{
    public class FieldAttr<T1, T2>
    {
        public T1 Field { get; set; }
        public T2 Attr { get; set; }
        private FieldAttr()
        {
        }

        public static FieldAttr<T1, T2> CreateFieldAttr(T1 field, T2 attr)
        {
            return new FieldAttr<T1, T2> { Field = field, Attr = attr };
        }

    }

    public class PropertyAttr<T1, T2>
    {
        public T1 Property { get; set; }
        public T2 Attr { get; set; }
        private PropertyAttr()
        {
        }

        public static PropertyAttr<T1, T2> CreatePropertyAttr(T1 property, T2 attr)
        {
            return new PropertyAttr<T1, T2> { Property = property, Attr = attr };
        }

    }
}
