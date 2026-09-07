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
    /// 万华镜鞭击：鞭尖落点在第一拍标出（出生即锁），下一小节第一拍整条鞭线抽下致命 6f，鞭尖另有一圈爆点。
    /// 鞭身是从她手到落点的二次贝塞尔（向上拱），用原版万华镜的手柄与鞭尖帧 + 光谱条带画。
    /// ai[0]/ai[1]=鞭尖世界坐标 ai[2]=宿主 whoAmI + 落拍延迟×256；强击 tipRadius 大且反馈重
    /// </summary>
    internal class EmpressWhipCrack : ModProjectile, IEmpressAttack, IPrimitiveDrawable
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.RainbowWhip;

        public EmpressScorchTier ScorchTier => Strong ? EmpressScorchTier.Heavy : EmpressScorchTier.Medium;
        public float FeedbackIntensity => Strong ? 1.25f : 0.8f;

        internal const int DamageFrames = 6;
        private const int AfterFrames = 30;
        private const int Segments = 28;

        private Vector2 Tip => new(Projectile.ai[0], Projectile.ai[1]);
        private int HostIndex => (int)Projectile.ai[2] & 255;
        private int FireDelay => Math.Max(((int)Projectile.ai[2] >> 8) & 255, 12);
        /// <summary>强击标记：ai[2] 第 16 位</summary>
        private bool Strong => (((int)Projectile.ai[2] >> 16) & 1) == 1;
        private NPC Host => HostIndex.TryGetNPC(out NPC n) ? n : null;
        private ref float Timer => ref Projectile.localAI[0];

        private bool Fired => Timer >= FireDelay;
        private float TipRadius => Strong ? 110f : 70f;
        private float Hue => (Projectile.identity * 0.23f) % 1f;
        private static float DayBlend => VaultUtils.isServer ? 0f : EmpressDayDrive.Intensity;

        private readonly Vector2[] curve = new Vector2[Segments + 1];
        private Vector2 root;

        /// <summary>打包 ai[2]</summary>
        internal static float PackAi2(int host, int fireDelay, bool strong) => host + (fireDelay << 8) + ((strong ? 1 : 0) << 16);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;

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

        /// <summary>鞭根：她面向落点那一侧的手</summary>
        private void UpdateRoot(NPC host) {
            float side = Math.Sign(Tip.X - host.Center.X);
            if (side == 0f) {
                side = 1f;
            }
            root = host.Center + new Vector2(side * 55f, -30f);
        }

        /// <summary>二次贝塞尔：控制点在弦中点向"上"偏 30% 弦长，鞭身像甩出去的弧</summary>
        private void BuildCurve() {
            Vector2 tip = Tip;
            Vector2 chord = tip - root;
            float len = chord.Length();
            Vector2 perp = chord.SafeNormalize(Vector2.UnitX).RotatedBy(-MathHelper.PiOver2);
            if (perp.Y > 0f) {
                perp = -perp;
            }
            Vector2 control = root + chord * 0.5f + perp * (len * 0.3f);
            for (int i = 0; i <= Segments; i++) {
                float t = i / (float)Segments;
                float inv = 1f - t;
                curve[i] = root * (inv * inv) + control * (2f * inv * t) + tip * (t * t);
            }
        }

        public override void AI() {
            NPC host = Host;
            if (host == null || !host.active) {
                Projectile.Kill();
                return;
            }
            if (Timer == 0f) {
                Projectile.timeLeft = FireDelay + AfterFrames;
            }
            Timer++;
            Projectile.Center = Tip;
            Projectile.velocity = Vector2.Zero;
            UpdateRoot(host);
            BuildCurve();

            Color light = EmpressMotion.FormColor(Hue, DayBlend, 0.62f);
            Lighting.AddLight(Tip, light.ToVector3() * (Fired ? 1f : 0.4f));
            if (VaultUtils.isServer) {
                return;
            }

            //预告：落点环上的细碎光屑越聚越紧
            float t = MathHelper.Clamp(Timer / FireDelay, 0f, 1f);
            if (!Fired && Main.rand.NextFloat() < 0.4f) {
                Vector2 spawn = Tip + Main.rand.NextVector2CircularEdge(TipRadius, TipRadius) * (1.3f - 0.3f * t);
                PRTLoader.NewParticle<PRT_EmpressSpark>(spawn, (Tip - spawn) * 0.06f, EmpressMotion.FormRim(Hue + Main.rand.NextFloat(0.2f), DayBlend, 0.6f) * 0.8f,
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(14, Hue, DayBlend);
            }

            if (Timer == FireDelay) {
                OnCrack();
            }
            //抽下后火花沿鞭身从根跑到尖
            if (Fired && Timer <= FireDelay + 8) {
                float p = (Timer - FireDelay) / 8f;
                int idx = Math.Min((int)(p * Segments), Segments - 1);
                Vector2 dir = (curve[idx + 1] - curve[idx]).SafeNormalize(Vector2.UnitX);
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_EmpressSpark>(curve[idx], dir.RotatedBy(Main.rand.NextFloat(-0.6f, 0.6f)) * Main.rand.NextFloat(6f, 14f),
                        EmpressMotion.FormRim(Hue + p * 0.4f, DayBlend, 0.66f), Main.rand.NextFloat(0.8f, 1.3f))?.Configure(16, Hue, DayBlend);
                }
            }
        }

        private void OnCrack() {
            EmpressProjectileSystem.EnqueueShot(Tip, SoundID.Item153 with { Volume = Strong ? 1.1f : 0.85f, Pitch = Strong ? -0.15f : 0.1f });
            Vector2 lashDir = (curve[Segments] - curve[Segments - 1]).SafeNormalize(Vector2.UnitX);
            EmpressMotion.ShakeAlong(Tip, lashDir, Strong ? 9f : 5f, 12);
            EmpressScreenFX.PushFlash(lashDir, (Strong ? 0.35f : 0.18f) * (1f - Math.Min(Vector2.Distance(Tip, Main.LocalPlayer.Center) / 1000f, 1f)));
            //鞭尖爆点：一圈涟漪 + 沿鞭向抛出的光谱碎屑
            PRTLoader.NewParticle<PRT_EmpressRipple>(Tip, Vector2.Zero, Color.White, Strong ? 1.1f : 0.7f)?.Configure(16, Hue, DayBlend);
            EmpressMotion.SparkBurst(Tip, lashDir, Strong ? 26 : 14, 4f, 13f, DayBlend, 0.9f);
        }

        public override bool? CanDamage() => Fired && Timer <= FireDelay + DamageFrames - 1 ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //鞭尖圆 + 鞭身折线（14px 半宽）
            if (Vector2.Distance(Tip, targetHitbox.ClosestPointInRect(Tip)) <= TipRadius) {
                return true;
            }
            float p = 0f;
            for (int i = 0; i < Segments; i++) {
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), curve[i], curve[i + 1], 28f, ref p)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>实体批：落点环（预告期收拢）、抽下后原版万华镜的手柄帧在根、鞭尖帧在尖</summary>
        public override bool PreDraw(ref Color lightColor) {
            float day = DayBlend;
            Color rim = EmpressMotion.FormRim(Hue, day, 0.6f);
            Color core = EmpressMotion.FormColor(Hue, day, 0.7f);
            if (!Fired) {
                float t = MathHelper.Clamp(Timer / FireDelay, 0f, 1f);
                //落点环：从 1.5 倍收到命中半径，末段爆亮
                float r = TipRadius * (1.5f - 0.5f * t);
                float glow = MathF.Pow(t, 6f);
                ShockRingDraw.Draw(Main.spriteBatch, Tip, r, 6f + 6f * t, Color.Lerp(core, Color.White, glow), rim, EmpressMotion.FormDark(day),
                    0.35f + 0.55f * t, 4f, 1f, 0.05f * t, Projectile.identity * 0.3f);
                return false;
            }

            Texture2D sheet = TextureAssets.Projectile[Type].Value;
            //原版 DrawWhip_RainbowWhip 按 Frame(1,5) 切：0 手柄 / 1~3 鞭段 / 4 鞭尖
            const int frames = 5;
            int frameH = sheet.Height / frames;
            float since = Timer - FireDelay;
            float fade = 1f - MathHelper.Clamp(since / AfterFrames, 0f, 1f);
            Color tint = core * fade;
            //手柄帧在根，指向第一段切线
            Rectangle handle = new(0, 0, sheet.Width, frameH);
            Vector2 rootDir = (curve[1] - curve[0]).SafeNormalize(Vector2.UnitX);
            Main.spriteBatch.Draw(sheet, root - Main.screenPosition, handle, tint, rootDir.ToRotation() + MathHelper.PiOver2,
                new Vector2(sheet.Width / 2f, frameH / 2f), 1f, SpriteEffects.None, 0f);
            //鞭尖帧在尖，指向末段切线
            Rectangle tipRect = new(0, (frames - 1) * frameH, sheet.Width, frameH);
            Vector2 tipDir = (curve[Segments] - curve[Segments - 1]).SafeNormalize(Vector2.UnitX);
            Main.spriteBatch.Draw(sheet, Tip - Main.screenPosition, tipRect, tint, tipDir.ToRotation() + MathHelper.PiOver2,
                new Vector2(sheet.Width / 2f, frameH / 2f), 1.1f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(sheet, Tip - Main.screenPosition, tipRect, (Color.White with { A = 0 }) * (0.6f * fade * MathF.Exp(-since / 5f)), tipDir.ToRotation() + MathHelper.PiOver2,
                new Vector2(sheet.Width / 2f, frameH / 2f), 1.1f, SpriteEffects.None, 0f);
            return false;
        }

        /// <summary>图元层：预告期细鞭线预览；抽下后光谱条带鞭身，6f 满亮再指数收</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (VaultUtils.isServer || Timer <= 0f) {
                return;
            }
            float day = DayBlend;
            if (!Fired) {
                float t = MathHelper.Clamp(Timer / FireDelay, 0f, 1f);
                Color c = EmpressMotion.FormColor(Hue, day, 0.7f);
                EmpressBeamDraw.DrawCurveMarker(curve, 6f, 10f + 8f * t, c, 0.25f + 0.35f * t, t, t * t, 1f - t, 0.5f);
                return;
            }
            float since = Timer - FireDelay;
            float strength = since < DamageFrames ? 1f : MathF.Exp(-(since - DamageFrames) / 6f);
            if (strength < 0.03f) {
                return;
            }
            Color tint = Color.Lerp(Color.White, new Color(255, 245, 225), day) * strength;
            EmpressBeamDraw.DrawCurveSunbeam(curve, 10f, 30f * strength + 8f, Hue, MathHelper.Clamp(strength, 0.1f, 1f), tint);
        }
    }
}
