using GensokyoWPNACC.PacketMode.Attributes;
using GensokyoWPNACC.PacketMode.NetInterface;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Terraria.ModLoader;
using static GensokyoWPNACC.PacketMode.NetType.OneLoading;

namespace GensokyoWPNACC.PacketMode.NetType
{
    public class NetProjectile : Net<ModProjectile, ProjectileNetFieldAttribute, ProjectileNetPropertyAttribute>
    {
        private static NetProjectile _netProjectile = new NetProjectile();
        public static NetProjectile NetProjectiles { get => _netProjectile; }

        private NetProjectile() { }
        private bool IsInitialize = false;
        public override void InitializeData()
        {
            if (IsInitialize)
                return;
            IsInitialize = true;
            base.InitializeData();
            Hook();
        }

        public override void WritePackets()
        {

        }

        //public virtual void SendExtraAI(BinaryWriter writer)
        private delegate void HookSend(ModProjectile obj, BinaryWriter writer);

        //public virtual void ReceiveExtraAI(BinaryReader reader)
        private delegate void HookBinaryReader(ModProjectile obj, BinaryReader reader);

        private void HookSendMethod(HookSend orig, ModProjectile obj, BinaryWriter writer)
        {
            SendMethod(obj, writer);
            orig.Invoke(obj, writer);
        }

        private void HookReceiveMethod(HookBinaryReader orig, ModProjectile obj, BinaryReader reader)
        {
            ReceiveMethod(obj, reader);
            orig.Invoke(obj, reader);
        }

        /// <summary>
        /// 给支持的弹幕挂钩子
        /// </summary>
        private void Hook()
        {
            HashSet<Type> HookProjectile = [];
            foreach (var KV in Fields)
            {
                HookProjectile.Add(KV.Key);
            }

            foreach (var KV in Propertys)
            {
                HookProjectile.Add(KV.Key);
            }

            foreach (var type in HookProjectile)
            {
                MethodInfo sendMethod = type.GetMethod("SendExtraAI", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo receiveMethod = type.GetMethod("ReceiveExtraAI", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (sendMethod != null && receiveMethod != null)
                {
                    MonoModHooks.Add(sendMethod, HookSendMethod);
                    MonoModHooks.Add(receiveMethod, HookReceiveMethod);
                }
            }
        }
    }
}
