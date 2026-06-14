using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States.Arms;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    /// <summary>钳爪 NPCOverride；行为见 <see cref="ViceIdleState"/> 等 Vice 状态</summary>
    internal class PrimeViceAI : PrimeArm
    {
        public override int TargetID => NPCID.PrimeVice;
        public override bool? CheckDead() => true;

        protected override PrimeArmStateBase CreateInitialState() => new ViceIdleState();
        protected override int DetonationDelay => 56;
        protected override int FormationIndex => 3;

        protected override void ArmPreUpdate() {
            //冲击反馈衰减
            armContext.ImpactIntensity *= 0.88f;

            //距离安全网：飞太远全速归队
            if (!VaultUtils.isClient && armStateMachine.CurrentState is not ViceReturnState) {
                Vector2 anchor = head.Center + new Vector2(-200f * armContext.Side, 230f - head.height * 0.5f);
                if (npc.Distance(anchor) > 800f) {
                    armStateMachine.ChangeState(new ViceReturnState());
                    npc.netUpdate = true;
                }
            }
        }

        protected override void ArmPostUpdate() {
            //钳口开合由状态驱动（蓄力/突刺张开，命中/待机闭合），不再无意义地循环播帧
            //帧约定与死亡演出钳子 Actor 一致：0=张开 1=闭合
            frame = armContext.ClawOpen ? 0 : 1;
        }

        #region 绘制
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (NPC.IsMechQueenUp) {
                return true;
            }
            HeadPrimeAI.DrawArm(spriteBatch, npc, screenPos);
            Texture2D mainValue = HeadPrimeAI.BSPPliers.Value;
            Texture2D glowValue = HeadPrimeAI.BSPPliersGlow.Value;

            //命中冲击抖动
            float impact = armContext?.ImpactIntensity ?? 0f;
            Vector2 drawOffset = Vector2.Zero;
            if (impact > 0.5f) {
                drawOffset = Main.rand.NextVector2Circular(impact, impact);
            }

            Vector2 viceDrawPos = npc.Center - Main.screenPosition + drawOffset;
            Rectangle viceRect = mainValue.GetRectangle(frame, 2);
            Vector2 viceOrigin = VaultUtils.GetOrig(mainValue, 2);

            //机械热感滤镜，与头部共用 head.whoAmI
            int controllerId = (int)npc.ai[PrimeAiSlots.ArmHeadIndex];
            MechBossThermalRenderer.DrawOutlineHaloByController(spriteBatch, mainValue, viceDrawPos, viceRect,
                npc.rotation, viceOrigin, npc.scale, SpriteEffects.None, controllerId);

            bool shaderApplied = MechBossThermalRenderer.BeginThermalShaderByController(
                spriteBatch, mainValue, viceRect, controllerId, seed: (npc.whoAmI % 64) / 64f);
            spriteBatch.Draw(mainValue, viceDrawPos, viceRect,
                drawColor, npc.rotation, viceOrigin, npc.scale, SpriteEffects.None, 0);
            if (shaderApplied) {
                MechBossThermalRenderer.EndThermalShader(spriteBatch);
            }

            Main.EntitySpriteDraw(glowValue, viceDrawPos, viceRect,
                Color.White, npc.rotation, viceOrigin, npc.scale, SpriteEffects.None, 0);
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => !NPC.IsMechQueenUp;
        #endregion
    }
}
