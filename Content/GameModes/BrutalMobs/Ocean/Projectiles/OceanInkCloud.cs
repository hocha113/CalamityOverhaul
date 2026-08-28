using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ocean.Projectiles
{
    /// <summary>
    /// 鱿鱼墨云（纯遮视场地，恒无伤害、无预告债）。ai[0]=半径 ai[1]=滞留帧。
    /// 可见=判定：黑暗判定圈与墨团绘制读同一 ai[0] 半径；成形完成后才施加判定，消散期停判。
    /// 黑暗减益在受击端本地滚动施加（本机 AddBuff 原生同步）。
    /// 墨体走 Extra_98 真 alpha 暗层配方（暗=真 alpha+A&gt;0 染色，加色物理上画不出暗层）
    /// </summary>
    internal class OceanInkCloud : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>成形帧数（墨团扩散到全半径）</summary>
        private const int ExpandFrames = 20;
        /// <summary>消散帧数</summary>
        private const int FadeFrames = 30;
        /// <summary>圈内黑暗减益时长（滚动施加，约 2 秒）</summary>
        private const int DarknessTicks = 120;
        /// <summary>墨团块数（绘制用，确定性排布）</summary>
        private const int BlobCount = 6;
        /// <summary>Extra_98 可见幅（像素@scale1，量测值，用于半径→缩放折算）</summary>
        private const float MaskContentPx = 47f;

        /// <summary>判定半径＝墨团覆盖半径（同一读处）</summary>
        private float Radius => Projectile.ai[0];
        private int LingerFrames => (int)Projectile.ai[1];
        private int TotalLife => ExpandFrames + LingerFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;
        /// <summary>判定窗：成形完成到消散开始</summary>
        private bool Active => Elapsed >= ExpandFrames && Elapsed < ExpandFrames + LingerFrames;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 320;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = false;//纯遮视，恒无伤害
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 210;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>纯遮视场地，永不参与伤害</summary>
        public override bool? CanDamage() => false;

        public override void AI() {
            //首帧定死时间轴（两端以同一 ai 值各自展开；timeLeft 不进同步包）
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = TotalLife;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.8f, Pitch = -0.5f, MaxInstances = 4 },
                        Projectile.Center);
                    for (int i = 0; i < 10; i++) {
                        Dust burst = Dust.NewDustPerfect(Projectile.Center, DustID.TintableDust,
                            Main.rand.NextVector2Circular(2.5f, 2.5f), 120, new Color(20, 26, 42),
                            Main.rand.NextFloat(1.2f, 1.9f));
                        burst.noGravity = true;
                    }
                }
            }

            if (Main.dedServ) {
                return;
            }

            //圈内黑暗：本机玩家判定，滚动施加（受击端本地结算，原生同步）
            if (Active) {
                Player localPlayer = Main.LocalPlayer;
                if (localPlayer.active && !localPlayer.dead
                    && localPlayer.Distance(Projectile.Center) < Radius) {
                    localPlayer.AddBuff(BuffID.Darkness, DarknessTicks);
                }
            }

            //墨雾徐动（≤1 粒/帧）
            if (Elapsed < ExpandFrames + LingerFrames && Main.rand.NextBool(3)) {
                float spread = MathHelper.Clamp(Elapsed / (float)ExpandFrames, 0f, 1f);
                Dust mist = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(Radius * spread * 0.8f, Radius * spread * 0.7f),
                    DustID.TintableDust, new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.1f, 0.4f)),
                    140, new Color(24, 30, 46), Main.rand.NextFloat(0.9f, 1.4f));
                mist.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float spread = MathHelper.Clamp(elapsed / (float)ExpandFrames, 0f, 1f);
            float alpha;
            if (elapsed >= ExpandFrames + LingerFrames) {
                alpha = MathHelper.Clamp(1f - (elapsed - ExpandFrames - LingerFrames) / (float)FadeFrames, 0f, 1f);
            }
            else {
                alpha = 0.35f + 0.65f * spread;
            }
            if (alpha <= 0.01f) {
                return false;
            }

            Texture2D blob = CWRAsset.Extra_98.Value;
            Vector2 orig = blob.Size() / 2f;
            Vector2 center = Projectile.Center - Main.screenPosition;
            float drift = Main.GlobalTimeWrappedHourly * 0.35f;

            //墨团簇：真 alpha 暗层（A>0 染色才能压暗背景），确定性排布铺满判定半径
            for (int i = 0; i < BlobCount; i++) {
                float seed = Projectile.identity * 1.3f + i * 2.39996f;
                float ang = seed + drift * (i % 2 == 0 ? 1f : -1.4f);
                float dist = Radius * (0.18f + 0.38f * (0.5f + 0.5f * MathF.Sin(seed * 3.1f))) * spread;
                Vector2 pos = center + ang.ToRotationVector2() * dist;
                float size = Radius * (0.75f + 0.30f * MathF.Sin(seed * 1.7f)) * (0.55f + 0.45f * spread);
                Color ink = new Color(15, 21, 36) * (0.80f * alpha);
                Main.EntitySpriteDraw(blob, pos, null, ink, ang * 0.4f, orig,
                    new Vector2(size * 2f / MaskContentPx, size * 1.5f / MaskContentPx), SpriteEffects.None, 0);
            }
            //中心浓芯（保证圈心不透）
            Main.EntitySpriteDraw(blob, center, null, new Color(10, 14, 26) * (0.85f * alpha), 0f, orig,
                new Vector2(Radius * 1.7f / MaskContentPx, Radius * 1.3f / MaskContentPx) * spread, SpriteEffects.None, 0);
            //边缘幽蓝辉（低强度加色，读出云界，不承担暗度）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Main.EntitySpriteDraw(glow, center, null, new Color(60, 110, 140, 0) * (0.10f * alpha),
                0f, glow.Size() / 2f, Radius * 2.2f / glow.Width * spread, SpriteEffects.None, 0);
            return false;
        }
    }
}
