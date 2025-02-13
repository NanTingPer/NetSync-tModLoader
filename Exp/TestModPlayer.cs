using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ModLoader;

namespace GensokyoWPNACC.Global
{
    public class TestModPlayer : ModPlayer
    {
        public bool 同步字段 = false;

        [NetPlayerTest]
        public Vector2 要被同步的字段01 = Vector2.Zero;

        [NetPlayerTest]
        public int 要被同步的字段02ItemID = 0;

        private int time = 0;
        public override void PreUpdate()
        {
            要被同步的字段01 = Main.MouseWorld;
            要被同步的字段02ItemID = Player.HeldItem.type;
            time++;
            if(time % 60 == 0)
                同步字段 = true;
            base.PreUpdate();
        }


        [AttributeUsage(AttributeTargets.Field)]
        public class NetPlayerTestAttribute : Attribute { }
    }
}
