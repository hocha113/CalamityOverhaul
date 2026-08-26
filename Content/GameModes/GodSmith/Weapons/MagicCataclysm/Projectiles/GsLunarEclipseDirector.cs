using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles
{
    /// <summary>
    /// 月耀灾变「月蚀审判」：锚定光标区。蓄势 45t 上空月蚀盘渐显；
    /// 爆发 150t 蚀盘倾泻 12 连月焰瀑（×1.0，落点错拍编织），每第 3 落补 1 道幻影月光柱（×1.3）；
    /// 余韵 120t 月尘辉光飘落。主控无自身判定，伤害全在月焰与光柱
    /// </summary>
    internal class GsLunarEclipseDirector : GsCataclysmDirectorProj
    {
        public override int OmenTicks => 45;
        public override int MainTicks => 150;
        public override int AftermathTicks => 120;

        /// <summary>蚀盘悬高</summary>
        private const float DiskHeight = 340f;
        /// <summary>十二落错拍编织表（相对锚点的横向落点）</summary>
        private static readonly float[] Weave = [-150f, 40f, -80f, 140f, -20f, 100f, -140f, 20f, -100f, 80f, -40f, 150f];

        [VaultLoaden(CWRConstant.Masking + "Extra_98")]
        internal static Asset<Texture2D> DarkTex = null;

        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        internal static Asset<Texture2D> GlowTex = null;

        internal static readonly Color MoonCyan = new(150, 220, 235);
        internal static readonly Color MoonPale = new(226, 240, 250);
        internal static readonly Color EclipseDusk = new(60, 50, 110);

        private static int FlareType => ContentSamples.ItemsByType[ItemID.LunarFlareBook].shoot;

        private Vector2 DiskPos => Projectile.Center + new Vector2(0f, -DiskHeight);

        /// <summary>蚀盘可见度</summary>
        private float DiskEnvelope() {
            if (Phase == 0) {
                return VaultUtils.EaseOutQuad(Elapsed / (float)OmenTicks);
            }
            if (Phase == 1) {
                return 1f;
            }
            return MathHelper.Clamp(1f - (Elapsed - OmenTicks - MainTicks) / (float)AftermathTicks, 0f, 1f);
        }

        protected override void OmenUpdate(int t) {
            if (t == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.7f, Pitch = -0.45f }, DiskPos);
            }
            //月尘自盘缘剥落
            if (!VaultUtils.isServer && t % 5 == 0) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                PRTLoader.NewParticle<PRT_Sparkle>(DiskPos + angle.ToRotationVector2() * 56f,
                    angle.ToRotationVector2() * 0.5f, MoonCyan, Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(MoonCyan, 26);
            }
        }

        protected override void MainUpdate(int t) {
            //十二连月焰瀑：每 12t 一落，第 3 的倍数落补月光柱
            if (t % 12 == 3 && t / 12 < Weave.Length) {
                int k = t / 12;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item88 with { Volume = 0.55f, Pitch = 0.1f + k * 0.02f }, DiskPos);
                }
                if (OwnerSide) {
                    Vector2 landing = Projectile.Center + new Vector2(Weave[k], 0f);
                    Vector2 spawn = DiskPos + new Vector2(Weave[k] * 0.3f, 0f);
                    Vector2 vel = (landing - spawn).SafeNormalize(Vector2.UnitY) * 17f;
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), spawn, vel,
                        FlareType, ScaledDamage(1f), Projectile.knockBack, Projectile.owner);
                    if (k % 3 == 2) {
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(), landing, Vector2.Zero,
                            ModContent.ProjectileType<GsMoonPillarProj>(), ScaledDamage(1.3f),
                            Projectile.knockBack, Projectile.owner);
                    }
                }
            }
            Lighting.AddLight(DiskPos, MoonCyan.ToVector3() * 0.6f);
        }

        protected override void AftermathUpdate(int t) {
            //月尘辉光飘落
            if (!VaultUtils.isServer && t % 6 == 0) {
                Vector2 pos = DiskPos + new Vector2(Main.rand.NextFloat(-120f, 120f), Main.rand.NextFloat(-30f, 50f));
                PRTLoader.NewParticle<PRT_HeavenfallStar>(pos,
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(0.8f, 1.8f)),
                    Color.Lerp(MoonCyan, MoonPale, Main.rand.NextFloat()), Main.rand.NextFloat(0.35f, 0.55f))
                    ?.Configure(false, 40);
            }
            Lighting.AddLight(DiskPos, EclipseDusk.ToVector3() * 1.2f * (1f - t / (float)AftermathTicks));
        }

        /// <summary>主控恒无判定，伤害全在月焰与光柱</summary>
        public override bool? CanDamage() => false;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D dark = DarkTex?.Value;
            Texture2D glow = GlowTex?.Value;
            float env = DiskEnvelope();
            if (dark == null || glow == null || env <= 0.02f) {
                return false;
            }
            Vector2 diskScreen = DiskPos - Main.screenPosition;
            float breathe = 0.9f + 0.1f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 1.8f + Projectile.identity * 0.61f);
            //外晕
            Main.EntitySpriteDraw(glow, diskScreen, null, EclipseDusk with { A = 0 } * (0.55f * env), 0f,
                glow.Size() * 0.5f, 210f / glow.Width * breathe, SpriteEffects.None, 0);
            //月牙亮缘：亮层错位于暗盘后方露出一弯
            Main.EntitySpriteDraw(glow, diskScreen + new Vector2(9f, -7f), null, MoonPale with { A = 0 } * (0.75f * env), 0f,
                glow.Size() * 0.5f, 108f / glow.Width, SpriteEffects.None, 0);
            //暗蚀盘（真 alpha 压暗）
            Main.EntitySpriteDraw(dark, diskScreen, null, new Color(16, 12, 32) * (0.92f * env),
                Projectile.identity * 0.23f + Main.GlobalTimeWrappedHourly * 0.05f,
                dark.Size() * 0.5f, 100f / dark.Width, SpriteEffects.None, 0);
            //盘心冷辉
            Main.EntitySpriteDraw(glow, diskScreen, null, MoonCyan with { A = 0 } * (0.3f * env * breathe), 0f,
                glow.Size() * 0.5f, 66f / glow.Width, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 幻影月光柱：落点竖直光柱，淡入 6t 无伤、实体 10t 判定、消散 14t。
    /// 判定核心宽度小于可见亮体。ai[0]=相位计时
    /// </summary>
    internal class GsMoonPillarProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithMagicCataclysm";

        private const int FadeIn = 6;
        private const int Solid = 10;
        private const int FadeOut = 14;
        private const int LifeTicks = FadeIn + Solid + FadeOut;
        /// <summary>判定核心半宽</summary>
        private const float CoreHalf = 22f;
        /// <summary>柱高（自落点向上）</summary>
        private const float PillarHeight = 480f;

        private ref float Timer => ref Projectile.ai[0];

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;

        public override void SetDefaults() {
            Projectile.width = 44;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = LifeTicks + 4;
        }

        /// <summary>柱体强度包络</summary>
        private float Envelope() {
            float rise = MathHelper.Clamp(Timer / FadeIn, 0f, 1f);
            float fall = MathHelper.Clamp((LifeTicks - Timer) / (float)FadeOut, 0f, 1f);
            return Math.Min(VaultUtils.EaseOutQuad(rise), fall);
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            if (Timer == 0f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.5f, Pitch = 0.45f }, Projectile.Center);
            }
            Timer++;
            if (Timer >= LifeTicks) {
                Projectile.Kill();
                return;
            }
            Lighting.AddLight(Projectile.Center + new Vector2(0f, -PillarHeight * 0.4f),
                GsLunarEclipseDirector.MoonCyan.ToVector3() * 0.5f * Envelope());
        }

        /// <summary>只有实体窗有伤</summary>
        public override bool? CanDamage() => Timer >= FadeIn && Timer < FadeIn + Solid ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Rectangle pillar = new((int)(Projectile.Center.X - CoreHalf), (int)(Projectile.Center.Y - PillarHeight),
                (int)(CoreHalf * 2f), (int)(PillarHeight + 30f));
            return pillar.Intersects(targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = GsLunarEclipseDirector.GlowTex?.Value;
            if (glow == null) {
                return false;
            }
            float env = Envelope();
            Vector2 basePos = Projectile.Center - Main.screenPosition;
            Vector2 mid = basePos + new Vector2(0f, -PillarHeight * 0.5f);
            //可见亮体宽于判定核心：外缘 64px、核心 30px，判定 44px 介于其间偏核心
            Main.EntitySpriteDraw(glow, mid, null, GsLunarEclipseDirector.MoonCyan with { A = 0 } * (0.45f * env),
                MathHelper.PiOver2, glow.Size() * 0.5f,
                new Vector2(PillarHeight + 60f, 64f * env + 6f) / glow.Width, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, mid, null, GsLunarEclipseDirector.MoonPale with { A = 0 } * (0.8f * env),
                MathHelper.PiOver2, glow.Size() * 0.5f,
                new Vector2(PillarHeight + 30f, 30f * env + 3f) / glow.Width, SpriteEffects.None, 0);
            //落点辉光
            Main.EntitySpriteDraw(glow, basePos, null, GsLunarEclipseDirector.MoonPale with { A = 0 } * (0.6f * env),
                0f, glow.Size() * 0.5f, 60f / glow.Width * env, SpriteEffects.None, 0);
            return false;
        }
    }
}
