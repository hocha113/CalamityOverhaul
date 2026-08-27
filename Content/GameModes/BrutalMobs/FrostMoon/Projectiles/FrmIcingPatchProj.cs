using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.FrostMoon.Projectiles
{
    /// <summary>
    /// 姜饼人突进滴落的糖霜减速斑（地面贴片，恒无伤害的控制区）。
    /// 生成位置即锁定（贴地）；落地后 <see cref="FadeInFrames"/> 帧无害渐显才激活，
    /// 激活期踩上滚动施加缓慢（本机玩家判定 + 本机 AddBuff，减益原生同步）。
    /// 可见区=判定区：绘制与 <see cref="InZone"/> 读同一几何
    /// </summary>
    internal class FrmIcingPatchProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SnowBallHostile;

        /// <summary>无害渐显帧（公平契约：贴片可见 ≥30 帧才有控制力）</summary>
        internal const int FadeInFrames = 30;
        /// <summary>激活存续帧</summary>
        private const int ActiveFrames = 300;
        /// <summary>消融帧</summary>
        private const int FadeOutFrames = 20;
        /// <summary>踩上施加的缓慢时长（1.5 秒，滚动续挂）</summary>
        private const int SlowTicks = 90;
        /// <summary>贴片半宽（像素），判定与绘制共用</summary>
        private const float PatchHalfWidth = 30f;
        /// <summary>判定高度（地表以上，像素）</summary>
        private const float ZoneHeightPx = 24f;
        /// <summary>糖霜凸珠数（绘制用，确定性排布）</summary>
        private const int BlobCount = 3;

        private int TotalLife => FadeInFrames + ActiveFrames + FadeOutFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;
        private bool Activated => Elapsed >= FadeInFrames && Elapsed < FadeInFrames + ActiveFrames;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 240;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 12;
            Projectile.hostile = false;//纯控制贴片，恒无伤害
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = TotalLife;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item85 with { Volume = 0.3f, Pitch = 0.4f, MaxInstances = 4 }, Projectile.Center);
                }
            }

            int elapsed = Elapsed;

            //渐显期糖粉（≤1 粒/帧）
            if (elapsed < FadeInFrames && !Main.dedServ && Main.rand.NextBool(3)) {
                float spread = elapsed / (float)FadeInFrames;
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-PatchHalfWidth, PatchHalfWidth) * spread, 0f),
                    DustID.Snow, new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.9f)), 150, default, 0.7f);
                dust.noGravity = true;
            }

            if (!Activated) {
                return;
            }

            //本机玩家判定：踩进贴片滚动施加缓慢（本机 AddBuff 原生同步）
            if (!Main.dedServ) {
                Player localPlayer = Main.LocalPlayer;
                if (localPlayer.active && !localPlayer.dead && InZone(localPlayer.Hitbox)) {
                    localPlayer.AddBuff(BuffID.Slow, SlowTicks);
                }
            }

            //激活期黏亮微光（≤1 粒/帧）
            if (!Main.dedServ && Main.rand.NextBool(9)) {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-PatchHalfWidth, PatchHalfWidth), -2f),
                    DustID.Frost, new Vector2(0f, -0.25f), 160, default, 0.7f);
                dust.noGravity = true;
            }
        }

        /// <summary>判定盒与绘制共用同一几何（可见区=判定区）</summary>
        private bool InZone(Rectangle hitbox) {
            Rectangle zone = new((int)(Projectile.Center.X - PatchHalfWidth), (int)(Projectile.Center.Y - ZoneHeightPx),
                (int)(PatchHalfWidth * 2f), (int)(ZoneHeightPx + 8f));
            return zone.Intersects(hitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            //渐显期从中心向两端摊开，激活满幅，消融整体退淡
            float spread = elapsed < FadeInFrames ? elapsed / (float)FadeInFrames : 1f;
            float alpha;
            if (elapsed >= FadeInFrames + ActiveFrames) {
                alpha = MathHelper.Clamp(1f - (elapsed - FadeInFrames - ActiveFrames) / (float)FadeOutFrames, 0f, 1f);
            }
            else if (elapsed < FadeInFrames) {
                alpha = 0.35f + 0.35f * spread;
            }
            else {
                alpha = 1f;
            }
            if (alpha <= 0.01f) {
                return false;
            }

            //糖霜主体（真 alpha 实体层，宽度与判定同一 PatchHalfWidth）
            Texture2D sheet = CWRAsset.Extra_98.Value;
            float widthPx = PatchHalfWidth * 2f * spread;
            Vector2 sheetScale = new Vector2(widthPx / sheet.Width, 10f / sheet.Height);
            Color icing = new Color(240, 244, 250) * (0.78f * alpha);
            Main.EntitySpriteDraw(sheet, Projectile.Center - new Vector2(0f, 3f) - Main.screenPosition,
                null, icing, 0f, sheet.Size() / 2f, sheetScale, SpriteEffects.None, 0);

            //糖霜凸珠（原版雪球贴图小珠，确定性排布，实体感锚点）
            Main.instance.LoadProjectile(ProjectileID.SnowBallHostile);
            Texture2D blob = TextureAssets.Projectile[ProjectileID.SnowBallHostile].Value;
            int frames = Main.projFrames[ProjectileID.SnowBallHostile] > 0 ? Main.projFrames[ProjectileID.SnowBallHostile] : 1;
            Rectangle rect = blob.Frame(1, frames, 0, 0);
            for (int i = 0; i < BlobCount; i++) {
                float offsetX = (i - (BlobCount - 1) * 0.5f) * (PatchHalfWidth * 0.8f);
                if (Math.Abs(offsetX) > PatchHalfWidth * spread) {
                    continue;
                }
                float wobble = MathF.Sin(Projectile.identity * 1.9f + i * 2.6f);
                Vector2 pos = Projectile.Center + new Vector2(offsetX + wobble * 4f, -3f) - Main.screenPosition;
                Color pearl = Color.Lerp(lightColor, new Color(246, 250, 255), 0.65f) * (0.85f * alpha);
                Main.EntitySpriteDraw(blob, pos, rect, pearl, wobble * 0.4f,
                    rect.Size() / 2f, 0.42f + 0.08f * (i % 2), SpriteEffects.None, 0);
            }

            //激活期糖亮泽（加色敷料）；渐显期刻意保持哑光以示无害
            if (Activated) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                float shimmer = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 7f + Projectile.identity);
                Color sheen = new Color(210, 235, 255, 0) * (0.28f * alpha * shimmer);
                Main.EntitySpriteDraw(glow, Projectile.Center - new Vector2(0f, 5f) - Main.screenPosition,
                    null, sheen, 0f, glow.Size() / 2f, new Vector2(widthPx / glow.Width * 1.05f, 0.3f), SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-PatchHalfWidth, PatchHalfWidth), 0f),
                    DustID.Snow, new Vector2(0f, -Main.rand.NextFloat(0.4f, 1.2f)), 130, default, 0.8f);
                dust.noGravity = true;
            }
        }
    }
}
