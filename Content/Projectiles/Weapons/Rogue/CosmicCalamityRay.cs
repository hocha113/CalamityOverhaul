using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Rogue
{
    internal class CosmicCalamityRay : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        internal Vector2[] RayPoint;
        private Trail Trail;
        internal const int pointNum = 100;
        public override bool ShouldUpdatePosition() => false;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.timeLeft = 90;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = -1;
            Projectile.alpha = 255;
            Projectile.scale = 0.1f;
            Projectile.localNPCHitCooldown = -1;
            Projectile.usesLocalNPCImmunity = true;
        }

        private void Spwan() {
            RayPoint = new Vector2[pointNum];
            for (int i = 0; i < pointNum; i++) {
                RayPoint[i] = Projectile.velocity.ToRotation().ToRotationVector2() * (-pointNum * 30 + 60 * i) + Projectile.Center;
            }
            foreach (Vector2 pos in RayPoint) {
                BasePRT pulse = new PRT_DWave(pos - Projectile.velocity * 0.52f, Projectile.velocity / 1.5f, Color.Blue, new Vector2(1f, 2f), Projectile.velocity.ToRotation(), 0.52f, 0.06f, 90);
                PRTLoader.AddParticle(pulse);
                BasePRT pulse2 = new PRT_DWave(pos - Projectile.velocity * 0.40f, Projectile.velocity / 1.5f * 0.9f, Color.Gold, new Vector2(0.8f, 1.5f), Projectile.velocity.ToRotation(), 0.28f, 0.02f, 80);
                PRTLoader.AddParticle(pulse2);
            }
        }

        public override bool PreAI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Projectile.ai[0] == 0) {
                Spwan();
                Projectile.ai[0] = 1;
            }
            if (Projectile.timeLeft > 60) {
                Projectile.scale += 0.5f;
                if (Projectile.scale > 6)
                    Projectile.scale = 6;
            }
            if (Projectile.timeLeft < 20) {
                Projectile.scale -= 1f;
                if (Projectile.scale < 0)
                    Projectile.scale = 0;
            }
            return true;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , Projectile.rotation.ToRotationVector2() * -3000 + Projectile.Center
                , Projectile.rotation.ToRotationVector2() * 3000 + Projectile.Center, 32, ref point);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Projectile.damage = (int)(Projectile.damage * 0.98f);
            target.AddBuff(CWRID.Buff_GodSlayerInferno, 300);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                modifiers.FinalDamage /= 2f;
            }
        }

        internal float GetWidthFunc(float completionRatio) {
            return MathF.Sin(MathHelper.Pi * MathHelper.Clamp(completionRatio, 0f, 1f)) * Projectile.scale * Projectile.width * 6;
        }

        internal Color GetColorFunc(Vector2 completionRatio) {
            float colorInterpolant = (float)Math.Sin(Projectile.identity / 3f + completionRatio.X * 20f + Main.GlobalTimeWrappedHourly * 1.1f) * 0.5f + 0.5f;
            Color color = VaultUtils.MultiStepColorLerp(colorInterpolant, new Color(119, 210, 255), Color.Blue, new Color(247, 119, 255));
            return color;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (RayPoint == null || RayPoint.Length == 0) {
                return;
            }

            //创建或更新 Trail
            Trail ??= new Trail(RayPoint, GetWidthFunc, GetColorFunc);
            Trail.TrailPositions = RayPoint;

            //使用 InnoVault 的绘制方法
            Effect effect = EffectLoader.GradientTrail.Value;
            effect.Parameters["transformMatrix"].SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"].SetValue((float)Main.timeForVisualEffects * 0.08f);
            effect.Parameters["uTimeG"].SetValue(Main.GlobalTimeWrappedHourly * 0.2f);
            effect.Parameters["udissolveS"].SetValue(1f);
            effect.Parameters["uBaseImage"].SetValue(CWRUtils.GetT2DValue("CalamityMod/ExtraTextures/Trails/ScarletDevilStreak"));
            effect.Parameters["uFlow"].SetValue(CWRAsset.Extra_193.Value);
            effect.Parameters["uGradient"].SetValue(CWRUtils.GetT2DValue(CWRConstant.ColorBar + "DragonRage_Bar"));
            effect.Parameters["uDissolve"].SetValue(CWRAsset.Extra_193.Value);

            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            for (int i = 0; i < 3; i++) {
                Trail?.DrawTrail(effect);
            }
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        }

        public override bool PreDraw(ref Color lightColor) {
            return false;
        }
    }
}
