using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States.Arms;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    /// <summary>电锯 NPCOverride；行为见 <see cref="SawIdleState"/> 等 Saw 状态</summary>
    internal class PrimeSawAI : PrimeArm
    {
        public override int TargetID => NPCID.PrimeSaw;
        public override bool? CheckDead() => true;

        protected override PrimeArmStateBase CreateInitialState() => new SawIdleState();
        protected override int DetonationDelay => 44;
        protected override int FormationIndex => 2;

        protected override void ArmPreUpdate() {
            //锯片转速平滑趋向目标值 + 高转速啸叫
            armContext.SpinSpeed = MathHelper.Lerp(armContext.SpinSpeed, armContext.TargetSpinSpeed, 0.08f);
            if (!VaultUtils.isServer && armContext.SpinSpeed > 0.6f && Main.GameUpdateCount % 40 == 0) {
                SoundEngine.PlaySound(SoundID.Item23 with { Volume = 0.4f, Pitch = armContext.SpinSpeed * 0.5f }, npc.Center);
            }

            //距离安全网：飞太远强制归位
            if (!VaultUtils.isClient && armStateMachine.CurrentState is not SawRecoveryState) {
                Vector2 anchor = head.Center + new Vector2(-200f * armContext.Side, 230f - head.height * 0.5f);
                if (npc.Distance(anchor) > 800f) {
                    armStateMachine.ChangeState(new SawRecoveryState());
                    npc.netUpdate = true;
                }
            }
        }

        protected override void ArmPostUpdate() {
            //锯片转速：帧动画切帧，机体不再整体自旋
            int interval = (int)MathHelper.Clamp(9f - armContext.SpinSpeed * 7f, 2f, 9f);
            if (Main.GameUpdateCount % interval == 0 && ++frame > 1) {
                frame = 0;
            }
        }

        #region 绘制
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (NPC.IsMechQueenUp) {
                return true;
            }
            HeadPrimeAI.DrawArm(spriteBatch, npc, screenPos);
            Texture2D mainValue = HeadPrimeAI.BSPSAW.Value;
            Texture2D glowValue = HeadPrimeAI.BSPSAWGlow.Value;
            float drawRot = npc.rotation;
            Vector2 sawDrawPos = npc.Center - Main.screenPosition;
            Rectangle sawRect = mainValue.GetRectangle(frame, 2);
            Vector2 sawOrigin = VaultUtils.GetOrig(mainValue, 2);
            float spinSpeed = armContext?.SpinSpeed ?? 0f;

            //高速残影（滤镜前）：沿速度反方向位置残像，非旋转鬼影
            if (npc.velocity.LengthSquared() > 36f) {
                Vector2 velDir = npc.velocity * 0.45f;
                for (int i = 1; i <= 3; i++) {
                    Color trailColor = drawColor * (0.32f - i * 0.09f);
                    Main.EntitySpriteDraw(mainValue, sawDrawPos - velDir * i, sawRect,
                        trailColor, drawRot, sawOrigin, npc.scale, SpriteEffects.None, 0);
                }
            }

            //机械热感滤镜，与头部共用 head.whoAmI
            int controllerId = (int)npc.ai[PrimeAiSlots.ArmHeadIndex];
            MechBossThermalRenderer.DrawOutlineHaloByController(spriteBatch, mainValue, sawDrawPos, sawRect,
                drawRot, sawOrigin, npc.scale, SpriteEffects.None, controllerId);

            bool shaderApplied = MechBossThermalRenderer.BeginThermalShaderByController(
                spriteBatch, mainValue, sawRect, controllerId, seed: (npc.whoAmI % 64) / 64f);
            spriteBatch.Draw(mainValue, sawDrawPos, sawRect,
                drawColor, drawRot, sawOrigin, npc.scale, SpriteEffects.None, 0);
            if (shaderApplied) {
                MechBossThermalRenderer.EndThermalShader(spriteBatch);
            }

            Main.EntitySpriteDraw(glowValue, sawDrawPos, sawRect,
                Color.White * (0.8f + spinSpeed * 0.2f), drawRot, sawOrigin, npc.scale, SpriteEffects.None, 0);
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => !NPC.IsMechQueenUp;
        #endregion
    }
}
