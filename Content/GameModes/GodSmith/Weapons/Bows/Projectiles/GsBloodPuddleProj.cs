using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Bows.Projectiles
{
    /// <summary>
    /// 血雨弓 T3 血泊：贴地滞留 2 秒的地面域。踩踏跳伤（本地免疫 30 帧一跳）+ 对非 boss 轻减速。
    /// 判定宽度与可见体同源（hitbox 即绘制宽）；绘制只留原版血弹贴图按判定框拉伸的一笔
    /// </summary>
    internal class GsBloodPuddleProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BloodShot;

        //不注册新键，显示名指向原版物品键
        public override LocalizedText DisplayName => Language.GetText("ItemName.BloodRainBow");

        private const int LifeFrames = 120;

        public override void SetDefaults() {
            Projectile.width = 110;
            Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //域内非 boss 轻减速（NPC 由服务端权威模拟，客户端同跑无害）
            Rectangle zone = Projectile.Hitbox;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.boss || npc.dontTakeDamage) {
                    continue;
                }
                if (zone.Intersects(npc.Hitbox)) {
                    npc.velocity.X *= 0.88f;
                }
            }
        }

        /// <summary>范围提示：原版血弹贴图按判定框拉伸一笔（lightColor 着色），前 12 帧铺开、后 20 帧收干</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float grow = MathHelper.Clamp((LifeFrames - Projectile.timeLeft) / 12f, 0f, 1f);
            float fade = MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f);
            Vector2 scale = new(Projectile.width / (float)tex.Width * grow, Projectile.height / (float)tex.Height);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * fade, 0f,
                tex.Size() / 2f, scale, SpriteEffects.None);
            return false;
        }
    }
}
