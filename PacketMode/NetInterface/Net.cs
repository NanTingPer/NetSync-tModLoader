using GensokyoWPNACC.PacketMode.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;
using static GensokyoWPNACC.PacketMode.NetType.OneLoading;

namespace GensokyoWPNACC.PacketMode.NetInterface
{
    public abstract class Net<MType, FieldAttribute, PropertyAttribute> : INet 
        where FieldAttribute : Attribute 
        where PropertyAttribute : Attribute 
        where MType : ModType
    {
        private bool IsInitialize = false;
        public List<Tuple<int, Type>> Types { get; set; } = [];
        public Dictionary<Type, List<FieldInfo>> Fields { get; set; } = [];
        public Dictionary<Type, List<PropertyInfo>> Propertys { get; set; } = [];

        /// <summary>
        /// 初始化类型 属性 字段 列表
        /// </summary>
        public virtual void InitializeData()
        {
            if (IsInitialize)
                return;
            IsInitialize = true;
            InitializeTypes();
            InitializeFAP();
        }

        public abstract void WritePackets();
       
        /// <summary>
        /// 初始化类型列表
        /// </summary>
        protected void InitializeTypes()
        {
            Type type = GetType();
            if (type != null)
            {
                int typeProjectileCount = 0;
                //typeof(Mod).Assembly
                foreach (Type item in AssemblyManager.GetLoadableTypes(type.Assembly).Where(type => type.IsSubclassOf(typeof(MType))/* || type == typeof(MType)*/))
                {
                    Types.Add(new Tuple<int, Type>(typeProjectileCount, item));
                    typeProjectileCount++;
                };
            }

            int a = 0;
            Main.NewText(a);

        }

        /// <summary>
        /// 初始化属性和字段列表
        /// </summary>
        protected void InitializeFAP()
        {
            foreach (var tuple in Types)
            {
                Type type = tuple.Item2;
                var fields =
                    type
                        .GetFields(CanReflectionWell())
                        .Where(field => field.GetCustomAttribute<FieldAttribute>() != null && TypeCheck(field));
                if (fields.Count() >= 1)
                {
                    Fields.Add(type, fields.ToList());
                }
                var propertys =
                    type
                        .GetProperties(CanReflectionWell())
                        .Where(proper => proper.GetCustomAttribute<PropertyAttribute>() != null && TypeCheck(proper));
                if (propertys.Count() >= 1)
                {
                    Propertys.Add(type, propertys.ToList());
                }
            }
        }

        protected void SendMethod(object obj, BinaryWriter writer)
        {
            if(Fields.TryGetValue(obj.GetType(), out var value))
            {
                Write(writer, obj, value);
            }
            if(Propertys.TryGetValue(obj.GetType(), out var value2))
            {
                Write(writer, obj, value);
            }
        }

        protected void ReceiveMethod(object obj, BinaryReader reader)
        {
            if(Fields.TryGetValue(obj.GetType(), out var value))
            {
                foreach (var item in Reader(reader, obj, value));
            }
            if(Propertys.TryGetValue(obj.GetType(),out var value2))
            {
                foreach (var item in Reader(reader, obj, value));
            }
        }
    }

    public enum NetMessageType
    {
        Player = 1,
        GNPC = 2,
        杂项 = 999,
    }
}
