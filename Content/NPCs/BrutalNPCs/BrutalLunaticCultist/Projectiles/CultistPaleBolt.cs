using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 苍白弹：假身的无火之弹，vanilla 468 苍白火球做体，没有元素色，识破谎言的动态线索<br/>
    /// ai[0]=1 幻象模式：半透明、无光、永不咬人。真弹遮挡发光,假弹透背景,材质法则即识真线索
    /// </summary>
    internal class CultistPaleBolt : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.CultistBossFireBallClone;

        private bool IsPhantom => Projectile.ai[0] == 1f;

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = 4;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 270;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (++Projectile.frameCounter >= 5) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
            }

            //苍白弱光，比真弹暗，光强也是线索;幻象完全无光
            if (!IsPhantom) {
                Lighting.AddLight(Projectile.Center, CultistMotion.PaleClone.ToVector3() * 0.18f);
            }
        }

        /// <summary>幻象永不咬人</summary>
        public override bool CanHitPlayer(Player target) => !IsPhantom;

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer || !CultistMotion.OnScreen(Projectile.Center, 200f)) {
                return;
            }
            CultistMotion.RuneBurst(Projectile.Center, CultistMotion.PaleClone, 3, 3.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.CultistBossFireBallClone);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.CultistBossFireBallClone].Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            int frameHeight = tex.Height / Main.projFrames[Type];
            Rectangle frame = new(0, frameHeight * Projectile.frame, tex.Width, frameHeight);
            Vector2 pos = Projectile.Center - Main.screenPosition;

            if (IsPhantom) {
                //幻象:半透明无晕,背景透得过去(识真线索=遮挡)
                Main.EntitySpriteDraw(tex, pos, frame, Color.White * 0.42f, Projectile.rotation,
                    frame.Size() * 0.5f, Projectile.scale, SpriteEffects.None, 0);
                return false;
            }
            //微弱苍晕
            Main.EntitySpriteDraw(glow, pos, null, CultistMotion.PaleClone with { A = 0 } * 0.3f,
                0f, glow.Size() * 0.5f, Projectile.scale * 0.5f, SpriteEffects.None, 0);
            //vanilla 苍白火球体
            Main.EntitySpriteDraw(tex, pos, frame, Color.White, Projectile.rotation,
                frame.Size() * 0.5f, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
