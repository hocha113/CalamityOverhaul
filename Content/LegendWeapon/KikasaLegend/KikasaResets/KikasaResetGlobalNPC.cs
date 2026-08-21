using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaResets
{
    /// <summary>
    /// 重启持有 NPC 的兜底拦截：正牌冻结走 TimeFreezes 租约——统一 AI 入口在
    /// On_NPC.AI 检测钩最前层直接短路，Override/原版 AI 一并停摆，本钩子届时不会被执行；
    /// 只有租约未生效的边角帧（挂租失败、身份重置的空窗）才落到这里，
    /// 拦下 tML 层 AI 并钳零速度，位置仍由 <see cref="KikasaReset"/> 按历史驱动
    /// </summary>
    internal sealed class KikasaResetGlobalNPC : GlobalNPC
    {
        public override bool PreAI(NPC npc) {
            if (!KikasaReset.IsNpcHeld(npc.whoAmI)) {
                return true;
            }
            npc.velocity = Vector2.Zero;
            return false;
        }
    }
}
