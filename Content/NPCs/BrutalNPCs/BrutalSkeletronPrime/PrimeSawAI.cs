using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States.Arms;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    /// <summary>
    /// 电锯控制器：行为见 <see cref="SawIdleState"/> / <see cref="SawSpinUpState"/> / <see cref="SawDashState"/>
    /// / <see cref="SawOrbitState"/> / <see cref="SawDrillState"/> / <see cref="SawRecoveryState"/>
    /// </summary>
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
            if (Main.GameUpdateCount % 5 == 0 && ++frame > 1) {
                frame = 0;
            }
        }

        #region 绘制
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (HeadPrimeAI.DontReform() || NPC.IsMechQueenUp) {
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

            //旋转拖尾（在滤镜之前——拖尾本身柔和，不需要套描边）
            if (spinSpeed > 0.4f) {
                for (int i = 0; i < 3; i++) {
                    float trailRot = drawRot - (i + 1) * spinSpeed * 0.3f;
                    Color trailColor = drawColor * (0.3f - i * 0.1f);
                    Main.EntitySpriteDraw(mainValue, sawDrawPos, sawRect,
                        trailColor, trailRot, sawOrigin, npc.scale, SpriteEffects.None, 0);
                }
            }

            //机械热感滤镜——和头部共用 head.whoAmI 状态
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

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => !HeadPrimeAI.DontReform() && !NPC.IsMechQueenUp;
        #endregion
    }
}
