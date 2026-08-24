using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>霰的演出集中处：碎裂白闪与霰珠伴生弹幕</summary>
    internal static class FuXianFX
    {
        /// <summary>霜白身份色，定义与演出同源取此</summary>
        internal static readonly Color Accent = new(224, 236, 242);

        /// <summary>碎冰蓝：霰珠深缘</summary>
        internal static readonly Color IceDeep = new(150, 182, 198);

        /// <summary>碎裂白闪：白环+放射短线+霜珠迸散+脆响，各端本地</summary>
        internal static void ShatterFlash(Vector2 pos) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(pos, Vector2.Zero,
                Color.White * 0.7f, 0.08f)?.Configure(0.08f, 0.62f, 10);
            for (int i = 0; i < 6; i++) {
                Vector2 dir = (MathHelper.TwoPi * i / 6f + Main.rand.NextFloat(-0.2f, 0.2f))
                    .ToRotationVector2();
                PRTLoader.NewParticle<PRT_Line>(pos + dir * 4f,
                    dir * Main.rand.NextFloat(3f, 6f),
                    Color.Lerp(Accent, Color.White, 0.5f) * 0.8f,
                    Main.rand.NextFloat(0.35f, 0.5f))?.Configure(false, 9);
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_KikasaInkBead>(pos + Main.rand.NextVector2Circular(6f, 4f),
                    Main.rand.NextVector2Circular(3f, 2f) - Vector2.UnitY * 2f,
                    Main.rand.NextBool(3) ? IceDeep : Accent,
                    Main.rand.NextFloat(0.14f, 0.22f))?.Configure(Main.rand.Next(14, 24));
            }
            //脆响：晶碎音抬一截音高，配一记高频溅点
            KikasaInk.Play(SoundID.Item27, pos, 0.5f, 0.3f, 3);
            KikasaInk.Play(KikasaInk.InkSplash, pos, 0.3f, 0.6f, 3);
        }
    }

    /// <summary>
    /// 霰·霰珠：大滴碎出的小冰珠，重力弹跳，落地可再弹两次，
    /// 命中或弹尽即碎成霜屑。棱面白珠+速度残线，读作"冻实的雨"
    /// </summary>
    internal class FuXianHailPellet : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private int bounces;
        private float life;

        /// <summary>确定性相位：自旋方向各端一致</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            life++;
            //霰是冻实的雨：必须一路加速砸下去
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.34f, 15f);
            Projectile.rotation += Projectile.velocity.X * 0.06f + (Seed > 1.8f ? 0.04f : -0.04f);

            //高速段偶发晶闪（端本地）
            if (!Main.dedServ && Projectile.velocity.Length() > 6f && Main.rand.NextBool(9)) {
                PRTLoader.NewParticle<PRT_Line>(
                    Projectile.Center - Projectile.velocity * 0.5f,
                    Projectile.velocity * 0.12f,
                    Color.Lerp(FuXianFX.Accent, Color.White, 0.4f) * 0.55f, 0.3f)
                    ?.Configure(false, 7);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            bounces++;
            //弹力耗尽或超次即碎
            if (bounces > FuXian.PelletBounces
                || (MathF.Abs(oldVelocity.Y) < 2f && MathF.Abs(oldVelocity.X) < 2f)) {
                return true;
            }
            if (Projectile.velocity.X != oldVelocity.X) {
                Projectile.velocity.X = -oldVelocity.X * 0.72f;
            }
            if (Projectile.velocity.Y != oldVelocity.Y) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.55f;
            }
            if (!Main.dedServ) {
                PRTLoader.NewParticle<PRT_KikasaInkBead>(Projectile.Bottom, -Vector2.UnitY * 1.4f,
                    FuXianFX.Accent, 0.16f)?.Configure(10);
                if (bounces == 1 && Main.rand.NextBool(2)) {
                    KikasaInk.Play(SoundID.Item27, Projectile.Center, 0.2f, 0.55f, 2);
                }
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //霜屑迸散：小而脆的一口
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_KikasaInkBead>(
                    Projectile.Center + Main.rand.NextVector2Circular(4f, 3f),
                    Main.rand.NextVector2Circular(2.2f, 1.6f) - Vector2.UnitY * 1.2f,
                    Main.rand.NextBool(3) ? FuXianFX.IceDeep : FuXianFX.Accent,
                    Main.rand.NextFloat(0.12f, 0.18f))?.Configure(Main.rand.Next(12, 20));
            }
            PRTLoader.NewParticle<PRT_Line>(Projectile.Center, -Vector2.UnitY * 0.8f,
                Color.White * 0.6f, 0.3f)?.Configure(false, 7);
            KikasaInk.Play(SoundID.Item27, Projectile.Center, 0.24f, 0.45f, 2);
        }

        /// <summary>棱面白珠：深缘垫底、近白珠体、错角复描一层读出棱面，白芯提亮</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float alpha = MathHelper.Clamp(life / 4f, 0f, 1f);
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.03f, 0f, 0.5f);
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 scale = new Vector2(10f * (1f - stretch * 0.3f), 10f * (1f + stretch)) / tex.Width;

            Main.EntitySpriteDraw(tex, pos, null, FuXianFX.IceDeep * (alpha * 0.75f),
                Projectile.rotation, origin, scale * 1.25f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, FuXianFX.Accent * (alpha * 0.95f),
                Projectile.rotation, origin, scale, SpriteEffects.None, 0);
            //错角复描：读出一点棱面感
            Main.EntitySpriteDraw(tex, pos, null, FuXianFX.Accent * (alpha * 0.35f),
                Projectile.rotation + MathHelper.PiOver4, origin, scale * 0.85f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos + new Vector2(-1f, -1.5f), null,
                Color.White * (alpha * 0.6f), Projectile.rotation, origin,
                scale * 0.4f, SpriteEffects.None, 0);
            return false;
        }
    }
}
