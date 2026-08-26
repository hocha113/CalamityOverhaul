using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.DuneStorm.Projectiles
{
    /// <summary>
    /// 「风堑」阵风波（无伤害的位移场地实体）。ai[0]=风向(±1) ai[1]=绑定档位。
    /// 世界锚定：预告 60 帧（地面沙线加速流动 + 风啸渐强的双通道预告）
    /// → 推挤 26 帧（对暴露在天空下的本机玩家施加温和水平推力，空中减半，只推 X 不碰 Y）
    /// → 余韵 36 帧（沙尘缓散）。有遮蔽即免疫，Boss 在场时推力静默跳过。
    /// 各端从 timeLeft 确定性推相位，推力只由各端施加给自己的玩家（玩家位移端权威）
    /// </summary>
    internal class DuneStormGustWaveProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SandBallFalling;

        /// <summary>预告帧数（公平契约 ≥45）</summary>
        private const int TelegraphFrames = 60;
        /// <summary>推挤窗口帧数</summary>
        private const int PushFrames = 26;
        private const int AfterFrames = 36;
        /// <summary>作用区半宽/半高（像素，世界锚定）</summary>
        private const float AreaHalfWidth = 900f;
        private const float AreaHalfHeight = 520f;

        private float Dir => Projectile.ai[0] >= 0f ? 1f : -1f;
        private int Tier => Math.Clamp((int)Projectile.ai[1], 1, 3);
        private int TotalLife => TelegraphFrames + PushFrames + AfterFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        //推挤窗开启帧缓存的本机门（城镇安宁只在窗口起点判一次，服务端不消费）
        private bool localGateCached;
        private bool localTownCalm;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1000;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;//纯位移场，无伤害
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphFrames + PushFrames + AfterFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int elapsed = Elapsed;

            //向氛围层上报涌起量：预告渐强构成听觉预告，推挤拉满，余韵回落
            if (!Main.dedServ) {
                float swell;
                if (elapsed < TelegraphFrames) {
                    swell = 0.8f * elapsed / TelegraphFrames;
                }
                else if (elapsed < TelegraphFrames + PushFrames) {
                    swell = 1f;
                }
                else {
                    swell = MathHelper.Clamp(1f - (elapsed - TelegraphFrames - PushFrames) / (float)AfterFrames, 0f, 1f);
                }
                //远离波心的观察者少听一点
                float near = 1f - MathHelper.Clamp(
                    (Projectile.Distance(Main.LocalPlayer.Center) - AreaHalfWidth) / 900f, 0f, 1f);
                DuneStormAmbience.ReportGustSwell(swell * near);
            }

            if (elapsed == TelegraphFrames) {
                //落地拍：厚重的过顶风声
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.9f, Pitch = -0.45f, MaxInstances = 3 }, Projectile.Center);
                    localTownCalm = DuneStorm.TownCalm(Main.LocalPlayer.Center);
                    localGateCached = true;
                }
            }

            if (elapsed >= TelegraphFrames && elapsed < TelegraphFrames + PushFrames) {
                PushLocalPlayer();
            }

            if (Main.dedServ) {
                return;
            }
            //屏外剔除：远离观察者的波不喷粒子（波跑满逻辑，画面只给近处的人）
            if (Projectile.Distance(Main.LocalPlayer.Center) > 2600f) {
                return;
            }
            if (elapsed < TelegraphFrames) {
                TelegraphDust(elapsed / (float)TelegraphFrames);
            }
            else if (elapsed < TelegraphFrames + PushFrames) {
                CurtainDust();
            }
            else if (Main.rand.NextBool(2)) {
                SettleDust();
            }
        }

        /// <summary>推挤只由本机端施加给自己的玩家（服务端 myPlayer=255 天然无操作）</summary>
        private void PushLocalPlayer() {
            if (Main.dedServ) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player.dead || player.ghost) {
                return;
            }
            if (CWRWorld.HasBoss) {
                return;//Boss 在场位移机制暂停
            }
            if (localGateCached && localTownCalm) {
                return;//城镇安宁
            }
            if (!DuneStorm.InSurfaceDesert(player) || !InArea(player.Center)) {
                return;
            }
            if (!DuneStorm.ExposedToSky(player)) {
                return;//有遮蔽即免疫（躲进屋檐是有效反制）
            }

            float dir = Dir;
            float accel = DuneStorm.GustAccelByTier[Tier - 1];
            if (player.velocity.Y != 0f) {
                accel *= 0.5f;//空中减半
            }
            //只推 X 且封顶携带速度：温和推挤，不产生坠落死亡级位移
            if (player.velocity.X * dir < DuneStorm.GustCarryCapByTier[Tier - 1]) {
                player.velocity.X += dir * accel;
            }
        }

        private bool InArea(Vector2 pos)
            => Math.Abs(pos.X - Projectile.Center.X) < AreaHalfWidth
            && Math.Abs(pos.Y - Projectile.Center.Y) < AreaHalfHeight;

        //预告期：地面沙线加速流动（2 粒/帧，速度随进度爬升）
        private void TelegraphDust(float progress) {
            for (int i = 0; i < 2; i++) {
                float worldX = Projectile.Center.X + Main.rand.NextFloat(-AreaHalfWidth, AreaHalfWidth);
                int tileX = (int)(worldX / 16f);
                int startY = (int)(Projectile.Center.Y / 16f) - 10;
                if (!DuneStorm.TryFindGround(tileX, startY, out Vector2 ground)) {
                    continue;
                }
                Dust dust = Dust.NewDustPerfect(ground + new Vector2(0f, -Main.rand.NextFloat(2f, 12f)),
                    DustID.Sand, new Vector2(Dir * (2f + 9f * progress) * Main.rand.NextFloat(0.8f, 1.2f), -0.2f),
                    Main.rand.Next(90, 130), default, Main.rand.NextFloat(0.9f, 1.4f));
                dust.noGravity = true;
                dust.fadeIn = 0.3f;
            }
        }

        //推挤窗口：横扫的沙幕（3 粒/帧，离地更高、更快）
        private void CurtainDust() {
            for (int i = 0; i < 3; i++) {
                Vector2 pos = Projectile.Center + new Vector2(
                    Main.rand.NextFloat(-AreaHalfWidth, AreaHalfWidth),
                    Main.rand.NextFloat(-AreaHalfHeight * 0.5f, AreaHalfHeight * 0.4f));
                Dust dust = Dust.NewDustPerfect(pos, DustID.Sand,
                    new Vector2(Dir * Main.rand.NextFloat(9f, 14f), Main.rand.NextFloat(-0.6f, 0.6f)),
                    Main.rand.Next(80, 120), default, Main.rand.NextFloat(1f, 1.5f));
                dust.noGravity = true;
            }
        }

        //余韵：沙尘缓散坠地
        private void SettleDust() {
            Vector2 pos = Projectile.Center + new Vector2(
                Main.rand.NextFloat(-AreaHalfWidth, AreaHalfWidth),
                Main.rand.NextFloat(-AreaHalfHeight * 0.4f, AreaHalfHeight * 0.3f));
            Dust dust = Dust.NewDustPerfect(pos, DustID.Sand,
                new Vector2(Dir * Main.rand.NextFloat(0.8f, 2f), Main.rand.NextFloat(0.5f, 1.5f)),
                Main.rand.Next(110, 150), default, Main.rand.NextFloat(0.8f, 1.2f));
            dust.noGravity = false;
        }

        /// <summary>波心地表的暖光低带：预告期渐亮、推挤期拉满、余韵退淡（方向感交给沙线本身）</summary>
        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float strength;
            if (elapsed < TelegraphFrames) {
                strength = 0.35f * elapsed / TelegraphFrames;
            }
            else if (elapsed < TelegraphFrames + PushFrames) {
                strength = 0.5f;
            }
            else {
                strength = 0.5f * MathHelper.Clamp(
                    1f - (elapsed - TelegraphFrames - PushFrames) / (float)AfterFrames, 0f, 1f);
            }
            if (strength <= 0.02f) {
                return false;
            }
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.identity);
            Color warm = new Color(DuneStorm.WarnGlow.R, DuneStorm.WarnGlow.G, DuneStorm.WarnGlow.B, 0)
                * (strength * pulse * 0.55f);
            //横贯作用区的低平光带，暗示风道范围
            Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null, warm, 0f,
                glow.Size() / 2f, new Vector2(AreaHalfWidth / 26f, 1.1f), SpriteEffects.None, 0);
            return false;
        }
    }
}
