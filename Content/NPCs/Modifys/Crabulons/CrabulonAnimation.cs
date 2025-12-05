using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.Modifys.Crabulons
{
    /// <summary>
    /// 菌生蟹帧动画系统
    /// </summary>
    internal class CrabulonAnimation
    {
        private readonly NPC npc;
        private readonly ModifyCrabulon owner;
        private readonly CrabulonPhysics physics;

        public CrabulonAnimation(NPC npc, ModifyCrabulon owner, CrabulonPhysics physics) {
            this.npc = npc;
            this.owner = owner;
            this.physics = physics;
        }

        //更新帧动画
        public bool UpdateFrame(int frameHeight) {
            if (owner.FeedValue <= 0f) {
                return true;
            }

            if (!npc.collideY) {
                UpdateAirFrames(frameHeight);
            }
            else {
                UpdateGroundFrames(frameHeight);
            }

            return false;
        }

        //更新空中帧
        private void UpdateAirFrames(int frameHeight) {
            if (npc.velocity.Y < 0 || physics.GroundClearance > 100) {
                owner.ai[11] = MathHelper.Lerp(owner.ai[11], CrabulonConstants.JumpFrame, CrabulonConstants.FrameLerpSpeed);
            }
            else {
                owner.dontTurnTo = CrabulonConstants.TurnDelayTime;
                owner.ai[11] = MathHelper.Lerp(owner.ai[11], CrabulonConstants.FallFrame, CrabulonConstants.FallFrameLerpSpeed);
            }

            npc.frame.Y = frameHeight * (int)owner.ai[11];
            npc.frameCounter = 0;
        }

        //更新地面帧
        private void UpdateGroundFrames(int frameHeight) {
            if (Math.Abs(npc.velocity.X) > 0.1f) {
                UpdateRunningFrames(frameHeight);
            }
            else {
                UpdateIdleFrames(frameHeight);
            }
        }

        //更新跑步帧
        private void UpdateRunningFrames(int frameHeight) {
            npc.frameCounter += Math.Abs(npc.velocity.X) * CrabulonConstants.RunFrameSpeed;
            npc.frameCounter %= Main.npcFrameCount[npc.type];
            int frame = (int)npc.frameCounter;
            npc.frame.Y = frame * frameHeight;
        }

        //更新待机帧
        private void UpdateIdleFrames(int frameHeight) {
            if (owner.ai[9] > 0) {
                npc.frameCounter += CrabulonConstants.CrouchIdleFrameSpeed;
                npc.frameCounter %= CrabulonConstants.MaxIdleFrames;
            }
            else {
                npc.frameCounter += CrabulonConstants.IdleFrameSpeed;
                npc.frameCounter %= Main.npcFrameCount[npc.type];
            }

            int frame = (int)npc.frameCounter;
            npc.frame.Y = frame * frameHeight;
        }
    }
}
