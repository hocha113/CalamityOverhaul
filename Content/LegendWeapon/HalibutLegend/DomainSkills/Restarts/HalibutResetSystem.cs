using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.DomainSkills.Restarts
{
    /// <summary>
    /// 大范围重启调度：权威/演出推进必须在服务器也跑，不能住在 dedServ 早退的钩子里。
    /// 历史记录本体由 KikasaResetSystem 驱动（共享缓冲），这里只推进本家的演出
    /// </summary>
    internal class HalibutResetSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            HalibutReset.Update();
        }

        public override void ClearWorld() {
            HalibutReset.Reset();
        }
    }

    /// <summary>
    /// 重启持有 NPC 的兜底拦截：正牌冻结走 TimeFreezes 租约，统一 AI 入口在
    /// On_NPC.AI 检测钩最前层直接短路；只有租约未生效的边角帧（挂租失败、
    /// 身份重置的空窗）才落到这里，拦下 tML 层 AI 并钳零速度，
    /// 位置仍由 <see cref="HalibutReset"/> 按历史驱动
    /// </summary>
    internal sealed class HalibutResetGlobalNPC : GlobalNPC
    {
        public override bool PreAI(NPC npc) {
            if (!HalibutReset.IsNpcHeld(npc.whoAmI)) {
                return true;
            }
            npc.velocity = Vector2.Zero;
            return false;
        }
    }
}
