using CalamityOverhaul.Content.Items.Tools;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Tools
{
    internal class SpanDMBall : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item + "Tools/DarkMatter";
        internal DarkMatterBall darkMatterBall;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.timeLeft = 130;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.hostile = false;
        }

        public override void NetHeldSend(BinaryWriter writer) {
            if (darkMatterBall != null && darkMatterBall.Type > ItemID.None) {
                ItemIO.Send(darkMatterBall.Item, writer, true);
            }
            else {
                ItemIO.Send(new Item(), writer, true);
            }
        }

        public override void NetHeldReceive(BinaryReader reader) {
            var item = ItemIO.Receive(reader, true);
            if (item.Alives() && item.ModItem != null && item.ModItem is DarkMatterBall _darkMatterBall) {
                darkMatterBall = _darkMatterBall;
            }
        }

        public override void AI() {
            if (Projectile.ai[0] > 60) {
                Projectile.ChasingBehavior(Owner.Center, 13);
                Projectile.Center = Vector2.Lerp(Projectile.Center, Owner.Center, 0.04f);
                if (Projectile.Distance(Owner.Center) < Projectile.width) {
                    Projectile.Kill();
                }
                Projectile.scale -= 0.02f;
            }
            else {
                Projectile.rotation += 0.1f;
                Projectile.scale += 0.02f;
                Projectile.alpha += 5;
                if (Projectile.alpha > 255) {
                    Projectile.alpha = 255;
                }
            }
            Projectile.ai[0]++;
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.QuickSpawnItem(Owner.FromObjectGetParent(), darkMatterBall.Item, 1);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float alp = Projectile.alpha / 255f;

            if (Projectile.ai[1] != 1) {
                Vector2 drawPos = Projectile.Center - Main.screenPosition;
                Color drawColor = Color.White * alp * 0.02f;
                Vector2 drawOrig = DarkMatterBall.DarkMatter.Size() / 2;
                float slp = (255 - Projectile.alpha) / 15f;
                for (int i = 0; i < 113; i++) {
                    Main.EntitySpriteDraw(DarkMatterBall.DarkMatter.Value, drawPos, null, drawColor
                    , Projectile.rotation + (MathHelper.TwoPi / 113 * i), drawOrig, slp, SpriteEffects.None, 0);
                }
            }

            Main.EntitySpriteDraw(DarkMatterBall.DarkMatter.Value, Projectile.Center - Main.screenPosition, null, Color.White * alp
                , Projectile.rotation, DarkMatterBall.DarkMatter.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
