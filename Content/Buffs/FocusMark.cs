using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Buffs
{
    //纯标记，死亡射线读取
    internal class FocusMark : ModBuff
    {
        /// <summary>基础持续(帧)</summary>
        public const int Duration = 240;
        /// <summary>射线命中追加(帧)，受 Duration 封顶</summary>
        public const int RayExtend = 30;
        /// <summary>射线命中伤害倍率</summary>
        public const float RayDamageMul = 1.5f;

        public override string Texture => CWRConstant.VaultPlaceholder2;
        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = true;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.LongerExpertDebuff[Type] = true;
        }
    }
}
