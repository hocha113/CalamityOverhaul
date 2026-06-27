using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Summon
{
    internal class GhostFireWhip : ModItem
    {
        public override string Texture => CWRConstant.Item_Summon + "GhostFireWhip";

        public override void SetDefaults() {
            Item.DefaultToWhip(ModContent.ProjectileType<GhostFireWhipProjectile>(), 220, 1, 12, 30);
            Item.rare = ItemRarityID.Purple;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.value = Terraria.Item.buyPrice(0, 16, 5, 75);
            Item.rare = ItemRarityID.Cyan;
        }

        public override bool MeleePrefix() {
            return true;
        }

        public override void AddRecipes() {
            if (CWRID.Item_RuinousSoul > 0) {
                _ = CreateRecipe()
                .AddIngredient(ItemID.BoneWhip)
                .AddIngredient(CWRID.Item_RuinousSoul, 5)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
            }
            else {
                CreateRecipe()
                .AddIngredient(ItemID.LunarBar, 5)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
            }
        }
    }

    internal class GhostFireWhipProjectile : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Summon + "GhostFireWhipProjectile";
        [VaultLoaden(CWRConstant.Projectile_Summon + "GhostFireWhipProjectileGlow")]
        private static Asset<Texture2D> Glow = null;

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
                Projectile.owner.TryGetPlayer(out Player owners);
                if (owners != null) {
                    float lengs = owners.Center.To(pos).Length();
                    if (lengs > 60) {
                        PRTLoader.NewParticle<PRT_SoulFire>(pos, new Vector2(0, -Main.rand.NextFloat(0.8f, 1.6f)));
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
        }

        private static void DrawLine(List<Vector2> list) {
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
        }//鞭连接线

        public override bool PreDraw(ref Color lightColor) {
            DrawLine(whipPoints);
            SpriteEffects flip = Projectile.spriteDirection < 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Texture2D _men = Glow.Value;

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
