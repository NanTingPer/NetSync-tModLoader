using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Terraria.ModLoader;

namespace GensokyoWPNACC.PacketMode.NetInterface
{
    public interface INet
    {
        /// <summary>
        /// 你应当承载的类型
        /// </summary>
        List<Tuple<int, Type>> Types { get; set; }

        /// <summary>
        /// 你应当承载的字段
        /// </summary>
        Dictionary<Type, List<FieldInfo>> Fields { get; set; }

        /// <summary>
        /// 你应当承载的属性
        /// </summary>
        Dictionary<Type, List<PropertyInfo>> Propertys { get; set; }

        /// <summary>
        /// 初始化字段 / 属性 / 各种列表
        /// </summary>
        void InitializeData();

        /// <summary>
        /// 往模组包塞需要同步的东西
        /// </summary>
        void WritePackets(); 
    }
}
