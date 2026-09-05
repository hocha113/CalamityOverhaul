using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Throwing.Projectiles
{
    /// <summary>
    /// 回收体:投掷物未命中而亡时留在世上的「可捡回的那一件」。真弹幕,远端可见。<br/>
    /// ai[0]=返还物品 ID;ai[1]=1 强制磁吸(回收体超员时被挤出的最旧一颗)。<br/>
    /// 300px 内磁吸向主人,12s 超时;触碰返还只在 owner 端结算(客户端权威背包)
    /// </summary>
    internal class GsRecoveryPickup : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private int ItemType => (int)Projectile.ai[0];
        private bool ForcePull => Projectile.ai[1] == 1f;

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 720;
            Projectile.netImportant = true;
        }

        public override bool? CanHitNPC(NPC target) => false;

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            bool pulling = false;
            if (owner.active && !owner.dead) {
                float dist = Projectile.Distance(owner.Center);
                if (ForcePull || dist <= 300f) {
                    //磁吸相:越近越快,穿墙回手
                    pulling = true;
                    float speed = MathHelper.Lerp(6f, 13f, 1f - MathHelper.Clamp(dist / 300f, 0f, 1f));
                    Vector2 want = (owner.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * speed;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.16f);
                    Projectile.tileCollide = false;
                    Projectile.rotation += 0.22f;
                }
            }
            if (!pulling) {
                //落地体:重力,触地立住
                Projectile.velocity.Y += 0.22f;
                if (Projectile.velocity.Y > 10f) {
                    Projectile.velocity.Y = 10f;
                }
                Projectile.velocity.X *= 0.985f;
                Projectile.rotation += Projectile.velocity.X * 0.04f;
                Projectile.tileCollide = true;
            }
            //触碰返还:owner 权威,写自己背包
            if (Projectile.owner == Main.myPlayer && owner.active && !owner.dead
                && Projectile.Hitbox.Intersects(owner.Hitbox)) {
                owner.GiveItem(Projectile.GetSource_FromThis(), ItemType, 1);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.7f }, Projectile.Center);
                }
                Projectile.Kill();
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //落地不消亡,立住等待磁吸
            Projectile.velocity = Vector2.Zero;
            return false;
        }

        /// <summary>本体一笔:画返还物品的原版贴图(弹幕自身无贴图,此为必要绘制);将逝 2s 闪烁提示</summary>
        public override bool PreDraw(ref Color lightColor) {
            int itemType = ItemType;
            if (itemType <= 0) {
                return false;
            }
            if (Projectile.timeLeft < 120 && Projectile.timeLeft / 6 % 2 == 0) {
                return false;
            }
            Main.instance.LoadItem(itemType);
            Texture2D tex = TextureAssets.Item[itemType].Value;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation, tex.Size() / 2f, 1f, SpriteEffects.None, 0);
            return false;
        }
    }
}
