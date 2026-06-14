using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States.Arms;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.Common;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    /// <summary>火箭炮 NPCOverride；行为见 CannonBombardState / CannonSpreadState</summary>
    internal class PrimeCannonAI : PrimeArm
    {
        public override int TargetID => NPCID.PrimeCannon;
        public override bool? CheckDead() => true;

        protected override PrimeArmStateBase CreateInitialState() => new CannonBombardState();
        protected override int DetonationDelay => 20;
        protected override int FormationIndex => 1;

        protected override void ArmPreUpdate() {
            //跟随自己打出的制导炮弹旋转，呈现"目送弹药"的细节
            if (FindPrimeCannonOnSpan(out Projectile primeCannonOnSpan)) {
                npc.rotation = primeCannonOnSpan.rotation - MathHelper.PiOver2;
            }
        }

        private bool FindPrimeCannonOnSpan(out Projectile projectile) {
            projectile = null;
            int type = ModContent.ProjectileType<PrimeCannonOnSpan>();
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type != type) {
                    continue;
                }
                if (proj.ai[0] == npc.whoAmI && proj.ai[2] == 0) {
                    projectile = proj;
                    return true;
                }
            }
            return false;
        }

        #region 绘制
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (NPC.IsMechQueenUp) {
                return true;
            }

            bool dir = (npc.rotation + MathHelper.PiOver2).ToRotationVector2().X > 0;

            HeadPrimeAI.DrawArm(spriteBatch, npc, screenPos);
            Texture2D mainValue = HeadPrimeAI.BSPCannon.Value;
            Texture2D glowValue = HeadPrimeAI.BSPCannonGlow.Value;

            float recoil = armContext?.RecoilIntensity ?? 0f;
            Vector2 aimDirection = armContext?.AimDirection ?? Vector2.UnitX;

            //后坐力抖动偏移
            Vector2 recoilOffset = Vector2.Zero;
            if (recoil > 1f) {
                recoilOffset = -aimDirection * (recoil * 2f);
                recoilOffset += Main.rand.NextVector2Circular(recoil * 0.5f, recoil * 0.5f);
            }

            Vector2 drawPos = npc.Center - Main.screenPosition + recoilOffset;

            //机械热感滤镜，与头部共用 head.whoAmI
            int controllerId = (int)npc.ai[PrimeAiSlots.ArmHeadIndex];
            Rectangle cannonRect = mainValue.Bounds;
            Vector2 cannonOrigin = mainValue.Size() / 2;
            SpriteEffects cannonFx = dir ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            MechBossThermalRenderer.DrawOutlineHaloByController(spriteBatch, mainValue, drawPos, cannonRect,
                npc.rotation, cannonOrigin, npc.scale, cannonFx, controllerId);

            bool shaderApplied = MechBossThermalRenderer.BeginThermalShaderByController(
                spriteBatch, mainValue, cannonRect, controllerId, seed: (npc.whoAmI % 64) / 64f);
            spriteBatch.Draw(mainValue, drawPos, null, drawColor,
                npc.rotation, cannonOrigin, npc.scale, cannonFx, 0);
            if (shaderApplied) {
                MechBossThermalRenderer.EndThermalShader(spriteBatch);
            }

            //发光层（开火时炽热增强）
            bool isFiring = recoil > 0.5f;
            float glowIntensity = isFiring ? MathHelper.Clamp(1.0f + recoil * 0.1f, 1.0f, 1.5f) : 1.0f;
            Color glowColor = Color.White * glowIntensity;
            if (isFiring) {
                glowColor = Color.Lerp(Color.White, Color.OrangeRed, recoil / 15f) * glowIntensity;
            }

            Main.EntitySpriteDraw(glowValue, drawPos, null, glowColor,
                npc.rotation, cannonOrigin, npc.scale, cannonFx, 0);

            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => !NPC.IsMechQueenUp;
        #endregion
    }
}
