using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Summon.Whips
{
    internal class AzureDragonRageProjectile : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Summon + "AzureDragonRageProjectile";

        private List<Vector2> whipPoints => Projectile.GetWhipControlPoints();

        public override void SetStaticDefaults() {
            ProjectileID.Sets.IsAWhip[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.DefaultToWhip();
            Projectile.WhipSettings.Segments = 20;
            Projectile.WhipSettings.RangeMultiplier = 0.8f;
        }

        public override bool PreAI() {
            if (whipPoints.Count - 2 >= 0 && whipPoints.Count - 2 < whipPoints.Count) {
                Vector2 pos = whipPoints[whipPoints.Count - 2];
                int num = Dust.NewDust(pos, Projectile.width, Projectile.height, DustID.Blood, Projectile.direction * 2, 0f, 150, new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB), 1.3f);
                Main.dust[num].noGravity = true;
                Main.dust[num].velocity = Vector2.Zero;
            }
            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Projectile.damage -= 55;
            if (Projectile.numHits == 0) {
                target.CWR().WhipHitNum += 3;
                target.CWR().WhipHitType = (byte)WhipHitTypeEnum.AzureDragonRage;

                for (int i = 0; i < 3; i++) {
                    Projectile.NewProjectile(
                        Projectile.FromObjectGetParent(),

                        target.Center -
                        Main.player[Projectile.owner].Center.To(target.Center).UnitVector()
                        .RotatedBy(MathHelper.ToRadians(Main.rand.Next(-75, 75))) * 300,

                        Vector2.Zero,
                        ProjectileID.Leaf,
                        Projectile.damage + 500,
                        0,
                        Projectile.owner,
                        Projectile.whoAmI
                    );
                }
            }
        }

        private void DrawLine(List<Vector2> list) {
            Texture2D texture = TextureAssets.FishingLine.Value;
            Rectangle frame = texture.Frame();
            Vector2 origin = new Vector2(frame.Width / 2, 2);

            Vector2 pos = list[0];
            for (int i = 0; i < list.Count - 2; i++) {
                Vector2 element = list[i];
                Vector2 diff = list[i + 1] - element;

                float rotation = diff.ToRotation() - MathHelper.PiOver2;
                Color color = new Color(222, 10, 112);
                Vector2 scale = new Vector2(1, (diff.Length() + 2) / frame.Height);

                Main.EntitySpriteDraw(texture, pos - Main.screenPosition, frame, color, rotation, origin, scale, SpriteEffects.None, 0);

                pos += diff;
            }
        }//绘制连接线

        public override bool PreDraw(ref Color lightColor) {
            DrawLine(whipPoints);
            SpriteEffects flip = Projectile.spriteDirection < 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Texture2D texture = TextureAssets.Projectile[Type].Value;

            Vector2 pos = whipPoints[0];

            for (int i = 0; i < whipPoints.Count - 1; i++) {
                Rectangle frame = new Rectangle(0, 0, 34, 60);

                Vector2 origin = new Vector2(18, 37);
                float scale = 1;

                if (i != 0) {
                    frame.Y = 62;
                    frame.Height = 24;
                    origin = new Vector2(17, 14);
                }

                if (i == whipPoints.Count - 2) {
                    frame.Y = 88;
                    frame.Height = 24;
                    origin = new Vector2(17, 11);
                }

                Vector2 element = whipPoints[i];
                Vector2 diff = whipPoints[i + 1] - element;

                float rotation = diff.ToRotation() - MathHelper.PiOver2;
                Color color = Lighting.GetColor(element.ToTileCoordinates());

                Main.EntitySpriteDraw(texture, pos - Main.screenPosition, frame, color, rotation, origin, scale, flip, 0);
                pos += diff;
            }
            return false;
        }
    }
}
