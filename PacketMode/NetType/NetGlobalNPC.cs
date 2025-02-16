using GensokyoWPNACC.PacketMode.NetInterface;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using static GensokyoWPNACC.PacketMode.NetType.OneLoading;


namespace GensokyoWPNACC.PacketMode.NetType
{
    /// <summary>
    /// 步骤:
    ///     1. 获取全部GlobalNPC    InitializeTypes()
    ///     2. 获取全部GlobalNPC字段和属性(带特性)
    ///     3. 创建用户自定义字段字典
    ///     4. 创建用户自定义字段映射表
    /// </summary>
    public class NetGlobalNPC /*: Net<GlobalNPC, Attribute, Attribute>*/
    {
        private static NetGlobalNPC _netGlobalNPC;
        public static NetGlobalNPC GetNetGlobalNPC 
        {
            get
            {
                if (_netGlobalNPC == null)
                    return _netGlobalNPC = new NetGlobalNPC();
                return _netGlobalNPC;
            }
        }

        #region 属性
        /// <summary>
        /// GlobaleNPC的类型映射表
        /// </summary>
        public static Dictionary<int, Type> TypeMap { get; } = [];

        /// <summary>
        /// GlobaleNPC的类型映射表(逆映射)
        /// </summary>
        public static Dictionary<Type, int> TypeMapRev { get; } = [];

        /// <summary>
        /// GlobaleNPC的带特性字段/属性列表
        /// </summary>
        public static Dictionary<Type, List<Tuple<MemberInfo, HashSet<Type>>>> MemnerInfos { get; } = [];

        /// <summary>
        /// 给定类型所包含的全部用户自定义字段 以及需要同步的属性/字段
        /// </summary>
        public static Dictionary<Type, Dictionary<FieldInfo, List<MemberInfo>>> TypeUseFields { get; } = [];

        /// <summary>
        /// 用户自定义字段映射表 DType.FullName + FieldInfo.Name
        /// </summary>
        public static Dictionary<int, string> UseFieldMap { get; private set; } = [];

        /// <summary>
        /// 用户自定义字段映射表 DType.FullName + FieldInfo.Name
        /// </summary>
        public static Dictionary<string, int> UseFieldMapRev { get; } = [];

        /// <summary>
        /// 全部用户自定义字段 
        /// </summary>
        public static Dictionary<int, FieldInfo> UseFieldInfos { get; } = [];

        /// <summary>
        /// 用户自定义字段反字典
        /// </summary>
        public static Dictionary<FieldInfo, int> UseFieldInfosRev { get; } = [];

        public static Dictionary<Type, MethodInfo> GetGolabeNPCMethods { get; } = [];
        #endregion 属性

        private static bool IsStartThread = false;
        public static void StartGlobalNPCThread()
        {
            if (IsStartThread)
                return;
            IsStartThread = true;
            if (Main.netMode != NetmodeID.Server)
            {
                NetGlobalNPCThread ??= new Thread(NetGolabeNPCTask);
                NetGlobalNPCThread.IsBackground = true;
                NetGlobalNPCThread.Start();
            }
        }

        /// <summary>
        /// 消息处理方法
        /// </summary>
        public static void 占位符(BinaryReader reader, int whoAmI)
        {
            //第一个是whoAmI
            //类型值
            //字段值
            int npcWhoAmI = reader.ReadInt32();
            int gnpcTypeIndex = reader.ReadInt32();
            int useFieldIndex = reader.ReadInt32();

            NPC npc = null;

            try
            {
                npc = Main.npc[npcWhoAmI];
            }
            catch 
            {
                ClearStream(reader);
                return;
            }

            //获取GNPC Type
            TypeMap.TryGetValue(gnpcTypeIndex, out Type gNPCtype);
            //获取useField
            UseFieldInfos.TryGetValue(useFieldIndex, out FieldInfo useField);
            GetGolabeNPCMethods.TryGetValue(gNPCtype, out MethodInfo getGNPCmethodinfo);
            object[] objects = [npc, null];
            object isbool = getGNPCmethodinfo.Invoke(null, objects);
            object GNPC = objects[1];
            
            //获取字段列表
            if(!GetUseField(gNPCtype, useField.Name, out var list))
            {
                ClearStream(reader);
                return;
            }

            if(Main.netMode == NetmodeID.Server)
            {
                ModLoader.TryGetMod(typeof(NetGlobalNPC).FullName.Split(".")[0], out Mod mod);
                var pt = mod.GetPacket();
                pt.Write((int)NetMessageType.GNPC);
                pt.Write(npcWhoAmI);
                pt.Write(gnpcTypeIndex);
                pt.Write(useFieldIndex);
                foreach (var item in Reader(reader, GNPC, list))
                {
                    Write(pt, item);
                }
                pt.Send(-1, whoAmI);
            }
            else
            {
                foreach (var item in Reader(reader, GNPC, list));
            }
            
        }

        

        /// <summary>
        /// 用户NPC线程的委托方法
        /// </summary>
        private static void NetGolabeNPCTask()
        {
            while(true)
            {
                if (GensokyoWPNACC.cts.IsCancellationRequested)
                    break;
                Thread.Sleep(30);
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    continue;
                foreach(var npc in Main.ActiveNPCs)
                {
                    foreach (var kv in GetGolabeNPCMethods)
                    {
                        if (npc.whoAmI >= Main.npc.Length)
                            continue;

                        object[] objects = [npc, null];
                        object isbool = kv.Value.Invoke(null, objects);
                        if (!(bool)isbool)
                            continue;
                        object GNPC = objects[1];

                        //if (!npc.TryGetGlobalNPC<WPNACCNPC>(out var GNPC))
                        //    continue;
                        //int entityType, ReadOnlySpan< TGlobal > entityGlobals, TResult baseInstance, out TResult result
                        //npc.type == entityType
                        //entityGlobals == npc.EntityGlobals

                        Type gType = GNPC.GetType();
                        if (!GetUseFields(gType, out var fields))
                            continue;

                        foreach (var useField in fields)
                        {
                            if (!(bool)useField.GetValue(GNPC))
                                continue;
                            useField.SetValue(GNPC, false);

                            //为true 找到相应的字段
                            if (!GetUseField(gType, useField.Name, out var list))
                                continue;

                            ModLoader.TryGetMod(gType.Namespace.Split(".")[0], out Mod mod);

                            
                            UseFieldInfosRev.TryGetValue(useField, out int useFieldIndex);
                            TypeMapRev.TryGetValue(gType, out int gnpcTypeIndex);

                            ModPacket pt = mod.GetPacket();
                            pt.Write((int)NetMessageType.GNPC);
                            pt.Write(npc.whoAmI);
                            pt.Write(gnpcTypeIndex);    //类型值
                            pt.Write(useFieldIndex);    //字段值
                            Write(pt, GNPC, list);
                            pt.Send(-1, Main.myPlayer);
                        }

                    }
                }
            }
        }

        /// <summary>
        /// 给定GNPC类型，返回该类型的全部用户定义字段
        /// </summary>
        private static bool GetUseFields(Type type, out List<FieldInfo> fields)
        {
            fields = null;
            if (!TypeUseFields.TryGetValue(type, out var dic))
                return false;

            fields = [];
            foreach (var key in dic.Keys)
                fields.Add(key);
            return true;
        }

        private bool IsInitialize = false;
        private /*override*/ void InitializeData()
        {
            if (IsInitialize)
                return;
            IsInitialize = true;

            InitializeTypes();
            InitializeMemnerInfos();
        }
        
        /// <summary>
        /// 给定类型名，给定用户定义字段名，返回字段
        /// </summary>
        private static bool GetUseField(Type type, string fieldInfoName, out List<MemberInfo> list)
        {
            list = null;
            if(UseFieldMapRev.TryGetValue(type.FullName + fieldInfoName, out var index))
            {
                if (!UseFieldInfos.TryGetValue(index, out var fieldinfo))
                    return false;
                if (!TypeUseFields.TryGetValue(type, out var memberInfos))
                    return false;

                memberInfos.TryGetValue(fieldinfo, out list);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 创建用户自定义字段
        /// </summary>
        /// <typeparam name="GNPC"> 目标类型 </typeparam>
        /// <param name="useField"> 字段名 </param>
        public void CreateUseField<GNPC, FieldAttr, PropertAttr>(string useField)
        {
            if (!IsInitialize)
                InitializeData();

            Type type = typeof(GNPC);
            FieldInfo fieldInfo = type.GetField(useField, CanReflectionWell());//获取此用户自定义字段
            if (TypeUseFields.TryGetValue(type, out var dic)) //存在此类型的任意字段
            {
                MemnerInfos.TryGetValue(type, out var value);
                if (dic.TryGetValue(fieldInfo, out var memberInfos)) //TOOD全局型，要改再说
                {//存在此字段的列表
                    foreach (var tuple in value)
                    {
                        MemberInfo memberInfo = tuple.Item1;
                        HashSet<Type> attrTypes = tuple.Item2;
                        AddMemberInfo<FieldAttr, PropertAttr>(attrTypes, memberInfos, memberInfo);
                    }
                }else
                {//不存在此字段的列表 1.遍历MemberInfos列表 2.筛选符合项目
                    NoFieldInfoTask<FieldAttr, PropertAttr>(type, fieldInfo, dic);
                }
            }else
            {//直接就是不存在此类型
                Dictionary</*string*/FieldInfo, List<MemberInfo>> diccccc = [];
                NoFieldInfoTask<FieldAttr, PropertAttr>(type, fieldInfo, diccccc);
                TypeUseFields.Add(type, diccccc);
            }

            AddUseFieldMap(fieldInfo);
            StringToFieldInfoMap();
/*            AddUseFieldMap(fieldInfo);
            if (UseFieldInfosRev.Count == 0)
                UseFieldInfosRev.Add(fieldInfo, 0);

            if(!UseFieldInfosRev.TryGetValue(fieldInfo, out int a))
            {
                UseFieldInfosRev.Add(fieldInfo, UseFieldInfosRev.Values.Max() + 1);
            }
            UseFieldInfos.Clear();
            UseFieldInfosRev.MyReverse(UseFieldInfos);
*/
          }

        private void StringToFieldInfoMap()
        {
            UseFieldMapRev.Clear();
            UseFieldMap.Clear();
            foreach (var kv in UseFieldInfos)
            {
                FieldInfo fieldinfo = kv.Value;
                UseFieldMap.Add(kv.Key, fieldinfo.DeclaringType.FullName + fieldinfo.Name);
            }
            UseFieldMap.MyReverse(UseFieldMapRev);
        }

        /// <summary>
        /// 往用户定义字段映射表添加内容
        /// </summary>
        /// <param name="fieldInfo"></param>
        private void AddUseFieldMap(FieldInfo fieldInfo)
        {
            FieldInfo key = fieldInfo;
            if (UseFieldInfosRev.Count == 0)
                UseFieldInfosRev.Add(key, 0);

            if (!UseFieldInfosRev.TryGetValue(key, out int value))
            {
                UseFieldInfosRev.Add(key, (UseFieldInfosRev.Values.Max() + 1));
            }
            UseFieldInfos.Clear();
            UseFieldInfosRev.MyReverse(UseFieldInfos);
        }

        private void AddMemberInfo<FieldAttr, PropertAttr>(HashSet<Type> attrTypes, List<MemberInfo> memberInfos, MemberInfo memberInfo)
        {
            if (attrTypes.TryGetValue(typeof(FieldAttr), out Type d))
            {
                if (memberInfos.FirstOrDefault(f => f == memberInfo) == null)
                {
                    memberInfos.Add(memberInfo);
                }
            }
            if (attrTypes.TryGetValue(typeof(PropertAttr), out Type e))
            {
                if (memberInfos.FirstOrDefault(f => f == memberInfo) == null)
                {
                    memberInfos.Add(memberInfo);
                }
            }
        }

        /// <summary>
        /// 给定类型，给定字段名称，给定字典，获取此类型的全部Memberinfo并将符合的加入到字典列表
        /// </summary>
        /// <param name="type"> 目标类型 </param>
        /// <param name="fieldInfo"> 要被加入字典的字段 </param>
        /// <param name="dic"> 目标字典 </param>
        private void NoFieldInfoTask<FieldAttr, PropertAttr>(Type type, FieldInfo fieldInfo, Dictionary<FieldInfo, List<MemberInfo>> dic)
        {
            List<MemberInfo> memberinfoslist = [];
            if (MemnerInfos.TryGetValue(type, out var wtf))
            {
                foreach (var tuple in wtf)  //遍历此类型的全部Memberinfo
                {
                    var memberInfo = tuple.Item1;
                    if (memberInfo is FieldInfo || memberInfo is PropertyInfo)
                    {
                        var attrTypes = tuple.Item2;
                        AddMemberInfo<FieldAttr, PropertAttr>(attrTypes, memberinfoslist, memberInfo);
                    }
                    
                }
            }
            dic.Add(fieldInfo, memberinfoslist);
        }

        /// <summary>
        /// 初始化类型映射表与逆字典
        /// </summary>
        private void InitializeTypes()
        {
            int num = 0;
            //MethodInfo method = typeof(NPC).GetMethods(CanReflectionWell()).FirstOrDefault(f => f.IsGenericMethod && f.Name == "TryGetGlobalNPC" && f.GetParameters().Length == 1);
            MethodInfo method = typeof(PacketMode.NetExtMethods).GetMethod("TryGetGlobalNPC", BindingFlags.Public | BindingFlags.Static);
            foreach (var type in GetExtendTypeClass(typeof(GlobalNPC)))
            {
                GetGolabeNPCMethods.Add(type, method.MakeGenericMethod(type));
                TypeMap.Add(num, type);
                num++;
            }
            TypeMap.MyReverse(TypeMapRev);


            //base.InitializeTypes();
        }

        /// <summary>
        /// 初始化字段 / 属性表
        /// </summary>
        private void InitializeMemnerInfos()
        {
            foreach (var Itype in TypeMap)
            {
                Type type = Itype.Value;
                List<Tuple<MemberInfo, HashSet<Type>>> list = [];
                var fields = type.GetFields(CanReflectionWell()).Where(f => f.GetCustomAttributes().Any());
                foreach (var field in fields)
                {
                    HashSet<Type> set = [];
                    foreach (Attribute attrs in field.GetCustomAttributes())
                    {
                        set.Add(attrs.GetType());
                    }
                    list.Add(Tuple.Create(field as MemberInfo, set));
                }

                var properties = type.GetProperties(CanReflectionWell()).Where(f => f.GetCustomAttributes().Any());
                foreach (var propertie in properties)
                {
                    HashSet<Type> set = [];
                    foreach (Attribute attrs in propertie.GetCustomAttributes())
                    {
                        set.Add(attrs.GetType());
                    }
                    list.Add(Tuple.Create(propertie as MemberInfo, set));
                }
                MemnerInfos.Add(type, list);
            }
        }
    }
}
