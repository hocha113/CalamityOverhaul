using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant
{
    /// <summary>
    /// 施法节拍族的敌怪本地标记（骷髅头魔书「名录」印）。<br/>
    /// 命中类钩子只在攻击方端执行，因此印数据是攻击方本地量：
    /// 只用于 owner 端的选靶与骨爆裁决，不同步不跨端；
    /// 跨端可见的结果（追踪弹道、骨爆弹幕）经 MarkData/生成包过线
    /// </summary>
    internal class GsChantGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>名录印层数（3 层触发骨爆）</summary>
        internal int SkullMarkStacks;

        /// <summary>名录印失效时刻（Main.GameUpdateCount 口径）</summary>
        internal uint SkullMarkUntil;

        /// <summary>印是否在期（读取前先验时效）</summary>
        internal bool SkullMarkActive => SkullMarkStacks > 0 && Main.GameUpdateCount < SkullMarkUntil;

        /// <summary>叠一层名录印并续期</summary>
        internal void AddSkullMark(uint durationTicks) {
            if (!SkullMarkActive) {
                SkullMarkStacks = 0;
            }
            SkullMarkStacks++;
            SkullMarkUntil = Main.GameUpdateCount + durationTicks;
        }

        /// <summary>清空名录印（骨爆结算后）</summary>
        internal void ClearSkullMark() {
            SkullMarkStacks = 0;
            SkullMarkUntil = 0;
        }
    }
}
