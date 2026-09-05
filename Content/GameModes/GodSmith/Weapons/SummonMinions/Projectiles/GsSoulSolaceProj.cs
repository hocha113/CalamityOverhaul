using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 灵慰领域：阿比盖尔命中点留下的灵光圈（50 帧，半径 90）。本身不伤害；
    /// 圈内自家仆从 +10% 由 <see cref="MinionDoctrine.ApplyCommandBonuses"/> 在 owner 端统一查询。
    /// 真弹幕承载，队友可见
    /// </summary>
    internal class GsSoulSolaceProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.LostSoulFriendly;

        public override string LocalizationCategory => "GodSmithSummonMinionsA";

        internal const float FieldRadius = 90f;
        internal const int FieldFrames = 50;

        private ref float Life => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = FieldFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            Projectile.velocity = Vector2.Zero;
        }

        /// <summary>区域尺寸提示：原版贴图按领域半径缩放一笔（lightColor 着色，进出场渐隐）</summary>
        public override bool PreDraw(ref Color lightColor) {
            float fadeIn = MathHelper.Clamp(Life / 8f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 14f, 0f, 1f);
            float fade = fadeIn * fadeOut;
            if (fade <= 0.01f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * fade,
                0f, tex.Size() / 2f, FieldRadius * 2f / tex.Width, SpriteEffects.None, 0);
            return false;
        }
    }
}
