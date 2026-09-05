using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Bows.Projectiles
{
    /// <summary>
    /// 家族通用参数化 AoE 爆：处决 rider 与落地溅射共用的跨端可见实体。
    /// ai[0] = 判定半径 px，ai[1] = 主题索引（音效查表）。
    /// 前 3 帧为判定窗（每目标至多一次），其后只留范围提示
    /// </summary>
    internal class GsVolleyBurstProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bubble;

        //==================== 主题表 ====================

        public const int ThemeGold = 0;
        public const int ThemeFrost = 1;
        public const int ThemeShadow = 2;
        public const int ThemeSpore = 3;
        public const int ThemeVolt = 4;
        public const int ThemeTide = 5;
        public const int ThemeHoly = 6;
        public const int ThemeEmber = 7;
        public const int ThemePearl = 8;

        private ref float Radius => ref Projectile.ai[0];

        private ref float Theme => ref Projectile.ai[1];

        private ref float Life => ref Projectile.localAI[0];

        private const int TotalLife = 26;

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanDamage() => Life <= 3f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float r = MathHelper.Max(Radius, 8f);
            Vector2 closest = targetHitbox.ClosestPointInRect(Projectile.Center);
            return closest.DistanceSQ(Projectile.Center) <= r * r;
        }

        public override void AI() {
            if (Life == 0f && !VaultUtils.isServer) {
                PlayThemeSound();
            }
            Life++;
        }

        private int ThemeIndex() => (int)MathHelper.Clamp(Theme, 0f, ThemePearl);

        /// <summary>出生帧主题音效（客户端）</summary>
        private void PlayThemeSound() {
            int t = ThemeIndex();
            SoundStyle sound = t switch {
                ThemeFrost => SoundID.Item27 with { Volume = 0.7f },
                ThemeShadow => SoundID.Item103 with { Volume = 0.6f },
                ThemeSpore => SoundID.Item97 with { Volume = 0.6f },
                ThemeVolt => SoundID.Item94 with { Volume = 0.55f },
                ThemeTide => SoundID.Splash with { Volume = 0.8f },
                ThemeHoly => SoundID.Item29 with { Volume = 0.7f },
                ThemeEmber => SoundID.Item74 with { Volume = 0.5f, Pitch = 0.1f },
                ThemePearl => SoundID.Item29 with { Volume = 0.5f, Pitch = 0.55f },
                _ => SoundID.Item62 with { Volume = 0.55f, Pitch = 0.3f },
            };
            SoundEngine.PlaySound(sound, Projectile.Center);
        }

        /// <summary>范围提示：原版气泡贴图按判定半径缩放画一笔（lightColor 着色），随寿命渐隐</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float fade = 1f - MathHelper.Clamp(Life / TotalLife, 0f, 1f);
            float scale = MathHelper.Max(Radius, 8f) * 2f / tex.Width;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * fade, 0f,
                tex.Size() * 0.5f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
