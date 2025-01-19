using CalamityMod.Items;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using static Terraria.ID.ContentSamples.CreativeHelper;

namespace CalamityOverhaul.Content.Items.Rogue.Extras
{
    internal class IceFlySickle : ModItem
    {
        public override string Texture => CWRConstant.Item + "Rogue/IceFlySickle";
        public override void SetDefaults() {
            Item.CloneDefaults(ItemID.IceSickle);
            Item.damage = 20;
            Item.DamageType = CWRLoad.RogueDamageClass;
            Item.shoot = ModContent.ProjectileType<IceFlySickleThrowable>();
            Item.CWR().GetMeleePrefix = Item.CWR().GetRangedPrefix = true;
        }

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 4;

        public override void ModifyResearchSorting(ref ItemGroup itemGroup) => itemGroup = (ItemGroup)CalamityResearchSorting.RogueWeapon;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }

        public override void AddRecipes() {
            Recipe.Create(Type)
                    .AddIngredient(ItemID.IceSickle)
                    .AddTile(TileID.MythrilAnvil)
                    .Register();
            Recipe.Create(ItemID.IceSickle)
                    .AddIngredient(Type)
                    .AddTile(TileID.MythrilAnvil)
                    .Register();
        }
    }

    internal class IceFlySickleThrowable : BaseThrowable
    {
        public override string Texture => CWRConstant.Item + "Rogue/IceFlySickle";
        private bool outFive;
        private HashSet<NPC> onHitNPCs = [];
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 7;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetThrowable() {
            Projectile.DamageType = DamageClass.Melee;
            HandOnTwringMode = -60;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
            Projectile.scale = 1.5f;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return VaultUtils.CircleIntersectsRectangle(Projectile.Center, 75, targetHitbox);
        }

        public override bool PreThrowOut() {
            outFive = true;
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.2f }, Owner.Center);
            return true;
        }

        public override void FlyToMovementAI() {
            float addSpeedBaf = Projectile.ai[2] * 0.01f;
            if (addSpeedBaf > 1.45f) {
                addSpeedBaf = 1.45f;
            }
            Projectile.rotation += Projectile.velocity.X * 0.1f;
            Projectile.rotation += (MathHelper.PiOver4 / 4f + MathHelper.PiOver4 / 2f *
                Math.Clamp(CurrentThrowProgress * 2f, 0, 1)) * Math.Sign(Projectile.velocity.X) * addSpeedBaf;

            if (Projectile.ai[2] > 60) {
                Projectile.Center = Vector2.Lerp(Projectile.Center, Owner.Center, 0.1f);
                Projectile.ChasingBehavior(Owner.Center, 6, 0.1f);
                if (Projectile.Distance(Owner.Center) < Projectile.width) {
                    Projectile.Kill();
                }
            }
            else {
                NPC target = Projectile.Center.FindClosestNPC(300);
                if (target != null) {
                    Projectile.SmoothHomingBehavior(target.Center, 1, 0.2f);
                }
            }

            Projectile.ai[2]++;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (stealthStrike && !target.boss && !CWRLoad.WormBodys.Contains(target.type) && !target.CWR().IceParclose && !onHitNPCs.Contains(target)) {
                Projectile.NewProjectile(Projectile.FromObjectGetParent(), target.Center, Vector2.Zero
                        , ModContent.ProjectileType<IceParclose>(), 0, 0, Projectile.owner
                        , target.whoAmI, target.type, target.rotation);
                onHitNPCs.Add(target);
            }
        }

        public override void DrawThrowable(Color lightColor) {
            Vector2 orig = TextureValue.Size() / 2;
            SpriteEffects spriteEffects = Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            if (outFive) {
                for (int k = 0; k < Projectile.oldPos.Length; k++) {
                    Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + new Vector2(33, 33);
                    Color color = Color.AliceBlue * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length);
                    color.A = 0;
                    Main.EntitySpriteDraw(TextureValue, drawPos, null, color * Projectile.Opacity * 0.65f
                        , Projectile.oldRot[k], orig, Projectile.scale, spriteEffects, 0);
                }
            }

            Main.EntitySpriteDraw(TextureValue, Projectile.Center - Main.screenPosition, null, lightColor
                , Projectile.rotation + (MathHelper.PiOver4 + OffsetRoting) * (Projectile.velocity.X > 0 ? 1 : -1)
                , orig, Projectile.scale, spriteEffects, 0);
        }
    }
}
