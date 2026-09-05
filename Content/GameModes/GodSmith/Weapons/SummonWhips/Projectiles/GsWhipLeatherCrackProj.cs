using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Projectiles
{
    /// <summary>
    /// 皮鞭处决「皮革响鞭冲击」：单段全额爆（2.0x 鞭面板由生成方折算进 damage），
    /// 短暂聚拢后响鞭爆。owner 生成真弹幕，全端可闻
    /// </summary>
    internal class GsWhipLeatherCrackProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bubble;

        private const int GatherFrames = 3;   //聚拢
        private const int CrackFrames = 5;    //爆窗
        private const int LifeFrames = 24;

        private int Elapsed => LifeFrames - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = 120;
            Projectile.height = 120;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanDamage() => Elapsed >= GatherFrames && Elapsed < GatherFrames + CrackFrames ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Intersects(Utils.CenteredRectangle(Projectile.Center, new Vector2(120f)));

        public override void AI() {
            if (Elapsed == GatherFrames && !VaultUtils.isServer) {
                //响鞭爆帧：全端主音
                SoundEngine.PlaySound(SoundID.Item153 with { Volume = 1f, Pitch = 0.05f }, Projectile.Center);
            }
        }

        /// <summary>范围提示：原版气泡贴图按判定框缩放画一笔（lightColor 着色），爆后随余帧渐隐</summary>
        public override bool PreDraw(ref Color lightColor) {
            if (Elapsed < GatherFrames) {
                return false;
            }
            float fade = 1f - MathHelper.Clamp((Elapsed - GatherFrames) / (float)(LifeFrames - GatherFrames), 0f, 1f);
            if (fade <= 0.01f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * fade, 0f,
                tex.Size() * 0.5f, Projectile.width / (float)tex.Width, SpriteEffects.None, 0);
            return false;
        }
    }
}
