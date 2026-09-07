using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles
{
    /// <summary>
    /// 瞬现长枪：不飞的 hitscan 线。出生即定位定向，fireDelay 帧后整条线致命 6f。
    /// ai[0]=角度 ai[1]=fireDelay ai[2]=宿主 whoAmI。线以中心向两侧各伸 HalfLength。
    /// 枪体=原版 919 贴图后退再前冲+五道幻影汇拢；音只播离屏幕最近的一根
    /// </summary>
    internal class EmpressHitscanLance : ModProjectile, IEmpressAttack, IPrimitiveDrawable
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FairyQueenLance;

        public EmpressScorchTier ScorchTier => EmpressScorchTier.Medium;
        public float FeedbackIntensity => 0.8f;

        internal const float HalfLength = 1300f;
        internal const int DamageFrames = 6;
        private const int AfterFrames = 45;
        private const int BuildupLead = 36;
        private const int BlinkLead = 25;

        private ref float Angle => ref Projectile.ai[0];
        private int FireDelay => Math.Max((int)Projectile.ai[1], 12);
        private NPC Host => ((int)Projectile.ai[2]).TryGetNPC(out NPC n) ? n : null;
        private ref float Timer => ref Projectile.localAI[0];

        private bool Fired => Timer >= FireDelay;
        private float Hue => (Projectile.identity * 0.211f) % 1f;
        private static float DayBlend => VaultUtils.isServer ? 0f : EmpressDayDrive.Intensity;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3000;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            if (Timer == 0f) {
                Projectile.timeLeft = FireDelay + AfterFrames;
                Projectile.rotation = Angle;
            }
            Timer++;
            Projectile.velocity = Vector2.Zero;

            NPC host = Host;
            if (host == null || !host.active) {
                Projectile.Kill();
                return;
            }

            Vector2 dir = Angle.ToRotationVector2();
            Color light = EmpressMotion.FormColor(Hue, DayBlend, 0.62f);
            Lighting.AddLight(Projectile.Center, light.ToVector3() * (Fired ? 0.9f : 0.35f));

            if (VaultUtils.isServer) {
                return;
            }

            //预告期：沿线漂的细光丝（越靠中心越密）
            if (Timer <= FireDelay - 20 && Main.rand.NextFloat() < 0.33f) {
                float side = MathF.Pow(Main.rand.NextFloat(), 2f) * (Main.rand.NextBool() ? 1f : -1f) * 26f;
                Vector2 pos = Projectile.Center + dir * Main.rand.NextFloat(-HalfLength * 0.8f, HalfLength * 0.8f) + dir.RotatedBy(MathHelper.PiOver2) * side;
                if (Vector2.Distance(pos, Main.LocalPlayer.Center) < 2000f) {
                    PRTLoader.NewParticle<PRT_EmpressSpark>(pos, -dir * Main.rand.NextFloat(1f, 3f), light * 0.6f,
                        Main.rand.NextFloat(0.5f, 0.9f))?.Configure(18, Hue, DayBlend);
                }
            }

            //蓄力音固定在 fd-36，发射音在 fd；两者都只播最近一根
            if (Timer == FireDelay - BuildupLead) {
                EmpressProjectileSystem.EnqueueBuildup(NearestPointToScreen(), SoundID.DD2_DarkMageCastHeal with { Volume = 0.6f, Pitch = -0.2f });
            }
            if (Timer == FireDelay) {
                Vector2 near = NearestPointToScreen();
                EmpressProjectileSystem.EnqueueShot(near, SoundID.Item162 with { Volume = 0.9f, Pitch = Main.rand.NextFloat(-0.2f, 0.2f) });
                EmpressMotion.ShakeAlong(near, dir, 3f, 8);
                EmpressScreenFX.PushFlash(dir, 0.12f * (1f - Math.Min(Vector2.Distance(near, Main.LocalPlayer.Center) / 900f, 1f)));
            }

            //发射后 10f：火花线沿枪身从中心向两端跑（视觉上"切过去"，命中却是瞬时的）
            if (Fired && Timer <= FireDelay + 10) {
                float p = (Timer - FireDelay) / 10f;
                for (int s = -1; s <= 1; s += 2) {
                    Vector2 pos = Projectile.Center + dir * s * (HalfLength * (p + Main.rand.NextFloat(0.1f)));
                    if (Vector2.Distance(pos, Main.LocalPlayer.Center) > 2000f) {
                        continue;
                    }
                    Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f) * Main.rand.NextFloat()) * s * Main.rand.NextFloat(15f, 25f);
                    PRTLoader.NewParticle<PRT_EmpressSpark>(pos, vel, EmpressMotion.FormColor(Hue, DayBlend, 0.72f),
                        Main.rand.NextFloat(0.9f, 1.4f))?.Configure(16, Hue, DayBlend);
                }
            }
        }

        private Vector2 NearestPointToScreen() {
            Vector2 dir = Angle.ToRotationVector2();
            Vector2 me = Main.LocalPlayer.Center;
            float along = MathHelper.Clamp(Vector2.Dot(dir, me - Projectile.Center), -HalfLength, HalfLength);
            return Projectile.Center + dir * along;
        }

        public override bool? CanDamage() => Fired && Timer <= FireDelay + DamageFrames - 1 ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 dir = Angle.ToRotationVector2();
            //对角线时 AABB 膨胀过大，按 1/(1+0.414|sin2θ|) 修正内缩
            float pad = 6f / (1f + 0.41421357f * MathF.Abs(MathF.Sin(Angle * 2f)));
            float p = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft() - new Vector2(pad), targetHitbox.Size() + new Vector2(pad * 2f),
                Projectile.Center - dir * HalfLength, Projectile.Center + dir * HalfLength, 8f, ref p);
        }

        /// <summary>实体批：原版长枪贴图后退→前冲，五道幻影从横向汇拢再沿枪拉成拖影</summary>
        public override bool PreDraw(ref Color lightColor) {
            if (Fired) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 dir = Angle.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            float n = MathHelper.Clamp(Timer / FireDelay, 0f, 1f);
            float snap = Math.Max(6f * n - 5f, 0f);//最后 1/6 段前冲
            float smooth = MathHelper.SmoothStep(0f, 1f, n);
            Vector2 slide = dir * (-(1f - n) * (1f - n) * 25f + snap * snap * 20f);
            Color body = EmpressMotion.FormColor(Hue, DayBlend, 0.66f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;

            for (int i = -2; i <= 2; i++) {
                if (i == 0) {
                    continue;
                }
                Vector2 offset = slide + perp * (i * 9f * (1f - smooth)) + dir * (MathF.Pow(snap, 1.5f) * 15f * i);
                float alpha = 0.33f * smooth * smooth * (1f - MathF.Abs(i) / 3f);
                Main.spriteBatch.Draw(tex, drawPos + offset, null, (body with { A = 0 }) * alpha, Angle, origin, 0.6f, SpriteEffects.None, 0f);
            }
            Main.spriteBatch.Draw(tex, drawPos + slide, null, body * (0.35f + 0.65f * smooth), Angle, origin, 0.6f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, drawPos + slide, null, (Color.White with { A = 0 }) * (0.5f * snap), Angle, origin, 0.6f, SpriteEffects.None, 0f);
            return false;
        }

        /// <summary>图元层：预告线（带 fd-25 眨眼）与发射切片</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 dir = Angle.ToRotationVector2();
            Vector2 start = Projectile.Center - dir * HalfLength;
            Color c = EmpressMotion.FormColor(Hue, DayBlend, 0.7f);

            if (!Fired) {
                int fd = FireDelay;
                float n = 1f - MathHelper.Clamp((fd - Timer) / Math.Min(40f, fd), 0f, 1f);
                float blinkOn = Utils.GetLerpValue(40f, 60f, fd, true);
                float blink = Math.Min(MathF.Abs(Timer - (fd - BlinkLead)) / 3.5f * blinkOn + (1f - blinkOn), 1f);
                float bright = 0.6f * ((1f - n) * MathF.Sqrt(n) + n * n + 0.2f * (1f - MathF.Pow(n, 5f))) * blink;
                float halfW = 18f + 26f * n;
                EmpressBeamDraw.DrawMarker(start, dir, HalfLength * 2f, halfW, c, bright, n, n * n * 0.8f, 1f - n, 0.25f);
                return;
            }

            float since = Timer - FireDelay;
            float decay = MathF.Exp(-since / 7f);
            if (decay < 0.03f) {
                return;
            }
            //切片：前 6f 满强，之后指数衰减；宽度随衰减收
            float strength = since < DamageFrames ? 1f : decay;
            Color tint = Color.Lerp(Color.White, new Color(255, 240, 210), DayBlend) * strength;
            EmpressBeamDraw.DrawSunbeam(start, dir, HalfLength * 2f, 34f * strength + 6f, Hue, MathHelper.Clamp(strength, 0.1f, 1f), tint);
        }
    }
}
