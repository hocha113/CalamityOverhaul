using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles
{
    /// <summary>
    /// 灾变族左键 rider 共用的参数化微爆（P13 返工新增）：各武器签名机制触发的
    /// 跨端可见小型 AoE。ai[0] = 判定半径 px，ai[1] = 主题索引（定出生音），ai[2] = 主题参数
    /// （星籁=音阶步进定音高）。前 3 帧为判定窗（每目标至多一次），其后只留范围提示
    /// </summary>
    internal class GsCataclysmRiderBurstProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bubble;

        public override string LocalizationCategory => "GodSmithMagicCataclysm";

        //==================== 主题表 ====================

        /// <summary>星籁和弦爆</summary>
        public const int ThemeStar = 0;
        /// <summary>龙焰孽火爆</summary>
        public const int ThemeEmber = 1;
        /// <summary>星云微新星</summary>
        public const int ThemeNova = 2;
        /// <summary>谐振驻波脉冲</summary>
        public const int ThemeNode = 3;
        /// <summary>白灾霜爆</summary>
        public const int ThemeFrost = 4;

        private ref float Radius => ref Projectile.ai[0];

        private ref float Theme => ref Projectile.ai[1];

        private ref float ThemeParam => ref Projectile.ai[2];

        private ref float Life => ref Projectile.localAI[0];

        private const int TotalLife = 24;

        private int ThemeIndex() => (int)MathHelper.Clamp(Theme, 0f, ThemeFrost);

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
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

        /// <summary>出生帧主题音（客户端）</summary>
        private void PlayThemeSound() {
            SoundStyle sound = ThemeIndex() switch {
                //星籁：音高随和弦步进爬升（ThemeParam = 音阶步）
                ThemeStar => SoundID.Item26 with { Volume = 0.6f, Pitch = -0.1f + 0.08f * ThemeParam },
                ThemeEmber => SoundID.Item74 with { Volume = 0.5f, Pitch = 0.1f },
                ThemeNova => SoundID.Item103 with { Volume = 0.45f, Pitch = 0.3f },
                ThemeNode => SoundID.Item25 with { Volume = 0.55f, Pitch = 0.45f },
                _ => SoundID.Item27 with { Volume = 0.6f },
            };
            SoundEngine.PlaySound(sound, Projectile.Center);
        }

        /// <summary>范围提示：原版贴图按判定半径缩放画一笔（lightColor 着色），随寿命渐隐</summary>
        public override bool PreDraw(ref Color lightColor) {
            float fade = 1f - MathHelper.Clamp(Life / TotalLife, 0f, 1f);
            if (fade <= 0.01f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float scale = MathHelper.Max(Radius, 8f) * 2f / tex.Width;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * fade, 0f,
                tex.Size() * 0.5f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
