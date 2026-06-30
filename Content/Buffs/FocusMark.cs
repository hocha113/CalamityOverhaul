using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Buffs
{
    //聚焦标记：纯标记位，自身不致损，由聚能魔典的死亡射线读取并结算加成
    internal class FocusMark : ModBuff
    {
        /// <summary>标记基础持续时间(帧)</summary>
        public const int Duration = 240;
        /// <summary>死亡射线命中标记目标时追加的持续时间(帧)，受<see cref="Duration"/>封顶</summary>
        public const int RayExtend = 30;
        /// <summary>死亡射线命中标记目标的伤害倍率</summary>
        public const float RayDamageMul = 1.5f;

        public override string Texture => CWRConstant.Placeholder2;
        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = true;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.LongerExpertDebuff[Type] = true;
        }
    }
}
