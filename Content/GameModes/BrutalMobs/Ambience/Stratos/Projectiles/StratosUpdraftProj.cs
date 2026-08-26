using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Stratos.Projectiles
{
    /// <summary>
    /// 「星屑升流」可见上升气流柱（场地实体，无伤害的甜头）。
    /// ai[0]=柱半宽 ai[1]=存续帧 ai[2]=柱高，生成位置为柱底锚点。
    /// 成型 30 帧 → 存续 → 消散 30 帧；柱内玩家获得温和上推（施加逻辑在
    /// <see cref="StratosPlayer.PreUpdateMovement"/>，可见柱=助力区，绘制与判定读同一几何）
    /// </summary>
    internal class StratosUpdraftProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>柱内每帧上推加速度（×成型度）</summary>
        public const float LiftAccel = 0.34f;
        /// <summary>上推速度封顶（温和助力，不是弹射器）</summary>
        public const float LiftMax = 7f;

        private const int FadeInFrames = 30;
        private const int FadeOutFrames = 30;
        /// <summary>助力生效的最低成型度</summary>
        private const float LiftGate = 0.25f;

        private float HalfWidth => Projectile.ai[0];
        private int ActiveFrames => (int)Projectile.ai[1];
        private float HeightPx => Projectile.ai[2];
        private int TotalLife => FadeInFrames + ActiveFrames + FadeOutFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        /// <summary>成型度 0~1：淡入抬升、存续满值、消散回落</summary>
        private float Envelope {
            get {
                int elapsed = Elapsed;
                if (elapsed < FadeInFrames) {
                    return elapsed / (float)FadeInFrames;
                }
                if (elapsed < FadeInFrames + ActiveFrames) {
                    return 1f;
                }
                return MathHelper.Clamp(1f - (elapsed - FadeInFrames - ActiveFrames) / (float)FadeOutFrames, 0f, 1f);
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 850;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = false;//纯甜头场地，恒无伤害
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>助力几何与强度：可见柱=判定柱，成型不足时不给力</summary>
        public bool TryGetLift(out Rectangle zone, out float strength) {
            strength = Envelope;
            zone = new Rectangle((int)(Projectile.Center.X - HalfWidth), (int)(Projectile.Center.Y - HeightPx),
                (int)(HalfWidth * 2f), (int)HeightPx + 8);
            return strength > LiftGate;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                //存续期由 ai[1] 决定，两端以同一 ai 值各自展开时间轴
                Projectile.timeLeft = TotalLife;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.28f, Pitch = 0.8f, MaxInstances = 4 }, Projectile.Center);
                }
            }

            if (Main.dedServ) {
                return;
            }

            //屏外剔除：柱不在本机视野附近就省掉粒子预算
            Rectangle view = new((int)Main.screenPosition.X - 320, (int)Main.screenPosition.Y - 320,
                Main.screenWidth + 640, Main.screenHeight + 640);
            Rectangle column = new((int)(Projectile.Center.X - HalfWidth), (int)(Projectile.Center.Y - HeightPx),
                (int)(HalfWidth * 2f), (int)HeightPx);
            if (!view.Intersects(column)) {
                return;
            }

            float envelope = Envelope;
            if (envelope <= 0.05f) {
                return;
            }

            //柱内星屑上涌（≤1/3 帧）+ 偶发气团（≤1/9 帧）
            if (Main.rand.NextBool(3)) {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth) * 0.85f,
                        -Main.rand.NextFloat(0f, HeightPx)),
                    DustID.YellowStarDust,
                    new Vector2(Main.windSpeedCurrent * 0.4f + Main.rand.NextFloat(-0.2f, 0.2f),
                        -Main.rand.NextFloat(2.2f, 4.8f) * envelope),
                    150, default, Main.rand.NextFloat(0.6f, 1.05f));
                dust.noGravity = true;
            }
            if (Main.rand.NextBool(9)) {
                Dust cloud = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth) * 0.6f,
                        -Main.rand.NextFloat(0f, HeightPx * 0.7f)),
                    DustID.Cloud, new Vector2(0f, -Main.rand.NextFloat(1f, 2.2f) * envelope),
                    200, default, Main.rand.NextFloat(0.9f, 1.4f));
                cloud.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center - new Vector2(0f, HeightPx * 0.5f),
                new Vector3(0.10f, 0.14f, 0.22f) * envelope);
        }

        public override bool PreDraw(ref Color lightColor) {
            float envelope = Envelope;
            if (envelope <= 0.02f) {
                return false;
            }
            Texture2D spindle = CWRAsset.Extra_98?.Value;
            Texture2D sparkle = CWRAsset.StarGlow01?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (spindle == null || sparkle == null || glow == null) {
                return false;
            }

            Vector2 mid = Projectile.Center - new Vector2(0f, HeightPx * 0.5f) - Main.screenPosition;
            Vector2 spindleOrig = spindle.Size() * 0.5f;

            //气流柱体：真 alpha 梭形双层（梭形两端自带渐没，不许一刀切），可见宽=助力区宽
            Vector2 bodyScale = new(HalfWidth * 2.4f / spindle.Width, HeightPx * 1.1f / spindle.Height);
            Main.EntitySpriteDraw(spindle, mid, null, new Color(140, 185, 235) * (0.15f * envelope),
                0f, spindleOrig, bodyScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(spindle, mid, null, new Color(175, 210, 250) * (0.13f * envelope),
                0f, spindleOrig, new Vector2(bodyScale.X * 0.45f, bodyScale.Y * 0.96f), SpriteEffects.None, 0);

            //上升星点：确定性相位循环，底生顶散（两端 sin 包络归零）
            for (int k = 0; k < 7; k++) {
                float speed = 0.14f + 0.03f * (k % 3);
                float cycle = (Main.GlobalTimeWrappedHourly * speed + k * 0.37f + Projectile.identity * 0.13f) % 1f;
                float sway = MathF.Sin(Projectile.identity * 1.3f + k * 2.1f + cycle * MathHelper.Pi) * HalfWidth * 0.55f;
                Vector2 sparkPos = Projectile.Center + new Vector2(sway, -cycle * HeightPx) - Main.screenPosition;
                float fade = MathF.Sin(cycle * MathHelper.Pi);
                Main.EntitySpriteDraw(sparkle, sparkPos, null,
                    new Color(210, 232, 255, 0) * (0.5f * envelope * fade), cycle * 4f + k,
                    sparkle.Size() * 0.5f, 0.11f + 0.03f * (k % 3), SpriteEffects.None, 0);
            }

            //柱脚微光：气流从这里被搅起
            Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null,
                new Color(170, 205, 245, 0) * (0.22f * envelope), 0f, glow.Size() * 0.5f,
                new Vector2(HalfWidth * 2.2f / glow.Width, 0.4f), SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                Dust cloud = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth),
                        -Main.rand.NextFloat(0f, HeightPx * 0.5f)),
                    DustID.Cloud, new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.4f, 1.2f)),
                    200, default, Main.rand.NextFloat(0.8f, 1.2f));
                cloud.noGravity = true;
            }
        }
    }
}
