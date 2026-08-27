using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Temple.Projectiles
{
    /// <summary>
    /// 庙火余烬：蜥蜴系倒下时在尸位留下的短命火斑（阵地控制）。共 90 帧：
    /// 前 34 帧无害渐显、中 46 帧灼热判定（伤害窗=可见亮窗）、末 10 帧无害熄灭，
    /// 判定由各端从 timeLeft 确定性推得，不改写同步伤害字段。
    /// 弹体走真 alpha 暗橙外壳+加色亮芯配方（镜像 VileLanceProj.DrawGlob）；静止无位移，无拖尾项
    /// </summary>
    internal class TempleEmberProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>无害渐显帧（公平契约 ≥30）</summary>
        internal const int FadeInFrames = 34;
        /// <summary>灼热判定帧</summary>
        internal const int ActiveFrames = 46;
        /// <summary>无害熄灭帧</summary>
        internal const int FadeOutFrames = 10;
        /// <summary>火斑总驻留帧（=渐显+判定+熄灭）</summary>
        internal const int TotalFrames = FadeInFrames + ActiveFrames + FadeOutFrames;

        /// <summary>外壳暗橙（真 alpha 暗层）与亮芯（加色，A=0）</summary>
        private static readonly Color DarkShell = new Color(104, 42, 12);
        private static readonly Color BrightCore = new Color(255, 170, 64);

        private int Age => TotalFrames - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.alpha = 255;
            Projectile.netImportant = true;
        }

        /// <summary>伤害窗=可见亮窗：渐显与熄灭段一律无害（各端由 timeLeft 确定性判定）</summary>
        public override bool? CanDamage()
            => Age > FadeInFrames && Age <= FadeInFrames + ActiveFrames ? null : false;

        public override void AI() {
            int age = Age;

            //可见度与判定同一时间轴：渐显抬亮、灼热期微闪、熄灭压暗
            if (age <= FadeInFrames) {
                Projectile.alpha = (int)MathHelper.Lerp(255f, 30f, age / (float)FadeInFrames);
            }
            else if (age <= FadeInFrames + ActiveFrames) {
                Projectile.alpha = 10 + (int)(26f * (0.5f + 0.5f * MathF.Sin(age * 0.35f)));
            }
            else {
                float t = (age - FadeInFrames - ActiveFrames) / (float)FadeOutFrames;
                Projectile.alpha = (int)MathHelper.Lerp(40f, 255f, t);
            }

            //点燃瞬间各端本地报火声（判定窗开启的听觉沿）
            if (age == FadeInFrames + 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.3f, Pitch = 0.2f, MaxInstances = 4 }, Projectile.Center);
            }

            if (!VaultUtils.isServer) {
                if (Main.rand.NextBool(5)) {
                    Dust spark = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-8f, 8f), 4f),
                        DustID.Torch, new Vector2(0f, -Main.rand.NextFloat(0.6f, 1.6f)), 120, default, 1f);
                    spark.noGravity = true;
                }
                if (age > FadeInFrames && Main.rand.NextBool(8)) {
                    Dust smoke = Dust.NewDustPerfect(Projectile.Top, DustID.Smoke,
                        new Vector2(0f, -Main.rand.NextFloat(0.4f, 0.9f)), 150, default, 0.8f);
                    smoke.noGravity = true;
                }
            }
            float glow = 1f - Projectile.alpha / 255f;
            Lighting.AddLight(Projectile.Center, 0.5f * glow, 0.28f * glow, 0.08f * glow);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                Dust smoke = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke,
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.5f, 1.4f)), 150, default, 1f);
                smoke.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98.Value;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float opacity = 1f - Projectile.alpha / 255f;
            if (opacity <= 0.01f) {
                return false;
            }

            //火苗呼吸：横缩纵伸的摇曳（静止火斑，形变代拖尾表达「活着的火」）
            float flick = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 13f + Projectile.identity * 1.7f);
            Vector2 scale = new Vector2(0.30f * (2f - flick), 0.42f * flick);

            //底部暗斑：真 alpha 灰烬垫底，火斑坐在地上而非浮空贴片
            Main.EntitySpriteDraw(tex, pos + new Vector2(0f, 10f), null,
                new Color(36, 20, 10, 220) * (0.7f * opacity), 0f, origin, new Vector2(0.4f, 0.12f), SpriteEffects.None, 0);

            //两层配方（镜像 VileLanceProj.DrawGlob）：暗壳全 alpha ×1.18 打底遮挡，亮芯 A=0 内嵌
            Color dark = DarkShell * (0.92f * opacity);
            Color core = (BrightCore with { A = 0 }) * (0.85f * opacity * flick);
            Main.EntitySpriteDraw(tex, pos, null, dark, 0f, origin, scale * 1.18f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, core, 0f, origin, scale * 0.78f, SpriteEffects.None, 0);
            return false;
        }
    }
}
