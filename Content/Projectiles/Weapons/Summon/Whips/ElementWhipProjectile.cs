using CalamityMod.Buffs.DamageOverTime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Summon.Whips
{
    internal class ElementWhipProjectile : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Summon + "ElementWhipProjectile";

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
                int num = Dust.NewDust(pos, Projectile.width, Projectile.height, DustID.RainbowTorch, Projectile.direction * 2, 0f, 150, new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB), 1.3f);
                Main.dust[num].noGravity = true;
                Main.dust[num].velocity = Vector2.Zero;
            }
            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => target.AddBuff(ModContent.BuffType<ElementalMix>(), 160);

        public override void OnHitPlayer(Player target, Player.HurtInfo info) => target.AddBuff(ModContent.BuffType<ElementalMix>(), 60);

        private void DrawLine(List<Vector2> list) {
            Texture2D texture = TextureAssets.FishingLine.Value;
            Rectangle frame = texture.Frame();
            Vector2 origin = new Vector2(frame.Width / 2, 2);

            Vector2 pos = list[0];
            for (int i = 0; i < list.Count - 2; i++) {
                Vector2 element = list[i];
                Vector2 diff = list[i + 1] - element;

                float rotation = diff.ToRotation() - MathHelper.PiOver2;
                Color color = new Color(22, 102, 202);
                Vector2 scale = new Vector2(1, (diff.Length() + 2) / frame.Height);

                Main.EntitySpriteDraw(texture, pos - Main.screenPosition, frame, color, rotation, origin, scale, SpriteEffects.None, 0);

                pos += diff;
            }
        }//绘制连接线

        public override bool PreDraw(ref Color lightColor) {
            DrawLine(whipPoints);
            SpriteEffects flip = Projectile.spriteDirection < 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Texture2D texture = CWRUtils.GetT2DValue(Texture);
            Texture2D _men = CWRUtils.GetT2DValue(Texture + "Glow");

            Vector2 pos = whipPoints[0];

            for (int i = 0; i < whipPoints.Count - 1; i++) {
                Rectangle frame = new Rectangle(0, 0, 30, 42);

                Vector2 origin = new Vector2(15, 20);
                float scale = 1 + i / 30f;

                int count = i % 4;

                switch (count) {
                    case 0:
                        frame.Y = 44;
                        frame.Height = 14;
                        origin = new Vector2(15, 7);
                        break;
                    case 1:
                        frame.Y = 60;
                        frame.Height = 14;
                        origin = new Vector2(15, 7);
                        break;
                    case 2:
                        frame.Y = 76;
                        frame.Height = 14;
                        origin = new Vector2(15, 7);
                        break;
                    case 3:
                        frame.Y = 92;
                        frame.Height = 14;
                        origin = new Vector2(15, 7);
                        break;
                }

                if (i == whipPoints.Count - 2) {
                    frame.Y = 108;
                    frame.Height = 20;
                    origin = new Vector2(15, 10);
                }

                if (i == 0) {
                    frame = new Rectangle(0, 0, 30, 42);
                    origin = new Vector2(15, 20);
                }

                Vector2 element = whipPoints[i];
                Vector2 diff = whipPoints[i + 1] - element;

                float rotation = diff.ToRotation() - MathHelper.PiOver2;
                Color color = Lighting.GetColor(element.ToTileCoordinates());

                Main.EntitySpriteDraw(texture, pos - Main.screenPosition, frame, color, rotation, origin, scale, flip, 0);
                Main.EntitySpriteDraw(_men, pos - Main.screenPosition, frame, Color.White, rotation, origin, scale, flip, 0);

                pos += diff;
            }
            return false;
        }
    }
}
