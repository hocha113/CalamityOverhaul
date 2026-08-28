using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Brinefume.Projectiles
{
    /// <summary>
    /// 「毒霾潮」：随风向缓移的黄绿雾墙（残酷模式硫磺海环境机制）。
    /// 远处即可见、移速极缓；滞留其中累积中毒（无直接伤害），绕行或等它漂过即可。
    /// 曝露量各端只记自己的（BrinefumePlayer），减益本机 AddBuff 原生同步；
    /// 漂移读各端同源的风速，服务端低频 netUpdate 兜底对齐
    /// </summary>
    internal class BrinefumeHazeTideProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeFrames = 2700;
        private const int FadeInFrames = 90;
        private const int FadeOutFrames = 120;
        /// <summary>雾墙半宽/半高（可见即判定）</summary>
        internal const float HalfWidth = 170f;
        internal const float HalfHeight = 260f;
        /// <summary>生成锚点相对水面的抬升量（墙体下缘略浸水面）</summary>
        internal const float AnchorLift = HalfHeight - 60f;
        /// <summary>开始挂毒前的滞留宽限（给"走进去又立刻退出来"留反应窗）。
        /// 挂毒节拍按 30 帧对齐，本值须取 30 的整倍数才是真实首毒帧（旧值 40 实际在第 60 帧才起毒）</summary>
        private const int GraceTicks = 90;
        /// <summary>曝露累积上限</summary>
        private const int ExposureCap = 1800;

        private int Dir => Projectile.ai[0] >= 0f ? 1 : -1;

        /// <summary>淡入淡出包络 0~1</summary>
        private float Env {
            get {
                int elapsed = LifeFrames - Projectile.timeLeft;
                float fadeIn = Math.Min(elapsed / (float)FadeInFrames, 1f);
                float fadeOut = MathHelper.Clamp(Projectile.timeLeft / (float)FadeOutFrames, 0f, 1f);
                return Math.Min(fadeIn, fadeOut);
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = false;//无直接伤害，滞留减益走本机结算
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.netImportant = true;
        }

        public override void AI() {
            //随风缓移：底速保底，风大略快（风速是同步世界状态，各端同源推演）
            float drift = Dir * MathHelper.Clamp(0.30f + Math.Abs(Main.windSpeedCurrent) * 0.9f, 0.30f, 1.15f);
            Projectile.velocity = new Vector2(drift, 0f);

            //服务端低频重锚，端间微漂自愈（服务端拥有的弹幕 netUpdate 有效）
            if (VaultUtils.isServer) {
                Projectile.localAI[0]++;
                if (Projectile.localAI[0] >= 150f) {
                    Projectile.localAI[0] = 0f;
                    Projectile.netUpdate = true;
                }
            }

            if (Main.dedServ) {
                return;
            }

            float env = Env;
            //本机玩家滞留结算：曝露渐涨，越滞留毒越久；Boss 在场或模式关闭时暂停
            Player localPlayer = Main.LocalPlayer;
            if (env > 0.35f && localPlayer.active && !localPlayer.dead
                && GameModeSystem.BrutalActive && !CWRWorld.HasBoss
                && BodyRect().Intersects(localPlayer.Hitbox)) {
                BrinefumePlayer brine = localPlayer.GetModPlayer<BrinefumePlayer>();
                if (!brine.HazeSoaked) {
                    brine.HazeSoaked = true;//同帧多面墙不重复计
                    if (brine.HazeExposure < ExposureCap) {
                        brine.HazeExposure++;
                    }
                    if (brine.HazeExposure >= GraceTicks && brine.HazeExposure % 30 == 0) {
                        //减益档现读：残酷中毒，修罗及以上升剧毒；时长随滞留累积
                        int buff = GameModeSystem.EffectiveTier >= 2 ? BuffID.Venom : BuffID.Poisoned;
                        localPlayer.AddBuff(buff, 90 + Math.Min(brine.HazeExposure, 1500) / 3);
                    }
                }
            }

            //雾内浮尘（约 20 粒/秒每面墙）
            if (env > 0.2f && Main.rand.NextBool(3)) {
                Vector2 pos = Projectile.Center + new Vector2(
                    Main.rand.NextFloat(-HalfWidth, HalfWidth) * 0.9f,
                    Main.rand.NextFloat(-HalfHeight, HalfHeight) * 0.9f);
                Dust mote = Dust.NewDustPerfect(pos, DustID.TintableDust,
                    new Vector2(drift * 0.6f + Main.rand.NextFloat(-0.15f, 0.15f),
                        Main.rand.NextFloat(-0.1f, 0.06f)),
                    200, BrinefumeAmbience.MistDeep, Main.rand.NextFloat(1.1f, 1.9f));
                mote.noGravity = true;
                mote.noLight = true;
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.10f, 0.12f, 0.03f) * env);
        }

        /// <summary>墙体范围（可见=判定，滞留结算用）</summary>
        private Rectangle BodyRect() => new(
            (int)(Projectile.Center.X - HalfWidth), (int)(Projectile.Center.Y - HalfHeight),
            (int)(HalfWidth * 2f), (int)(HalfHeight * 2f));

        //确定性伪随机（identity 播种，各端一致，零逐帧分配）
        private float Hash(int i, float salt) =>
            MathF.Sin(Projectile.identity * 7.13f + i * 3.71f + salt) * 0.5f + 0.5f;

        public override bool PreDraw(ref Color lightColor) {
            float env = Env;
            if (env <= 0.01f) {
                return false;
            }
            Texture2D fog = CWRAsset.Fog.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 fogOrigin = fog.Size() * 0.5f;
            float t = Main.GlobalTimeWrappedHourly;
            Vector2 basePos = Projectile.Center - Main.screenPosition;

            //体雾：12 团纵向排布的浓雾（Fog 真 alpha，AlphaBlend 直接染色），慢旋慢摆，逐团镜像防贴纸感
            Color body = Color.Lerp(new Color(96, 104, 44), lightColor, 0.30f);
            for (int i = 0; i < 12; i++) {
                float col = (i % 3 - 1) * HalfWidth * 0.55f;
                float row = (i / 3 + 0.5f) / 4f * 2f - 1f;
                float phase = Hash(i, 0.7f) * MathHelper.TwoPi;
                Vector2 wob = new(
                    MathF.Sin(t * 0.31f + phase) * 16f + Dir * 8f * (row + 1f),
                    MathF.Sin(t * 0.23f + phase * 1.7f) * 10f);
                Vector2 pos = basePos + new Vector2(col, row * HalfHeight * 0.78f) + wob;
                float rot = t * (0.04f + 0.05f * Hash(i, 1.3f)) * (Hash(i, 2.9f) > 0.5f ? 1f : -1f);
                float scale = 1.5f + 1.0f * Hash(i, 4.1f);
                float alpha = (0.30f + 0.14f * Hash(i, 5.3f)) * env;
                Main.EntitySpriteDraw(fog, pos, null, body * alpha, rot, fogOrigin, scale,
                    i % 2 == 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);
            }

            //冠顶亮缘：上排更亮的稀雾，远处可读的"墙头"
            Color crown = Color.Lerp(BrinefumeAmbience.FoamPale, lightColor, 0.25f);
            for (int i = 0; i < 4; i++) {
                float u = (i + 0.5f) / 4f * 2f - 1f;
                float phase = Hash(i + 20, 0.7f) * MathHelper.TwoPi;
                Vector2 pos = basePos + new Vector2(
                    u * HalfWidth * 0.7f + MathF.Sin(t * 0.4f + phase) * 12f,
                    -HalfHeight * 0.92f + MathF.Sin(t * 0.33f + phase) * 8f);
                Main.EntitySpriteDraw(fog, pos, null, crown * (0.16f * env),
                    t * 0.05f * (i % 2 == 0 ? 1f : -1f), fogOrigin,
                    1.1f + 0.4f * Hash(i + 20, 3.3f),
                    i % 2 == 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
            }

            //腹心酸光（加色敷料 A=0）：墙心透出病态微光
            Color acid = BrinefumeAmbience.AcidGlow;
            float breathe = 0.8f + 0.2f * MathF.Sin(t * 1.1f + Projectile.identity);
            Main.EntitySpriteDraw(glow, basePos + new Vector2(0f, HalfHeight * 0.1f), null,
                new Color(acid.R, acid.G, acid.B, 0) * (0.22f * env * breathe), 0f, glow.Size() / 2f,
                new Vector2(HalfWidth * 2.2f / glow.Width, HalfHeight * 1.7f / glow.Height),
                SpriteEffects.None, 0);
            return false;
        }
    }
}
