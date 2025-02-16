
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace GensokyoWPNACC.PacketMode.NetType
{
    public partial class OneLoading : ModSystem //进行一次性加载内容
    {
        public override void Load()
        {
            #region 流写入方法与流读取方法
            foreach (var item in typeof(BinaryReader).GetMethods(CanReflectionWell()).Where(method => (method.Name == "ReadInt16" || method.Name == "ReadInt32" || method.Name == "ReadInt64" || method.Name == "ReadUInt16" || method.Name == "ReadUInt32" || method.Name == "ReadUInt64" || method.Name == "ReadDouble" || method.Name == "ReadSingle" || method.Name == "ReadBoolean") && method.GetParameters().Length == 0))
            {
                ReaderMethodInfo.Add(item.ReturnType, item);
            }
            ReaderMethodInfo.Add(typeof(Vector2), typeof(Utils).GetMethod("ReadVector2", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));

            foreach (var item in typeof(BinaryWriter).GetMethods(CanReflectionWell()).Where(f => (f.Name == "Write" || f.Name == "WriteVector2") && f.GetParameters().Length == 1 && !f.GetParameters()[0].ParameterType.Name.Contains("ReadOnlySpan")))
            {
                WriterMethodInfo.Add(item.GetParameters()[0].ParameterType, item);
            }
            WriterMethodInfo.Add(typeof(Vector2), typeof(Utils).GetMethod("WriteVector2", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
            #endregion 流写入方法与流读取方法

            NetProjectile.NetProjectiles.InitializeData();
            var r =  NetPlayer.GetNetPlayer();

            r.CrateNetFP<WPNACCPlayer, PlayerTestAttrbute, PlayerTestAttrbute>("同步测试");
            r.LoadUseField();
            r.LoadUseCommandMap();

            var GNPC = NetGlobalNPC.GetNetGlobalNPC;
            GNPC.CreateUseField<WPNACCNPC, GNPCTestAttribute, GNPCTestAttribute>("NetAsyncField");


            base.Load();
        }

        public static Thread NetPlayerThread { get; set; } = null;
        public static Thread NetGlobalNPCThread { get; set; } = null;
        public override void PostAddRecipes()
        {
            NetPlayer.StartNetPlayer(); //启动线程
            NetGlobalNPC.StartGlobalNPCThread();
            base.PostAddRecipes();
        }


        /// <summary>
        /// 使用类型拿取方法，存放Reader的方法
        /// </summary>
        public static Dictionary<Type, MethodInfo> ReaderMethodInfo { get; } = [];

        /// <summary>
        /// 使用类型拿取方法，存放Writer的方法
        /// </summary>
        public static Dictionary<Type, MethodInfo> WriterMethodInfo { get; } = [];

        /// <summary>
        /// 获取万用反射条件
        /// </summary>
        public static BindingFlags CanReflectionWell()
        {
            return
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;
        }

        #region 工具方法 判断类型是否支持自动同步
        /// <summary>
        /// 给定类型 判断该类型是否支持自动同步
        /// </summary>
        public static bool TypeCheck(Type type)
        {
            return ReaderMethodInfo.TryGetValue(type, out var value);
        }

        /// <summary>
        /// 给定一个字段 判断该字段是否支持自动同步
        /// </summary>
        public static bool TypeCheck(FieldInfo fieldInfo)
        {
            return ReaderMethodInfo.TryGetValue(fieldInfo.FieldType, out var value);
        }

        /// <summary>
        /// 给定一个属性 判断该属性是否支持自动同步
        /// </summary>
        public static bool TypeCheck(PropertyInfo propertyInfo)
        {
            return ReaderMethodInfo.TryGetValue(propertyInfo.PropertyType, out var value);
        }
        #endregion 工具方法


        #region Reader的工具方法
        /// <summary>
        /// 给定流，使用读取的方式，全部释放
        /// </summary>
        public static void ClearStream(BinaryReader reader)
        {
            reader.BaseStream.Position = reader.BaseStream.Length;
        }


        /// <summary>
        /// 给定一个流，给定一个对象，给定一个字段列表，读取并设置对象相应字段的值，返回该值
        /// </summary>
        public static IEnumerable<object> Reader<T>(BinaryReader reader, object obj, IList<T> infoList) where T : MemberInfo
        {
            foreach (var item in infoList)
            {
                object value = null;
                if (reader.BaseStream.CanRead)
                {
                    if (item is FieldInfo filed)
                    {
                        Reader(reader, filed, out value);
                        filed.SetValue(obj, value);
                        yield return value;
                    }
                    else if (item is PropertyInfo property)
                    {
                        Reader(reader, property, out value);
                        property.SetValue(obj, value);
                        yield return value;
                    }

                    //if (GetReaderMethod(field, out MethodInfo readerMethod))
                    //{
                    //    if (Reader(reader, readerMethod, out object value))
                    //    {
                    //        field.SetValue(obj, value);
                    //        yield return value;
                    //    }
                    //}
                }
            }
        }


        /// <summary>
        /// 使用readerMethod读取一次值
        /// </summary>
        public static bool Reader(BinaryReader reader, MethodInfo readerMethod, out object value)
        {
            if (reader.BaseStream.CanRead)
            {
                if (readerMethod.GetParameters().Length != 0)
                    value = readerMethod.Invoke(reader, [reader]);
                else
                    value = readerMethod.Invoke(reader, []);

                return true;
            }
            value = null;
            return false;
        }

        /// <summary>
        /// 给定一个流，一个字段/属性，输出该流读取的值
        /// </summary>
        public static bool Reader<T>(BinaryReader reader, T info, out object value) where T : MemberInfo
        {
            if (reader.BaseStream.CanRead)
            {
                if(GetReaderMethod(info, out var readerMethod))
                {
                    Reader(reader, readerMethod, out value);
                    return true;
                }
            }
            value = null;
            return false;
        }


        /// <summary>
        /// 给定一个属性 获取适用于该属性的读取方法
        /// </summary>
        public static bool GetReaderMethod<T>(T info, out MethodInfo method) where T : MemberInfo
        {
            if (info is PropertyInfo property)
                ReaderMethodInfo.TryGetValue(property.PropertyType, out method);
            else if (info is FieldInfo field)
                ReaderMethodInfo.TryGetValue(field.FieldType, out method);
            else
                method = null;

            if (method == null)
                return false;
            return true;
        }

        #region delete
        /// <summary>
        /// 给定一个属性 获取适用于该属性的读取方法
        /// </summary>
        [Obsolete]
        public static bool GetReaderMethod(PropertyInfo propertyInfo, out MethodInfo method)
        {
            if (ReaderMethodInfo.TryGetValue(propertyInfo.PropertyType, out method))
                return true;

            method = null;
            return false;
        }

        /// <summary>
        /// 给定一个字段 获取适用于该字段的读取方法
        /// </summary>
        [Obsolete]
        public static bool GetReaderMethod(FieldInfo field, out MethodInfo method)
        {
            if (ReaderMethodInfo.TryGetValue(field.FieldType, out method))
                return true;

            method = null;
            return false;
        }
        #endregion delete
        #endregion Reader的工具方法

        #region Write的工具方法
        /// <summary>
        /// 给定一个流，一个对象，一个字段/属性列表，将该对象对应的字段/属性值写入流
        /// </summary>
        public static void Write<T>(BinaryWriter pt, object obj, IList<T> infoList) where T : MemberInfo//MemberInfo MemberInfo
        {
            foreach (var item in infoList)
            {
                object value;
                if (item is PropertyInfo property)
                    value = property.GetValue(obj);
                else if (item is FieldInfo field)
                    value = field.GetValue(obj);
                else
                    value = null;

                if (value != null)
                    Write(pt, value);
            }
        }



        /// <summary>
        /// 给定一个流，给定一个字段，将value写入流
        /// </summary>
        public static bool Write(BinaryWriter pt, FieldInfo field, object value)
        {
            if (WriterMethodInfo.TryGetValue(field.FieldType, out MethodInfo ptMethodInfo))
            {
                if (ptMethodInfo.GetParameters().Length > 1)
                    ptMethodInfo.Invoke(pt, [pt, value]);
                else
                    ptMethodInfo.Invoke(pt, [value]);

                return true;
            }
            return false;
        }

        /// <summary>
        /// 给定一个流，给定一个属性，将value写入流
        /// </summary>
        public static bool Write(BinaryWriter pt, PropertyInfo property, object value)
        {
            if (WriterMethodInfo.TryGetValue(property.PropertyType, out MethodInfo ptMethodInfo))
            {
                Write(ptMethodInfo, pt, value);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 传入一个流，一个值，将值写入流
        /// </summary>
        public static bool Write(BinaryWriter pt, object value)
        {
            if (WriterMethodInfo.TryGetValue(value.GetType(), out MethodInfo ptMethodInfo))
            {
                Write(ptMethodInfo, pt, value);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 传入一个流，一个值，一个类型名称，将值写入流
        /// </summary>
        public static void Write(BinaryWriter pt, object value, string typeName)
        {
            switch (typeName)
            {
                case "Int32":
                    pt.Write((int)value);
                    break;

                case "Int64":
                    pt.Write((long)value);
                    break;

                case "Int16":
                    pt.Write((short)value);
                    break;

                case "UInt16":
                    pt.Write((ushort)value);
                    break;

                case "UInt32":
                    pt.Write((uint)value);
                    break;

                case "UInt64":
                    pt.Write((ulong)value);
                    break;

                case "Single":
                    pt.Write((float)value);
                    break;

                case "Double":
                    pt.Write((double)value);
                    break;

                case "Vector2":
                    pt.WriteVector2((Vector2)value);
                    break;
            }
        }

        /// <summary>
        /// 给定方法，流，值，调用
        /// </summary>
        private static void Write(MethodInfo method, BinaryWriter pt, object value)
        {
            if (method.GetParameters().Length > 1)
                method.Invoke(pt, [pt, value]);
            else
                method.Invoke(pt, [value]);
        }
        #endregion Write的工具方法

        #region 反射的辅助方法
        /// <summary>
        /// 获取继承给定类型的全部类型
        /// </summary>
        public static IEnumerable<Type> GetExtendTypeClass(Type type)
        {
            Type[] types = AssemblyManager.GetLoadableTypes(typeof(OneLoading).Assembly);
            return types.Where(typer => typer.IsSubclassOf(type) /*type.BaseType == type.GetType()*/);
        }


        /// <summary>
        /// 给定一个字段集合，将字段加入给定集合 不会重复添加
        /// </summary>
        public static void AddFieldInfoToList(List<FieldInfo> infos, List<MemberInfo> list)
        {
            foreach (FieldInfo info in infos)
            {
                if (list.FirstOrDefault(f =>
                {
                    if (f is FieldInfo filed)
                        return filed == info;
                    return false;
                }) == null)
                    list.Add(info);
            }
        }

        /// <summary>
        /// 给定一个属性集合，将属性加入给定集合 不会重复添加
        /// </summary>
        public static void AddPropertyInfoToList(List<PropertyInfo> infos, List<MemberInfo> list)
        {
            foreach (PropertyInfo info in infos)
            {
                if (list.FirstOrDefault(f =>
                {
                    if (f is PropertyInfo property)
                        return property == info;
                    return false;
                }) == null)
                    list.Add(info);
            }
        }

        /// <summary>
        /// 给定一个类型，给定一个列表，将类型的字段加入列表
        /// </summary>
        public static void GetFields(Type type, IList<FieldInfo> list)
        {
            foreach (var field in type.GetFields(CanReflectionWell()))
            {
                if(list.FirstOrDefault(f => f == field) == null)
                    list.Add(field);
            }
        }

        /// <summary>
        /// 给定一个类型，给定一个列表，将类型的字段加入列表
        /// </summary>
        public static void GetFields(Type type, IList<MemberInfo> list)
        {
            foreach (var field in type.GetFields(CanReflectionWell()))
            {
                if (list.FirstOrDefault(f => f == field) == null)
                    list.Add(field);
            }
        }

        /// <summary>
        /// 给定一个类型，给定一个列表，将类型的字段加入列表
        /// </summary>
        public static void GetFields(Type type, ISet<FieldInfo> set)
        {
            foreach (var field in type.GetFields(CanReflectionWell()))
            {
                set.Add(field);
            }
        }

        /// <summary>
        /// 给定一个类型，给定一个字典，将类型的字段加入字典
        /// </summary>
        public static void GetFields(Type type, IDictionary<Type, List<MemberInfo>> dic, Func<MemberInfo, bool> where = null)
        {
            if (where == null)
                where = f => true;
            foreach (MemberInfo field in type.GetFields(CanReflectionWell()).Where(where))
            {
                if (dic.TryGetValue(type, out var value))
                {
                    if (value.FirstOrDefault(f => f == field) == null)
                    {
                        value.Add(field);
                    }
                }
                else
                {
                    var list = new List<MemberInfo>();
                    dic.Add(type, list);
                    GetFields(type, list);
                }
            }
        }

        /// <summary>
        /// 给定一个类型，给定一个字典，将类型的字段加入字典
        /// </summary>
        public static void GetFields(Type type, IDictionary<Type, List<FieldInfo>> dic, Func<FieldInfo, bool> where = null)
        {
            if (where == null)
                where = f => true;
            foreach (var field in type.GetFields(CanReflectionWell()).Where(where))
            {
                if(dic.TryGetValue(type, out var value))
                {
                    if(value.FirstOrDefault(f => f == field) == null)
                    {
                        value.Add(field);
                    }
                }
                else
                {
                    var list = new List<FieldInfo>();
                    dic.Add(type, list);
                    GetFields(type, list);
                }
            }
        }

        /// <summary>
        /// 给定一个类型，给定一个字典，将类型的属性加入字典
        /// </summary>
        public static void GetPropertys(Type type, IDictionary<Type, List<MemberInfo>> dic, Func<PropertyInfo, bool> where = null)
        {
            if (where == null)
                where = f => true;
            foreach (var property in type.GetProperties(CanReflectionWell()).Where(where))
            {
                if (dic.TryGetValue(type, out var value))
                {
                    if (value.FirstOrDefault(f => f == property) == null)
                    {
                        value.Add(property);
                    }
                }
                else
                {
                    var list = new List<MemberInfo>();
                    dic.Add(type, list);
                    GetPropertys(type, list);
                }
            }
        }



        /// <summary>
        /// 给定一个类型，给定一个字典，将类型的属性加入字典
        /// </summary>
        public static void GetPropertys(Type type, IDictionary<Type, List<PropertyInfo>> dic, Func<PropertyInfo, bool> where = null)
        {
            if (where == null)
                where = f => true;
            foreach (var property in type.GetProperties(CanReflectionWell()).Where(where))
            {
                if (dic.TryGetValue(type, out var value))
                {
                    if (value.FirstOrDefault(f => f == property) == null)
                    {
                        value.Add(property);
                    }
                }
                else
                {
                    var list = new List<PropertyInfo>();
                    dic.Add(type, list);
                    GetPropertys(type, list);
                }
            }
        }

        /// <summary>
        /// 给定一个类型，给定一个列表，将类型的属性加入列表
        /// </summary>
        public static void GetPropertys(Type type, IList<MemberInfo> list)
        {
            foreach (var property in type.GetProperties(CanReflectionWell()))
            {
                if (list.FirstOrDefault(f => f == property) == null)
                    list.Add(property);
            }
        }

        /// <summary>
        /// 给定一个类型，给定一个列表，将类型的属性加入列表
        /// </summary>
        public static void GetPropertys(Type type, IList<PropertyInfo> list)
        {
            foreach (var property in type.GetProperties(CanReflectionWell()))
            {
                if (list.FirstOrDefault(f => f == property) == null)
                    list.Add(property);
            }
        }

        /// <summary>
        /// 给定一个类型，给定一个列表，将类型的属性加入列表
        /// </summary>
        public static void GetPropertys(Type type, ISet<PropertyInfo> set)
        {
            foreach (var property in type.GetProperties(CanReflectionWell()))
            {
                set.Add(property);
            }
        }

        /// <summary>
        /// 给定一个类型，给定一个非泛型列表，将该类型的字段和属性加入列表
        /// </summary>
        public static void GetFAP(Type type, System.Collections.IList list)
        {
            foreach (var field in type.GetFields(CanReflectionWell()))
            {
                list.Add(field);
            }
            foreach (var property in type.GetProperties(CanReflectionWell()))
            {
                list.Add(property);
            }
        }
        #endregion 获取类型的字段和属性 并加入
    }
}
