using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States.Arms;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    /// <summary>
    /// 激光炮控制器：行为见 <see cref="LaserAimState"/> / <see cref="LaserRapidFireState"/>
    /// / <see cref="LaserChargedShotState"/> / <see cref="LaserRingState"/>
    /// </summary>
    internal class PrimeLaserAI : PrimeArm
    {
        public override int TargetID => NPCID.PrimeLaser;
        public override bool? CheckDead() => true;

        protected override PrimeArmStateBase CreateInitialState() => new LaserAimState();
        protected override int DetonationDelay => 32;
        protected override int FormationIndex => 0;

        protected override void ArmPreUpdate() {
            //蓄力期的汇聚粒子（强度随充能渐增）
            if (VaultUtils.isServer || armContext.ChargeGlow <= 0f || armContext.ChargeGlow >= 1f) {
                return;
            }
            if (Main.rand.NextFloat() < armContext.ChargeGlow * 0.3f) {
                Vector2 particlePos = npc.Center + armContext.AimDirection * 60f + Main.rand.NextVector2Circular(20, 20);
                Dust dust = Dust.NewDustDirect(particlePos, 1, 1, DustID.FireworkFountain_Red,
                    0, 0, 100, Color.Cyan, Main.rand.NextFloat(0.8f, 1.5f));
                dust.velocity = (npc.Center + armContext.AimDirection * 80f - particlePos) * 0.1f;
                dust.noGravity = true;
            }
        }

        #region 绘制
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (HeadPrimeAI.DontReform() || NPC.IsMechQueenUp) {
                return true;
            }

            bool dir = (npc.rotation + MathHelper.PiOver2).ToRotationVector2().X > 0;
            HeadPrimeAI.DrawArm(spriteBatch, npc, screenPos);
            Texture2D mainValue = HeadPrimeAI.BSPlaser.Value;
            Texture2D glowValue = HeadPrimeAI.BSPlaserGlow.Value;

            //机械热感滤镜，与头部共用 head.whoAmI
            int controllerId = (int)npc.ai[PrimeAiSlots.ArmHeadIndex];
            Vector2 laserDrawPos = npc.Center - Main.screenPosition;
            Rectangle laserRect = mainValue.Bounds;
            Vector2 laserOrigin = mainValue.Size() / 2;
            SpriteEffects laserFx = dir ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            MechBossThermalRenderer.DrawOutlineHaloByController(spriteBatch, mainValue, laserDrawPos, laserRect,
                npc.rotation, laserOrigin, npc.scale, laserFx, controllerId);

            bool shaderApplied = MechBossThermalRenderer.BeginThermalShaderByController(
                spriteBatch, mainValue, laserRect, controllerId, seed: (npc.whoAmI % 64) / 64f);
            spriteBatch.Draw(mainValue, laserDrawPos, null, drawColor,
                npc.rotation, laserOrigin, npc.scale, laserFx, 0);
            if (shaderApplied) {
                MechBossThermalRenderer.EndThermalShader(spriteBatch);
            }

            //发光层随蓄力进度由白转青
            float charge = armContext?.ChargeGlow ?? 0f;
            Color glowColor = Color.White;
            if (charge > 0f) {
                glowColor = Color.Lerp(Color.White, Color.Cyan, charge) * (0.8f + charge * 0.7f);
            }

            Main.EntitySpriteDraw(glowValue, laserDrawPos, null, glowColor,
                npc.rotation, glowValue.Size() / 2, npc.scale, laserFx, 0);

            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => !HeadPrimeAI.DontReform() && !NPC.IsMechQueenUp;
        #endregion
    }
}
