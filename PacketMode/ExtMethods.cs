using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Terraria.ModLoader.Core;
using Terraria.ModLoader;
using Terraria;

namespace GensokyoWPNACC.PacketMode
{
    public static class NetExtMethods
    {
        #region Dictionary
        public static void MyReverse<T1, T2>(this Dictionary<T1, T2> dic, Dictionary<T2, T1> outResult)
        {
            foreach (var item in dic)
            {
                outResult.Add(item.Value, item.Key);
            }
        }
        #endregion Dictionary

        public static bool TryGetGlobalNPC<TResult>(this NPC npc, out TResult result) where TResult : GlobalNPC
        {
            TResult baseInstance = ModContent.GetInstance<TResult>();
            if (!TryGetGlobalSafe<GlobalNPC, TResult>(npc.type, npc.EntityGlobals, baseInstance, out result))
                return false;
            return true;
        }

        private static bool TryGetGlobalSafe<TGlobal, TResult>(int entityType, ReadOnlySpan<TGlobal> entityGlobals, TResult baseInstance, [NotNullWhen(true)] out TResult? result) where TGlobal : GlobalType<TGlobal> where TResult : TGlobal
        {
            short perEntityIndex = baseInstance.PerEntityIndex;
            if (entityType > 0 && perEntityIndex >= 0 && perEntityIndex < entityGlobals.Length)
            {
                result = entityGlobals[perEntityIndex] as TResult;
                return result != null;
            }

            if (GlobalTypeLookups<TGlobal>.AppliesToType(baseInstance, entityType))
            {
                result = baseInstance;
                return true;
            }

            result = null;
            return false;
        }
    }
}
