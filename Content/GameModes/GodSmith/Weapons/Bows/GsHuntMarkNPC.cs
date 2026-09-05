using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Bows
{
    /// <summary>
    /// 猎标载体（逐实例 GlobalNPC）。所有字段都是 owner-local 量：
    /// 命中类钩子只在攻击方端执行，因此每端只记本机玩家叠的标，不跨端同步；
    /// 伤害结算同样在弹幕 owner 端裁决，账目天然一致。跨端可见的处决表现全部走真弹幕
    /// </summary>
    internal class GsHuntMarkNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>猎标层数（0~Cap）</summary>
        internal int Stacks;

        /// <summary>标记剩余帧，归零清层（计划口径 4 秒）</summary>
        internal int Timer;

        /// <summary>幻影弓连击计数（对同一目标连续命中）</summary>
        internal int SoulCombo;

        /// <summary>连击续窗剩余帧，归零清连击</summary>
        internal int SoulComboTimer;

        /// <summary>标桩钉桩节流：>0 时本目标不再被钉（防连射永锁）</summary>
        internal int PinCooldown;

        /// <summary>钛金连弩蚀甲层数（0~5，每层齐射箭对该敌 +2 穿甲）</summary>
        internal int ErodeStacks;

        /// <summary>蚀甲剩余帧，归零清层</summary>
        internal int ErodeTimer;

        public override void PostAI(NPC npc) {
            if (Timer > 0 && --Timer == 0) {
                Stacks = 0;
            }
            if (SoulComboTimer > 0 && --SoulComboTimer == 0) {
                SoulCombo = 0;
            }
            if (PinCooldown > 0) {
                PinCooldown--;
            }
            if (ErodeTimer > 0 && --ErodeTimer == 0) {
                ErodeStacks = 0;
            }
        }

        /// <summary>该 NPC 可否被标记（排除友军、假人、无血皮的演出体）</summary>
        internal static bool CanMark(NPC npc)
            => npc.active && !npc.friendly && npc.lifeMax > 5 && npc.type != NPCID.TargetDummy;

        /// <summary>找最近的带标敌（owner 端索敌用）</summary>
        internal static NPC FindNearestMarked(Vector2 from, float maxDist) {
            NPC best = null;
            float bestDist = maxDist;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!CanMark(npc) || npc.GetGlobalNPC<GsHuntMarkNPC>().Stacks <= 0) {
                    continue;
                }
                float dist = from.Distance(npc.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }
    }
}
