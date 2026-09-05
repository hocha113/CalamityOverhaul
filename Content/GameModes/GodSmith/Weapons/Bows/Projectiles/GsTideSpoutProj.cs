using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Bows.Projectiles
{
    /// <summary>
    /// 海啸「拍浪」处决水柱：自标记敌脚下拔起的一柱潮涌。
    /// 生成端把 Center 定在敌脚上方约 55px，判定窗 5~18 帧每目标一次。
    /// 绘制只留原版水弹贴图按判定框拉伸的一笔
    /// </summary>
    internal class GsTideSpoutProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WaterBolt;

        private ref float Life => ref Projectile.localAI[0];

        private const int TotalLife = 34;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 112;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanDamage() => Life >= 5f && Life <= 18f ? null : false;

        public override void AI() {
            if (Life == 0f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Splash with { Volume = 1f, Pitch = -0.2f }, Projectile.Center);
            }
            Life++;
        }

        /// <summary>范围提示：原版水弹贴图按判定框拉伸一笔（lightColor 着色），尾 10 帧渐隐</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float fade = MathHelper.Clamp((TotalLife - Life) / 10f, 0f, 1f);
            Vector2 scale = new(Projectile.width / (float)tex.Width, Projectile.height / (float)tex.Height);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * fade, 0f,
                tex.Size() * 0.5f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
