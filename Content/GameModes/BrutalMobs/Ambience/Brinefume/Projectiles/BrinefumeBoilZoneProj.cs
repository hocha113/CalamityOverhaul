using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Brinefume.Projectiles
{
    /// <summary>
    /// 「酸沸区」：硫磺海水面周期出现的沸腾水域（残酷模式环境机制，酸雾腐蚀原型）。
    /// 生成位置即锁定水面锚点：气泡骤密+嘶嘶声预告 50 帧 → 水体翻滚 330 帧
    /// （浸入水中的玩家吃微量接触伤害并中毒，出水即缓）→ 平息 40 帧。
    /// 可见沸腾范围=判定范围；档位只由调度器调频率，减益档命中现读（tier≥2 升为剧毒）
    /// </summary>
    internal class BrinefumeBoilZoneProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>预告帧数（公平契约 ≥45）</summary>
        private const int TelegraphFrames = 50;
        private const int BoilFrames = 330;
        private const int FadeFrames = 40;
        /// <summary>沸腾半宽（可见=判定）</summary>
        private const float HalfWidth = 150f;
        /// <summary>判定向水下延伸深度</summary>
        private const float DepthPx = 96f;
        /// <summary>中毒时长（tier≥2 换剧毒，时长同）</summary>
        private const int DebuffFrames = 120;

        private const int TotalLife = TelegraphFrames + BoilFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        /// <summary>翻滚强度包络 0~1（预告 0，沸腾 14 帧升满，平息滑落）</summary>
        private float RollEnv {
            get {
                int t = Elapsed - TelegraphFrames;
                if (t <= 0) {
                    return 0f;
                }
                if (t > BoilFrames) {
                    return MathHelper.Clamp(1f - (t - BoilFrames) / (float)FadeFrames, 0f, 1f);
                }
                return Math.Min(t / 14f, 1f);
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 400;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = false;//沸腾窗口内才置真
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int elapsed = Elapsed;
            //判定窗=可见沸腾窗；Boss 在场或模式关闭时机制让位（演出走完，不咬人）
            bool live = GameModeSystem.BrutalActive && !CWRWorld.HasBoss;
            Projectile.hostile = live && elapsed >= TelegraphFrames && elapsed < TelegraphFrames + BoilFrames;

            if (Main.dedServ) {
                return;
            }

            if (elapsed == 0) {
                //嘶嘶声起：预告的听觉通道
                SoundEngine.PlaySound(SoundID.LiquidsWaterLava with {
                    Volume = 0.38f,
                    Pitch = 0.3f,
                    MaxInstances = 4,
                }, Projectile.Center);
            }
            else if (elapsed == TelegraphFrames) {
                //沸腾拍
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Volume = 0.75f,
                    Pitch = -0.35f,
                    MaxInstances = 4,
                }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.LiquidsWaterLava with {
                    Volume = 0.8f,
                    Pitch = -0.12f,
                    MaxInstances = 4,
                }, Projectile.Center);
                for (int i = 0; i < 8; i++) {
                    Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth) * 0.8f, 2f),
                        DustID.Water, new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(2.5f, 6f)),
                        60, default, Main.rand.NextFloat(1.1f, 1.7f));
                }
            }
            else if (elapsed > TelegraphFrames && elapsed < TelegraphFrames + BoilFrames && elapsed % 54 == 0) {
                //沸腾持续底响
                SoundEngine.PlaySound(SoundID.LiquidsWaterLava with {
                    Volume = 0.22f,
                    Pitch = Main.rand.NextFloat(-0.2f, 0.1f),
                    MaxInstances = 4,
                }, Projectile.Center);
            }

            if (elapsed < TelegraphFrames) {
                //预告：气泡骤密（从疏到密，≤2 粒/帧）
                float progress = elapsed / (float)TelegraphFrames;
                if (Main.rand.NextFloat() < 0.35f + 0.65f * progress) {
                    SpawnBubbleDust(progress);
                }
                if (progress > 0.6f && Main.rand.NextBool(3)) {
                    SpawnBubbleDust(progress);
                }
            }
            else if (elapsed < TelegraphFrames + BoilFrames) {
                //沸腾：概率化生成削峰（气泡 ~30/s + 溅水 ~15/s，单区峰值约 45 粒/s）
                if (Main.rand.NextBool(2)) {
                    SpawnBubbleDust(1f);
                }
                if (Main.rand.NextBool(4)) {
                    Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth) * 0.85f, 0f),
                        DustID.Water, new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(2f, 5f)),
                        70, default, Main.rand.NextFloat(0.9f, 1.5f));
                }
                Lighting.AddLight(Projectile.Center, new Vector3(0.14f, 0.18f, 0.05f));
            }
            else if (Main.rand.NextBool(3)) {
                //平息：余泡渐稀
                SpawnBubbleDust(0.4f);
            }
        }

        //贴水面冒起的酸沫泡（预告与沸腾共用，强度控密度与范围）
        private void SpawnBubbleDust(float strength) {
            Dust bubble = Dust.NewDustPerfect(
                Projectile.Center + new Vector2(
                    Main.rand.NextFloat(-HalfWidth, HalfWidth) * (0.35f + 0.65f * strength),
                    Main.rand.NextFloat(0f, 18f)),
                DustID.TintableDust,
                new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(0.8f, 1.8f + strength)),
                150, BrinefumeAmbience.FoamPale, Main.rand.NextFloat(0.7f, 1.15f));
            bubble.noGravity = true;
        }

        /// <summary>判定区：水面往下的浸没带（可见沸腾区=判定区）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            Rectangle zone = new((int)(Projectile.Center.X - HalfWidth), (int)(Projectile.Center.Y - 10f),
                (int)(HalfWidth * 2f), (int)(DepthPx + 10f));
            return zone.Intersects(targetHitbox);
        }

        /// <summary>出水即缓：只有身在水中的玩家会被烫到</summary>
        public override bool CanHitPlayer(Player target) => target.wet;

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            //减益档现读：残酷中毒，修罗及以上升剧毒（命中方本机结算，原生同步）
            int buff = GameModeSystem.EffectiveTier >= 2 ? BuffID.Venom : BuffID.Poisoned;
            target.AddBuff(buff, DebuffFrames);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float progress = MathHelper.Clamp(elapsed / (float)TelegraphFrames, 0f, 1f);
            float roll = RollEnv;
            float widthPx = HalfWidth * 2f * (0.5f + 0.5f * Math.Max(progress, roll));
            Texture2D band = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float t = Main.GlobalTimeWrappedHourly;

            if (roll > 0.02f) {
                //浑浊水带（真 alpha 实体层）：两层错相翻涌
                for (int layer = 0; layer < 2; layer++) {
                    float wob = MathF.Sin(t * (3.1f + layer * 1.7f) + Projectile.identity * 2.3f + layer * 2f) * 6f;
                    Vector2 pos = Projectile.Center + new Vector2(wob, 8f + layer * 9f) - Main.screenPosition;
                    Vector2 scale = new(widthPx * (1f - layer * 0.15f) / band.Width,
                        (22f + 8f * roll) / band.Height);
                    Color murk = BrinefumeAmbience.WaterMurk * ((0.42f - 0.12f * layer) * roll);
                    Main.EntitySpriteDraw(band, pos, null, murk, 0f, band.Size() / 2f, scale, SpriteEffects.None, 0);
                }
                //翻滚浪峰：五个确定性相位的小鼓包
                for (int i = 0; i < 5; i++) {
                    float u = (i + 0.5f) / 5f - 0.5f;
                    float bob = MathF.Sin(t * 5.2f + Projectile.identity * 1.31f + i * 2.4f);
                    Vector2 pos = Projectile.Center
                        + new Vector2(u * widthPx * 0.9f, -2f - MathF.Abs(bob) * 9f * roll)
                        - Main.screenPosition;
                    Vector2 scale = new(26f / band.Width, (14f + 8f * MathF.Abs(bob)) * roll / band.Height);
                    Color hump = Color.Lerp(BrinefumeAmbience.WaterMurk, BrinefumeAmbience.FoamPale,
                        0.35f + 0.25f * bob) * (0.5f * roll);
                    Main.EntitySpriteDraw(band, pos, null, hump, bob * 0.14f, band.Size() / 2f,
                        scale, SpriteEffects.None, 0);
                }
            }

            //水面警示光泽（加色敷料 A=0）：预告期快脉冲=视觉通道，沸腾期稳定压场
            float pulse = elapsed < TelegraphFrames
                ? 0.55f + 0.45f * MathF.Sin(t * 15f + Projectile.identity)
                : 0.8f + 0.2f * MathF.Sin(t * 6f + Projectile.identity);
            float sheenA = elapsed < TelegraphFrames
                ? 0.4f * progress
                : 0.5f * Math.Max(roll, 0.15f);
            if (elapsed >= TelegraphFrames + BoilFrames) {
                sheenA = 0.35f * roll;
            }
            Color acid = BrinefumeAmbience.AcidGlow;
            Color sheen = new Color(acid.R, acid.G, acid.B, 0) * (sheenA * pulse);
            Main.EntitySpriteDraw(glow, Projectile.Center + new Vector2(0f, 2f) - Main.screenPosition,
                null, sheen, 0f, glow.Size() / 2f,
                new Vector2(widthPx / glow.Width * 1.15f, 0.5f + 0.3f * roll), SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //平息收场：几粒余沫
            for (int i = 0; i < 4; i++) {
                Dust foam = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth) * 0.6f, 0f),
                    DustID.TintableDust, new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.8f)),
                    170, BrinefumeAmbience.FoamPale, Main.rand.NextFloat(0.6f, 0.9f));
                foam.noGravity = true;
            }
        }
    }
}
