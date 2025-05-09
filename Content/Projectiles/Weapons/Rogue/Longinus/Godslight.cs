using CalamityMod.Buffs.DamageOverTime;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.DamageModify;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Rogue.Longinus
{
    internal class Godslight : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        internal Vector2[] RayPoint;
        internal Vector2[] RayPointByX;
        internal int pointNum => 100;
        internal Color[] colors;
        private Trail TrailByY;
        private Trail TrailByX;
        public override bool ShouldUpdatePosition() => false;
        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 8000;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.timeLeft = 190;
            Projectile.DamageType = EndlessDamageClass.Instance;
            Projectile.penetrate = -1;
            Projectile.alpha = 255;
            Projectile.scale = 0.1f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override bool PreAI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Projectile.ai[0] == 0) {
                colors = [Color.Red, Color.Green, Color.OrangeRed];
                RayPoint = new Vector2[pointNum];
                RayPointByX = new Vector2[pointNum];
                Vector2 rotByY = Projectile.velocity.UnitVector();
                Vector2 rotByX = rotByY.RotatedBy(MathHelper.PiOver2);

                for (int i = 0; i < pointNum; i++) {
                    RayPoint[i] = rotByY * (-pointNum * 30 + 60 * i) + Projectile.Center;
                }
                for (int i = 0; i < pointNum; i++) {
                    RayPointByX[i] = rotByX * (-pointNum * 30 + 60 * i) + Projectile.Center;
                }

                for (int i = 0; i < 4; i++) {
                    foreach (Vector2 pos in RayPoint) {
                        Vector2 spanPos = pos + Main.rand.NextVector2Unit() * Main.rand.Next(56);
                        Vector2 vr = new Vector2((Main.rand.NextBool() ? -1 : 1) * Main.rand.Next(7, 51), 0);
                        PRT_Light light = new PRT_Light(spanPos
                            , vr, 0.3f, VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), colors), 30);
                        //不要在屏幕外面就消除了，否则玩家什么都看不到
                        light.ShouldKillWhenOffScreen = false;
                        PRTLoader.AddParticle(light);
                    }
                }

                if (!Main.dedServ) {
                    TrailByY ??= new Trail(RayPoint, GetColorFunc, GetWeithFunc);
                    TrailByX ??= new Trail(RayPointByX, GetColorFunc, GetWeithFunc);
                }

                Projectile.ai[0] = 1;
            }
            if (Projectile.timeLeft > 60) {
                Projectile.scale += 0.5f;
                if (Projectile.scale > 9)
                    Projectile.scale = 9;
            }
            if (Projectile.timeLeft < 20) {
                Projectile.scale -= 1f;
                if (Projectile.scale < 0)
                    Projectile.scale = 0;
            }
            if (Projectile.timeLeft == 20) {
                SpanDeadLightPenms();
            }
            return true;
        }

        public void SpanDeadLightPenms() {
            if (Projectile.IsOwnedByLocalPlayer()) {
                foreach (Vector2 pos in RayPoint) {
                    PRT_Light light = new PRT_Light(pos + Main.rand.NextVector2Unit() * Main.rand.Next(56)
                        , new Vector2(0, Main.rand.Next(7, 51)), 0.3f, VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), colors), 30);
                    PRTLoader.AddParticle(light);
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , Projectile.rotation.ToRotationVector2() * -3000 + Projectile.Center
                , Projectile.rotation.ToRotationVector2() * 3000 + Projectile.Center, 132, ref point);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Projectile.damage = (int)(Projectile.damage * 0.98f);
            target.AddBuff(ModContent.BuffType<GodSlayerInferno>(), 300);
        }

        public override void OnKill(int timeLeft) => SpanDeadLightPenms();

        public float GetColorFunc(float sengs) => Projectile.scale * Projectile.width * Projectile.ai[1];

        public Color GetWeithFunc(Vector2 sengs) {
            float colorInterpolant = (float)Math.Sin(Projectile.identity / 3f + sengs.X * 20f + Main.GlobalTimeWrappedHourly * 1.1f) * 0.5f + 0.5f;
            Color color = colors != null ? VaultUtils.MultiStepColorLerp(colorInterpolant, colors) : Color.White;
            return color;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (TrailByY == null || TrailByX == null) {
                return;
            }

            Effect effect = EffectLoader.GradientTrail.Value;
            effect.Parameters["transformMatrix"].SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"].SetValue((float)Main.timeForVisualEffects * 0.08f);
            effect.Parameters["uTimeG"].SetValue(Main.GlobalTimeWrappedHourly * 0.2f);
            effect.Parameters["udissolveS"].SetValue(1f);
            effect.Parameters["uBaseImage"].SetValue(CWRUtils.GetT2DValue(CWRConstant.Masking + "StarTexture"));
            effect.Parameters["uFlow"].SetValue(CWRAsset.Airflow.Value);
            effect.Parameters["uGradient"].SetValue(CWRUtils.GetT2DValue(CWRConstant.ColorBar + "DragonRage_Bar"));
            effect.Parameters["uDissolve"].SetValue(CWRAsset.Extra_193.Value);

            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            TrailByY?.DrawTrail(effect);
            TrailByX?.DrawTrail(effect);
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        }
    }
}
