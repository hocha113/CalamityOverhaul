using CalamityMod.NPCs.Crabulon;
using CalamityOverhaul.Content.NPCs.Modifys;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Tools
{
    internal class FungalFeed : ModItem//触发物品，真菌饲料
    {
        public override string Texture => CWRConstant.Item + "Tools/FungalFeed";
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.damage = 2;
            Item.knockBack = 2;
            Item.DamageType = DamageClass.Generic;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = Item.useAnimation = 12;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.consumable = true;
            Item.maxStack = 64;
            Item.value = 5800;
            Item.shootSpeed = 12;
            Item.rare = ItemRarityID.Cyan;
            Item.shoot = ModContent.ProjectileType<FungalFeedProj>();
        }
    }

    internal class FungalFeedProj : ModProjectile
    {
        public override string Texture => CWRConstant.Item + "Tools/FungalFeed";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.timeLeft = 600;
        }

        public override void AI() {
            Projectile.ai[0] += 1f;
            if (Projectile.ai[0] > 10f) {
                Projectile.ai[0] = 10f;
                if (Projectile.velocity.Y == 0f && Projectile.velocity.X != 0f) {
                    Projectile.velocity.X = Projectile.velocity.X * 0.96f;

                    if (Projectile.velocity.X > -0.01 && Projectile.velocity.X < 0.01) {
                        Projectile.velocity.X = 0f;
                        Projectile.netUpdate = true;
                    }
                }
                Projectile.velocity.Y = Projectile.velocity.Y + 0.2f;
            }
            Projectile.rotation += Projectile.velocity.X * 0.1f;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.NPCDeath1, Projectile.Center);
            for (int i = 0; i < 22; i++) {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height
                    , DustID.GlowingMushroom, Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 1f));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            if (target.type != ModContent.NPCType<Crabulon>()) {
                return;
            }

            if (!target.TryGetOverride<ModifyCrabulon>(out var modifyCrabulon)) {
                return;
            }

            modifyCrabulon.Feed(Projectile);
            modifyCrabulon.SendFeedPacket(Projectile.identity);
        }

        public override bool TileCollideStyle(ref int width, ref int height, ref bool fallThrough, ref Vector2 hitboxCenterFrac) {
            width = height = 22;
            return true;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.velocity.X != oldVelocity.X && Math.Abs(oldVelocity.X) > 1f) {
                Projectile.velocity.X = oldVelocity.X * -0.2f;
            }
            if (Projectile.velocity.Y != oldVelocity.Y && Math.Abs(oldVelocity.Y) > 1f) {
                Projectile.velocity.Y = oldVelocity.Y * -0.2f;
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Rectangle rectangle = texture.GetRectangle();
            Vector2 drawOrigin = rectangle.Size() / 2;

            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + Projectile.Size / 2;
                Color color = Color.White * (float)((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length / 2);
                Main.EntitySpriteDraw(texture, drawPos, rectangle, color, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, rectangle, Color.White, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
