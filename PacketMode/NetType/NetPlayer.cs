using GensokyoWPNACC.PacketMode.NetInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using static GensokyoWPNACC.PacketMode.NetType.OneLoading;
using System.IO;

namespace GensokyoWPNACC.PacketMode.NetType
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class PlayerTestAttrbute : Attribute { }


    /// <summary>
    /// NetPlayer
    /// </summary>
    /// <typeparam name="PT"> ModPlayer的类型 全局直接传入ModPlayer </typeparam>
    /// <typeparam name="FIT"> 字段的特性 </typeparam>
    /// <typeparam name="POT"> 属性的特性 </typeparam>
    public sealed class NetPlayer/*<FIT, POT>*/ /*: Net<ModPlayer, Attribute, Attribute> */
        //where FIT : Attribute
        //where POT : Attribute
    {
        private NetPlayer() { }

        #region 属性
        private List<Tuple<int, Type>> Types { get; } = [];

        /// <summary>
        /// 给定类型的全部字段
        /// </summary>
        private Dictionary<Type, List<FieldInfo>> PAFields { get; set; } = [];

        /// <summary>
        /// 给定类型的全部属性
        /// </summary>
        private Dictionary<Type, List<PropertyInfo>> PAProperty { get; set; } = [];

        /// <summary>
        /// 字段特性
        /// </summary>
        public static HashSet<Type> FITAttribute { get; private set; } = [];

        /// <summary>
        /// 属性特性
        /// </summary>
        public static HashSet<Type> POTAttribute { get; private set; } = [];

        /// <summary>
        /// Player中(Key) 每个字段和其对应的特性
        /// <para> List中，每个元组都代表一个字段，元组的Attr是该字段的特性 </para>
        /// </summary>
        public static Dictionary<Type, List<FieldAttr<FieldInfo, HashSet<Type>>>> PlayerField { get; private set; } = [];

        /// <summary>
        /// Player中(Key) 每个属性和其对应的特性
        /// <para> List中，每个元组都代表一个属性，元组的Attr是该属性的特性 </para>
        /// </summary>
        public static Dictionary<Type, List<PropertyAttr<PropertyInfo, HashSet<Type>>>> PlayerProperty { get; private set; } = [];

        /// <summary>
        /// (用户自定义字段) Key是用户自定义字段所在的类和该字段的名称，值是该字段对应的同步属性/字段
        /// </summary>
        public static Dictionary<Tuple<Type, string>, List<MemberInfo>> UseNetFields { get; set; } = [];

        /// <summary>
        /// (用户自定义字段) Key是该字段存在的类，值是该类所有的同步字段
        /// </summary>
        public static Dictionary<Type, HashSet<string>> NetFieldsName { get; set; } = [];

        /// <summary>
        /// (用户自定义字段) Key是该字段存在的类，值是该类的同步字段
        /// </summary>
        public static Dictionary<Type, List<FieldInfo>> NetFields { get; set; } = [];

        /// <summary>
        /// 类型映射表，映射对象为ModPlyaer类型
        /// </summary>
        public static Dictionary<int, Type> TypeMap { get; set; } = [];

        /// <summary>
        /// TypeMap的逆字典，类型映射表，映射对象为ModPlyaer类型
        /// </summary>
        public static Dictionary<Type, int> TypeMapRev { get; set; } = [];

        /// <summary>
        /// 字段映射表，映射对象为用户自定义字段名称
        /// </summary>
        public static Dictionary<int, string> CommandMap { get; set; } = [];

        /// <summary>
        /// CommandMap的逆字典，字段映射表，映射对象为用户自定义字段名称
        /// </summary>
        public static Dictionary<string, int> CommandMapRev { get; set; } = [];

        /// <summary>
        /// 获取ModPlayer的方法字典，使用ModPlyaer的类型获取
        /// </summary>
        public static Dictionary<Type, MethodInfo> PlayerGenericMethod { get; set; } = [];
        #endregion 属性

        private static NetPlayer _netplayer = new NetPlayer();
        public static NetPlayer GetNetPlayer()
        {
            _netplayer?.InitializeData();
            return _netplayer;            
        }
        /*public override void WritePackets()
        {
            
        }*/

        private bool IsInitialize = false;

        /// <summary>
        /// 说明: 
        ///     1. 先调用父加载器，加载全部ModPlayer类型
        ///     2. 调用InitializeFPA方法，将全部的ModPlayer的字段和属性分别写入字典
        ///     3. 调用LoadFPA方法，从上一步的字段和属性中，将带有特效标记的字段/属性提取出来
        ///     4. 调用LoadPlayerGenericMethod方法，将全部ModPlayer相应的GetModPlayer提取出来
        ///     5. 调用InitializeTypeMap方法，初始化类型映射字典，用于联机同步的索引
        ///     6. 用户注册用户自定义字段 使用CrateNetFP方法
        ///     7. 用户自行调用LoadUseField方法 将创建的NetFP加载入字典
        ///     8. 用户自定调用LoadUseCommandMap方法，用于创建联机同步的索引映射字典
        /// </summary>
        public /*override*/ void InitializeData()
        {
            if (IsInitialize)
                return;
            IsInitialize = true;
            LoadTypes();
            //base.InitializeData();
            InitializeFPA();    //初始化全部字段与属性
            LoadFPA();          //加载全部字段和属性
            LoadPlayerGenericMethod();
            InitializeTypeMap();    //初始化映射表
            //LoadUseField() 手动调用
        }

        /// <summary>
        /// 处理玩家自动同步的包
        /// </summary>
        public static void 占位符(BinaryReader reader, int whoAmI)
        {
            //pt.Write((int)NetMessageType.Player);
            //pt.Write(Main.myPlayer);
            //pt.Write(typeIndex);
            //pt.Write(commandIndex);
            if (Main.netMode == NetmodeID.Server)
            {
                if (NetPlayerFinallyTask(reader, out List<int> server, out object player, out List<MemberInfo> list))
                {
                    ModPacket pt = ModLoader.GetMod(player.GetType().Namespace.Split(".")[0]).GetPacket();
                    pt.Write((int)NetMessageType.Player);
                    foreach (var index in server)
                    {
                        pt.Write(index);
                    }
                    foreach (var value in Reader(reader, player, list))
                    {
                        Write(pt, value);
                    }
                    pt.Send(-1, whoAmI);
                }
            }
            else//多人客户端
            {
                if (NetPlayerFinallyTask(reader, out List<int> server, out object player, out List<MemberInfo> list))
                {
                    //CombatText.NewText
                    //int num = 0; // todo delete
                    //Main.NewText("[c/80ffff:--------------------------------------]");
                    foreach (var value in Reader(reader, player, list))
                    {
                        //Main.NewText(list[num].Name + ": " + value);
                        //num++;
                    }

                    #region todo Delete Test
                    //todo Delete
                    /*
                    for (int i = 0; i < server.Count; i++)
                    {
                        if(i == 0)
                        {
                            Main.NewText($"玩家名称: {Main.player[server[i]].name}");
                        }else if (i == 1)
                        {
                            TypeMap.TryGetValue(server[i], out Type type);
                            Main.NewText($"ModPlayer类型名称: {type.Name}");
                        }else
                        {
                            CommandMap.TryGetValue(server[i], out string command);
                            Main.NewText($"用户自定义指令名称: {command}");
                        }
                    }
                    Main.NewText("[c/80ffff:--------------------------------------]");
                    */
                    #endregion todo Delete Test
                }
            }
        }

        /// <summary>
        /// 处理ModPlayer(NetPlayer)的包调用此方法 需要确保第一次reader出来的枚举是Plyaer
        /// <para> serverMust 如果是服务器中转，务必将此数组按照顺序写入模组包 </para> 
        /// <para> modPlayer 此模组包同步的玩家 </para>
        /// <para> lists 输出此模组包包含的字段/属性 </para>
        /// </summary>
        public static bool NetPlayerFinallyTask(BinaryReader reader, out List<int> serverMust,out object modPlayer, out List<MemberInfo> lists)
        {
            Type modPlyaerType = null;
            MethodInfo getPlyaerMethod = null;
            string command = null;
            modPlayer = null;
            lists = null;
            serverMust = []; 

            int playerIndex = reader.ReadInt32();
            int typeIndex = reader.ReadInt32();
            int commandIndex = reader.ReadInt32();
            Player player = Main.player.FirstOrDefault(f => f.whoAmI == playerIndex);//Main.player[playerIndex];
            if (player == null){
                ClearStream(reader); return false;
            }
            
            TypeMap.TryGetValue(typeIndex, out modPlyaerType);  //获取modPlayer类型
            PlayerGenericMethod.TryGetValue(modPlyaerType, out getPlyaerMethod); //获取get方法
            CommandMap.TryGetValue(commandIndex, out command);  //获取用户指令

            if (modPlyaerType == null && getPlyaerMethod == null && command == null){
                ClearStream(reader); return false;
            }

            UseNetFields.TryGetValue(Tuple.Create(modPlyaerType, command), out lists);  //获取属性列表

            if (lists == null){
                ClearStream(reader); return false;
            }
            serverMust.Add(playerIndex);
            serverMust.Add(typeIndex);
            serverMust.Add(commandIndex);
            modPlayer = getPlyaerMethod.Invoke(player, []); //获取ModPlayer Object
            return true;
        }

        private static MethodInfo GetModPlayerMethod { get; set; } = null;
        private static bool IsStartNetPlayer = false;
        /// <summary>
        /// 调用此方法，启动玩家同步线程
        /// </summary>
        public static void StartNetPlayer()
        {
            if (IsStartNetPlayer)
                return;

            IsStartNetPlayer = true;
            LoadGetPlayerMethod();
            if (Main.netMode != NetmodeID.Server)
            {
                NetPlayerThread ??= new Thread(NetPlayerTask);
                NetPlayerThread.IsBackground = true; //后台线程
                NetPlayerThread.Start();
            }
        }


        /// <summary>
        /// 用作线程的委托方法，时刻检查用户定义字段的值
        /// </summary>
        private static void NetPlayerTask()
        {
            while (true)
            {
                Thread.Sleep(17);
                if (GensokyoWPNACC.cts.IsCancellationRequested)
                    break;

                if (Main.netMode != NetmodeID.MultiplayerClient)
                    continue;

                Player player = Main.player[Main.myPlayer];
                if (player == null)
                    continue;
                //遍历
                foreach (var kv in NetFields)
                {
                    Type type = kv.Key;
                    List<FieldInfo> fields = kv.Value;
                    MethodInfo genericMethod = GetModPlayerMethod.MakeGenericMethod(type);
                    object modPlayer = genericMethod.Invoke(player, []);
                    foreach (var field in fields)
                    {
                        bool ovNet = (bool)field.GetValue(modPlayer);
                        if (!ovNet)
                            continue;

                        field.SetValue(modPlayer, false);
                        if (GetNetMemberInfo(modPlayer, field, out var memberinfos))
                        {
                            SendMemberInfo(modPlayer, memberinfos, field);
                        }
                    }
                }
            }        
        }

        /// <summary>
        /// 加载GetPlayerMethod的方法引用
        /// </summary>
        private static void LoadGetPlayerMethod()
        {
            if (GetModPlayerMethod == null)
            {
                GetModPlayerMethod = typeof(Player).GetMethods(CanReflectionWell())
                    .First
                    (method =>
                        method.Name == "GetModPlayer" &&
                        method.IsGenericMethod &&
                        method.GetParameters().Length == 0
                    );
            }
        }

        /// <summary>
        /// 加载ModPlayer类型对应的Get方法
        /// </summary>
        private void LoadPlayerGenericMethod()
        {
            LoadGetPlayerMethod();
            foreach (var IType in Types)
            {
                Type type = IType.Item2;
                PlayerGenericMethod.Add(type, GetModPlayerMethod.MakeGenericMethod(type));
            }
        }

        /// <summary>
        /// 给定一个ModPlayer，一个Field(用户自定义字段) 返回该字段对应的同步属性与字段
        /// </summary>
        private static bool GetNetMemberInfo(object modPlayer, FieldInfo useField, out List<MemberInfo> memberinfos)
        {
            if(UseNetFields.TryGetValue(Tuple.Create(modPlayer.GetType(), useField.Name), out memberinfos))
                return true;
            return false;
        }

        /// <summary>
        /// 给定一个ModPlyaer，一个MemberInfo(需要同步的属性与字段) 将该属性/字段列表写入包并发往服务器
        /// </summary>
        /// <param name="useField"> 用户自定义字段 </param>
        private static void SendMemberInfo(object modPlayer, List<MemberInfo> memberinfos, FieldInfo useField)
        {
            Type playerType = modPlayer.GetType();
            ModPacket pt = ModLoader.GetMod(modPlayer.GetType().Namespace.Split(".")[0]).GetPacket();
            if (pt == null) return;

            int typeIndex = -1;
            TypeMapRev.TryGetValue(playerType, out typeIndex);
            if (typeIndex == -1) return;

            int commandIndex = -1;
            CommandMapRev.TryGetValue(useField.Name, out commandIndex);
            if (typeIndex == -1) return;

            pt.Write((int)NetMessageType.Player);
            pt.Write(Main.myPlayer);
            pt.Write(typeIndex);
            pt.Write(commandIndex);
            Write(pt, modPlayer, memberinfos);
            pt.Send(-1, Main.myPlayer);
        }


        /// <summary>
        /// 载入用户自定义同步字段
        /// </summary>
        public void LoadUseField()
        {
            foreach (var kv in NetFieldsName)
            {
                Type type = kv.Key;
                HashSet<string> fieldNames = kv.Value;
                List<FieldInfo> fields = [];
                foreach (var item in fieldNames)
                {
                    fields.Add(type.GetField(item, CanReflectionWell()));
                }
                NetFields.Add(type, fields);
            }
        }


        /// <summary>
        /// 初始化类型映射表
        /// </summary>
        private void InitializeTypeMap()
        {
            HashSet<Type> set = [];
            foreach (var type in PlayerField)
            {
                set.Add(type.Key);
            }

            foreach (var type in PlayerProperty)
            {
                set.Add(type.Key);
            }
            int count = 0;
            foreach (var item in set.ToList())
            {
                TypeMap.Add(count, item);
                count++;
            }
        }

        private bool OFFLoadUseCommandMap = false;
        /// <summary>
        /// 加载用户自定义字段映射表
        /// </summary>
        public void LoadUseCommandMap()
        {
            if (OFFLoadUseCommandMap)
                throw new Exception("请不要重复加载用户自定义映射表");

            OFFLoadUseCommandMap = true;
            HashSet<string> set2 = [];
            foreach (var command in UseNetFields)
            {
                set2.Add(command.Key.Item2);
            }
            int count2 = 0;
            foreach (var com in set2)
            {
                CommandMap.Add(count2, com);
                count2++;
            }

            foreach (var item in CommandMap)
            {
                CommandMapRev.Add(item.Value, item.Key);
            }

            foreach (var item in TypeMap)
            {
                TypeMapRev.Add(item.Value, item.Key);
            }
        }

        /// <summary>
        /// 创建用户自定义同步字段
        /// </summary>
        /// <typeparam name="ModPlayer"> ModPlayer的类型 </typeparam>
        /// <typeparam name="FieldAttr"> 字段特性 </typeparam>
        /// <typeparam name="PropertyAttr"> 属性特性 </typeparam>
        /// <param name="fieldName"> 同步字段名称 </param>
        public void CrateNetFP<ModPlayer , FieldAttr, PropertyAttr>(string fieldName)
        {
            Type playerType = typeof(ModPlayer);
            if (NetFieldsName.TryGetValue(playerType, out var value))
            {
                value.Add(fieldName);
            }
            else
            {
                HashSet<string> set = [];
                NetFieldsName.Add(playerType, set);
                set.Add(fieldName);
            }

            if(UseNetFields.TryGetValue(Tuple.Create(playerType, fieldName), out var typeAndFP))
            {
                AddFieldInfoToList(GetTypeFields<FieldAttr>(playerType), typeAndFP);
                AddPropertyInfoToList(GetTypePropertys<PropertyAttr>(playerType), typeAndFP);
            }
            else
            {
                List<MemberInfo> list = [];
                UseNetFields.Add(Tuple.Create(playerType, fieldName), list);
                AddFieldInfoToList(GetTypeFields<FieldAttr>(playerType), list);
                AddPropertyInfoToList(GetTypePropertys<PropertyAttr>(playerType), list);
            }
        }


        /// <summary>
        /// 使用类型和特性，获取该类型下被给定特性标记的属性和字段(需要使用is / as)，适用于局部UseNet字段
        /// </summary>
        public static List<MemberInfo> GetTypeFAP<Attr>(Type type)
        {
            List<MemberInfo> list = [];
            list.AddRange(GetTypeFields<Attr>(type));
            list.AddRange(GetTypePropertys<Attr>(type));

            return list;
        }

        /// <summary>
        /// 获取给定类型被指定特性标记的字段
        /// </summary>
        public static List<FieldInfo> GetTypeFields<Attr>(Type type)
        {
            List<FieldInfo> list = [];
            if (PlayerField.TryGetValue(type, out var fieldAttrs))
            {
                foreach (var fieldAttr in fieldAttrs)
                {
                    if (fieldAttr.Attr.TryGetValue(typeof(Attr), out var eeee))
                        list.Add(fieldAttr.Field);
                }
            }
            return list;
        }

        /// <summary>
        /// 获取给定类型被指定特性标记的属性
        /// </summary>
        public static List<PropertyInfo> GetTypePropertys<Attr>(Type type)
        {
            List<PropertyInfo> list = [];
            if (PlayerProperty.TryGetValue(type, out var propertyAttrs))
            {
                foreach (var propertyAttr in propertyAttrs)
                {
                    if (propertyAttr.Attr.TryGetValue(typeof(Attr), out var eeee))
                        list.Add(propertyAttr.Property);
                }
            }
            return list;
        }

        /// <summary>
        /// 使用特性获取被该特性标记的字段，适用于全局UseNet字段
        /// </summary>
        public static List<Tuple<Type, FieldInfo>> GetTypeFields<Attr>()
        {
            List<Tuple<Type, FieldInfo>> list = [];
            foreach (var kv in PlayerField)
            {
                var fieldAttrs = kv.Value;
                foreach (var fieldAttr in fieldAttrs)
                {
                    if (fieldAttr.Attr.TryGetValue(typeof(Attr), out var attrrr))
                        list.Add(Tuple.Create(kv.Key, fieldAttr.Field));//attrrr命名无意义
                }
            }
            return list;
        }

        /// <summary>
        /// 载入全部字段和属性
        /// </summary>
        private void InitializeFPA()
        {
            foreach (var iType in Types)
            {
                var type = iType.Item2;
                OneLoading.GetFields(type, PAFields);
                GetPropertys(type, PAProperty);
            }
        }

        private void LoadTypes()
        {
            int num = 0;
            foreach (var type in GetExtendTypeClass(typeof(ModPlayer)))
            {
                Types.Add(Tuple.Create(num, type));
                num++;
            }
        }

        /// <summary>
        /// 载入 PlayerField 和 PlayerProperty
        /// </summary>
        private void LoadFPA()
        {
            foreach (var intType in Types)
            {
                var type = intType.Item2;   //获取玩家类型
                if (!PlayerField.TryGetValue(type, out List<FieldAttr<FieldInfo, HashSet<Type>>> playerField))    //尝试获取该玩家的字段
                {
                    //LoadFA(type, playerField);
                    LoadFA(type);
                }

                if (!PlayerProperty.TryGetValue(type, out var playerProperty))
                {
                    LoadPA(type);
                }
            }
        }


        /// <summary>
        /// 载入该类型的全部属性
        /// </summary>
        private void LoadPA(Type type)
        {
            List<PropertyAttr<PropertyInfo, HashSet<Type>>> list = [];
            PlayerProperty.Add(type, list);
            PAProperty.TryGetValue(type, out List<PropertyInfo> propertys); //尝试获取全部该类型的字段
            {
                foreach (PropertyInfo property in propertys)
                {
                    HashSet<Type> set = [];
                    foreach (Attribute att in property.GetCustomAttributes())
                    {
                        set.Add(att.GetType());
                    }
                    if (set.Count <= 0)
                        continue;

                    list.Add(PropertyAttr<PropertyInfo, HashSet<Type>>.CreatePropertyAttr(property, set));
                }
            }
        }

        /// <summary>
        /// 载入该类型的全部字段
        /// </summary>
        private void LoadFA(Type type)
        {
            List<FieldAttr<FieldInfo, HashSet<Type>>> list = [];
            PlayerField.Add(type, list);
            PAFields.TryGetValue(type, out List<FieldInfo> fields); //尝试获取全部该类型的字段
            {
                foreach (FieldInfo field in fields)
                {
                    HashSet<Type> set = [];
                    foreach (Attribute att in field.GetCustomAttributes())
                    {
                        set.Add(att.GetType());
                    }
                    if (set.Count <= 0)
                        continue;

                    list.Add(FieldAttr<FieldInfo, HashSet<Type>>.CreateFieldAttr(field, set));
                }
            }
        }

        [Obsolete]
        private void LoadFA(Type type, List<FieldAttr<FieldInfo, HashSet<Attribute>>> playerField)
        {
            if (PAFields.TryGetValue(type, out List<FieldInfo> fields))    //尝试获取该类型的全部字段
            {
                foreach (FieldInfo field in fields) //遍历字段
                {
                    var fa = playerField.FirstOrDefault(fa => fa.Field == field); //查看字典中是否包含该字段
                    if (fa != null) //获取到了
                    {
                        var set = fa.Attr; //为其添加特性
                        foreach (var item in field.GetCustomAttributes())
                        {
                            set.Add(item);
                        }
                    }
                }
            }
        }

    }
}
