using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Dusts;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Summon.Whips
{
    internal class GhostFireWhipProjectile : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Summon + "GhostFireWhipProjectile";

        private List<Vector2> whipPoints => Projectile.GetWhipControlPoints();

        public override void SetStaticDefaults() {
            ProjectileID.Sets.IsAWhip[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.DefaultToWhip();
            Projectile.ownerHitCheck = true;
            Projectile.WhipSettings.Segments = 30;
            Projectile.WhipSettings.RangeMultiplier = 1.8f;
        }

        public override bool PreAI() {
            if (whipPoints.Count - 2 >= 0 && whipPoints.Count - 2 < whipPoints.Count) {
                Vector2 pos = whipPoints[whipPoints.Count - 2];
                Player owners = CWRUtils.GetPlayerInstance(Projectile.owner);
                if (owners != null) {
                    float lengs = owners.Center.To(pos).Length();
                    if (lengs > 60) {
                        int num = Dust.NewDust(pos, Projectile.width, Projectile.height, ModContent.DustType<SoulFire>(), Projectile.direction * 2, 0f, 150, new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB), 1.3f);
                        Main.dust[num].noGravity = true;
                        Main.dust[num].velocity = new Vector2(0, -Main.rand.NextFloat(0.8f, 1.6f));
                    }
                }
            }
            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Projectile.damage -= 115;
            if (Projectile.damage <= 0)
                Projectile.damage = 5;
            target.AddBuff(ModContent.BuffType<SoulBurning>(), 60);
            if (Projectile.numHits == 0) {
                target.CWR().WhipHitNum += 3;
                target.CWR().WhipHitType = (byte)WhipHitTypeEnum.GhostFireWhip;

                for (int i = 0; i < 3; i++) {
                    int proj = Projectile.NewProjectile(
                        Projectile.parent(),
                        target.Center -
                        Main.player[Projectile.owner].Center.To(target.Center).UnitVector()
                        .RotatedBy(MathHelper.ToRadians(Main.rand.Next(-75, 75))) * 300,
                        CWRUtils.randVr(6, 9),
                        ModContent.ProjectileType<FateCluster>(),
                        Projectile.damage / 2,
                        0,
                        Projectile.owner,
                        Projectile.whoAmI
                    );
                    Projectile newDoms = Main.projectile[proj];
                    newDoms.DamageType = DamageClass.Summon;
                    newDoms.timeLeft = 65;
                    newDoms.ai[0] = 1;
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
            Texture2D texture = CWRUtils.GetT2DValue(Texture);
            Texture2D _men = CWRUtils.GetT2DValue(Texture + "Glow");

            Vector2 pos = whipPoints[0];

            for (int i = 0; i < whipPoints.Count - 1; i++) {
                Rectangle frame = new Rectangle(0, 0, 36, 58);

                Vector2 origin = new Vector2(20, 33);
                float scale = 1;

                int count = i % 4;

                switch (count) {
                    case 0:
                    case 1:
                    case 2:
                    case 3:
                        frame.Y = 60;
                        frame.Height = 18;
                        origin = new Vector2(20, 10);
                        break;
                }

                if (i == whipPoints.Count - 2) {
                    frame.Y = 118;
                    frame.Height = 48;
                    origin = new Vector2(20, 23);
                }

                if (i == 0) {
                    frame = new Rectangle(0, 0, 36, 58);
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
