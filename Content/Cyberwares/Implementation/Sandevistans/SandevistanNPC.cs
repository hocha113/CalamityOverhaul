using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans
{
    /// <summary>GlobalNPC 时缓，不侵入 CWRNpc</summary>
    internal class SandevistanNPC : GlobalNPC
    {
        public override bool PreAI(NPC npc) {
            if (!SandevistanTimeSlow.IsActive) {
                return true;
            }
            if (!SandevistanTimeSlow.ShouldAffectNPC(npc)) {
                return true;
            }

            //缓速期间首次出现或槽位被复用时按需重新抓取原速度
            SandevistanTimeSlow.EnsureNPCSnapshot(npc);

            int id = npc.whoAmI;
            Vector2 slowVel = SandevistanTimeSlow.NPCCachedVelocities[id] * SandevistanTimeSlow.SlowFactor;

            //保活+冻动画
            npc.timeLeft++;
            npc.aiAction = 0;
            npc.frameCounter = 0;
            //回滚本帧位移，按缓存速度缩放重设
            npc.position = npc.oldPosition + slowVel;
            npc.velocity = slowVel;
            npc.direction = npc.oldDirection;

            return false;
        }
    }
}
