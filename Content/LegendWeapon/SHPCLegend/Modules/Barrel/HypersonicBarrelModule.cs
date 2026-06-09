using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>
    /// 超音速枪管（亚龙）：极高速直线光束在路径上留下音爆线，短暂延迟后沿原路径炸开窄长二次伤害，
    /// 终点附加一记马赫锥冲击。换取爆发线性伤害的代价是几乎放弃追踪。
    /// </summary>
    internal sealed class HypersonicBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //超音速主题黄色
        public override Color TintColor => new(255, 235, 60);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamSpeedMul += 0.88f;
            ctx.AttackSpeedMul += 0.16f;
            ctx.DamageMul += -0.12f;
            ctx.HomingMul += -0.84f;
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            Vector2 dir = beam.Projectile.velocity.SafeNormalize(Vector2.UnitX);
            int dmg = Math.Max((int)(beam.Projectile.damage * 0.8f), 1);
            Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                beam.Projectile.Center, dir,
                ModContent.ProjectileType<SHPCSonicBoomProj>(),
                dmg, 0f, beam.Projectile.owner);
        }
    }

    /// <summary>
    /// 音爆线：沿来袭方向的窄长延迟冲击。前 8 帧蓄势，随后短暂激活做一次线段判定，
    /// 并在前端引爆一记马赫锥。velocity 仅用于定向。
    /// </summary>
    internal sealed class SHPCSonicBoomProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int Lifetime = 16;
        private const int Delay = 8;
        private const float HalfLength = 220f;
        private const float HitWidth = 24f;

        private Vector2 axis = Vector2.UnitX;

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = Lifetime;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        private bool Active => Lifetime - Projectile.timeLeft >= Delay;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                axis = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                Projectile.rotation = axis.ToRotation();
            }
            Projectile.velocity = Vector2.Zero;
            int age = Lifetime - Projectile.timeLeft;
            //激活瞬间：音爆 + 前端马赫锥引爆
            if (age == Delay) {
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.6f, Pitch = -0.4f }, Projectile.Center);
                    for (int i = 0; i < 18; i++) {
                        Vector2 along = axis * Main.rand.NextFloat(-HalfLength, HalfLength);
                        Vector2 vel = axis.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-4f, 4f);
                        PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center + along, vel, new Color(255, 245, 170), Main.rand.NextFloat(0.6f, 1.4f)).Configure(new Color(255, 200, 40), Main.rand.Next(8, 16));
                    }
                }
                if (Projectile.owner == Main.myPlayer) {
                    int dmg = Math.Max(Projectile.damage, 1);
                    int idx = Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                        Projectile.Center + axis * HalfLength, Vector2.Zero,
                        ModContent.ProjectileType<CyberDetonationProj>(),
                        dmg, 0f, Projectile.owner, ai0: 0.35f);
                    if (idx >= 0 && idx < Main.maxProjectiles) {
                        Main.projectile[idx].localAI[2] = 95f;
                    }
                }
            }
            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.92f, 0.4f) * (Active ? 0.8f : 0.3f));
        }

        public override bool? CanDamage() => Active;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Active) return false;
            float point = 0f;
            Vector2 start = Projectile.Center - axis * HalfLength;
            Vector2 end = Projectile.Center + axis * HalfLength;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, HitWidth, ref point);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            int age = Lifetime - Projectile.timeLeft;
            float charge = MathHelper.Clamp(age / (float)Delay, 0f, 1f);
            float active = Active ? MathHelper.Clamp(Projectile.timeLeft / (float)(Lifetime - Delay), 0f, 1f) : 0f;
            Vector2 screen = Projectile.Center - Main.screenPosition;

            Texture2D shot = CWRAsset.LightShotAlt?.Value;
            if (shot != null) {
                Vector2 origin = new(shot.Width * 0.5f, shot.Height * 0.5f);
                //蓄势期细线预告，激活期粗亮音爆线
                float width = MathHelper.Lerp(0.12f, 0.5f, charge) + active * 0.4f;
                float lengthScale = (HalfLength * 2f) / shot.Width;
                Color col = Active
                    ? new Color(255, 250, 200, 0) * (0.5f + active * 0.5f)
                    : new Color(255, 230, 120, 0) * charge * 0.4f;
                spriteBatch.Draw(shot, screen, null, col, axis.ToRotation(), origin, new Vector2(lengthScale, width), SpriteEffects.None, 0f);
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null && Active) {
                SHPCNaturalFx.GlowLayered(spriteBatch, glow, screen + axis * HalfLength, new Color(255, 240, 170, 0) * active, new Color(255, 150, 30, 0) * active * 0.4f, 0.8f, 0f, 3);
            }
        }
    }
}
