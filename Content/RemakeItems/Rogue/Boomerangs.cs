using CalamityOverhaul.Content.Projectiles.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Rogue
{
    internal class ModifyBananarang : ItemOverride
    {
        public override int TargetID => ItemID.Bananarang;
        public override void SetDefaults(Item item) {
            item.damage = 56;
            item.DamageType = CWRLoad.RogueDamageClass;
            item.shoot = ModContent.ProjectileType<BananarangHeld>();
        }
        public override bool? On_CanUseItem(Item item, Player player) => player.ownedProjectileCounts[item.shoot] <= 16;
        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    internal class BananarangHeld : BaseThrowable
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Bananarang].Value;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 4;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }
        public override void SetThrowable() {
            CWRUtils.SafeLoadItem(ItemID.Bananarang);
            HandOnTwringMode = -30;
            OffsetRoting = MathHelper.ToRadians(30 + 180);
            Projectile.CWR().HitAttribute.WormResistance = 0.6f;
            UseDrawTrail = true;
        }

        public override void PostSetThrowable() {
            if (stealthStrike && Projectile.ai[2] == 0) {
                Projectile.scale *= 1.25f;
            }
        }

        public override bool PreThrowOut() {
            if (stealthStrike && Projectile.ai[2] == 0 && Projectile.IsOwnedByLocalPlayer()) {
                for (int i = 0; i < 2; i++) {
                    Projectile.NewProjectileDirect(Projectile.GetSource_FromAI(), Projectile.Center
                        , Projectile.velocity.RotatedBy(i == 0 ? -0.3f : 0.3f), Type, Projectile.damage, 0.2f, Owner.whoAmI, ai2: 1);
                }
            }
            return base.PreThrowOut();
        }

        public override void FlyToMovementAI() {
            base.FlyToMovementAI();
            int dust = Dust.NewDust(Projectile.position, (int)Projectile.Size.X, (int)Projectile.Size.Y, DustID.BubbleBurst_Green);
            Main.dust[dust].noGravity = true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (stealthStrike && Projectile.ai[2] == 0 && Projectile.numHits == 0) {
                Projectile.numHits++;
                Projectile.Explode();
                for (int i = 0; i < 26; i++) {
                    Vector2 spanPos = target.Top + new Vector2(Main.rand.Next(-80, 80), -20);
                    Projectile projectile = Projectile.NewProjectileDirect(Projectile.GetSource_FromAI(), spanPos
                        , new Vector2(Main.rand.NextFloat(-6, 6), -6), ProjectileID.HappyBomb, Projectile.damage, 0.2f, Owner.whoAmI, ai2: 1);
                    projectile.friendly = true;
                    projectile.CWR().HitAttribute.WormResistance = 0.4f;
                }
            }
        }
    }

    internal class ModifyTrimarang : ItemOverride
    {
        public override int TargetID => ItemID.Trimarang;
        public override void SetDefaults(Item item) {
            item.damage = 16;
            item.DamageType = CWRLoad.RogueDamageClass;
            item.shoot = ModContent.ProjectileType<TrimarangHeld>();
        }
        public override bool? On_CanUseItem(Item item, Player player) => player.ownedProjectileCounts[item.shoot] <= 16;
        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    internal class TrimarangHeld : BaseThrowable
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Trimarang].Value;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 4;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }
        public override void SetThrowable() {
            CWRUtils.SafeLoadItem(ItemID.Trimarang);
            HandOnTwringMode = -30;
            OffsetRoting = MathHelper.ToRadians(30 + 180);
            Projectile.CWR().HitAttribute.WormResistance = 0.6f;
            UseDrawTrail = true;
        }

        public override void PostSetThrowable() {
            if (stealthStrike) {
                Projectile.scale *= 1.6f;
            }
        }

        public override bool PreThrowOut() {
            if (Projectile.ai[2] == 0 && Projectile.IsOwnedByLocalPlayer()) {
                for (int i = 0; i < 2; i++) {
                    Projectile projectile = Projectile.NewProjectileDirect(Projectile.GetSource_FromAI(), Projectile.Center
                        , Projectile.velocity.RotatedBy(i == 0 ? -2.2f : 2.2f), Type, Projectile.damage, 0.2f, Owner.whoAmI, ai2: 1);
                    if (stealthStrike) {
                        projectile.scale = 1.6f;
                    }
                }
            }
            return base.PreThrowOut();
        }

        public override void FlyToMovementAI() {
            base.FlyToMovementAI();
            for (int i = 0; i < 3; i++) {
                int dust = Dust.NewDust(Projectile.position, (int)Projectile.Size.X, (int)Projectile.Size.Y
                    , DustID.MagicMirror, Projectile.velocity.X * -0.1f, Projectile.velocity.Y * -0.1f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (stealthStrike && Projectile.ai[2] == 0 && Projectile.numHits == 0) {
                Projectile.numHits++;
                Projectile.Explode();
                int num = Main.rand.Next(3);
                if (num == 0) {
                    for (int i = 0; i < 6; i++) {
                        Projectile proj = Projectile.NewProjectileDirect(Projectile.GetSource_FromAI()
                            , Projectile.Center + new Vector2(0, -588).RotatedByRandom(0.35f)
                            , new Vector2(0, 20).RotatedByRandom(0.2f) * Main.rand.NextFloat(0.6f, 1f)
                            , ProjectileID.SuperStar, Projectile.damage / 2, 0.2f, Owner.whoAmI, ai2: 1);
                        proj.extraUpdates = 3;
                        proj.scale *= Main.rand.NextFloat(0.3f, 0.6f);
                        proj.DamageType = CWRLoad.RogueDamageClass;
                        proj.CWR().HitAttribute.WormResistance = 0.4f;
                    }
                }
                else if (num == 1) {
                    Projectile.NewProjectileDirect(Projectile.FromObjectGetParent(), Projectile.Center, Projectile.velocity * 0.1f
                    , ModContent.ProjectileType<IceExplosionFriend>(), 13, 0, Projectile.owner, 0);
                }
                else if (num == 2) {
                    float rand = Main.rand.NextFloat(MathHelper.TwoPi);
                    for (int i = 0; i < 6; i++) {
                        Projectile proj = Projectile.NewProjectileDirect(Projectile.GetSource_FromAI()
                            , Projectile.Center, (MathHelper.TwoPi / 6 * i + rand).ToRotationVector2() * 6
                            , ProjectileID.Mushroom, Projectile.damage / 5, 0.2f, Owner.whoAmI);
                        proj.DamageType = CWRLoad.RogueDamageClass;
                    }
                }
            }
        }
    }

    internal class ModifyIceBoomerang : ItemOverride
    {
        public override int TargetID => ItemID.IceBoomerang;
        public override void SetDefaults(Item item) {
            item.damage = 18;
            item.DamageType = CWRLoad.RogueDamageClass;
            item.shoot = ModContent.ProjectileType<IceBoomerangHeld>();
        }
        public override bool? On_CanUseItem(Item item, Player player) => player.ownedProjectileCounts[item.shoot] <= 4;
        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    internal class IceBoomerangHeld : BaseThrowable
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.IceBoomerang].Value;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 4;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }
        public override void SetThrowable() {
            CWRUtils.SafeLoadItem(ItemID.IceBoomerang);
            HandOnTwringMode = -30;
            OffsetRoting = MathHelper.ToRadians(30 + 180);
            Projectile.CWR().HitAttribute.WormResistance = 0.6f;
            UseDrawTrail = true;
        }

        public override void PostSetThrowable() {
            if (stealthStrike && Projectile.ai[2] == 0) {
                Projectile.scale *= 1.25f;
            }
        }

        public override bool PreThrowOut() {
            if (stealthStrike && Projectile.ai[2] == 0 && Projectile.IsOwnedByLocalPlayer()) {
                for (int i = 0; i < 2; i++) {
                    Projectile.NewProjectileDirect(Projectile.GetSource_FromAI(), Projectile.Center
                        , Projectile.velocity.RotatedBy(i == 0 ? -0.3f : 0.3f), Type, Projectile.damage, 0.2f, Owner.whoAmI, ai2: 1);
                }
            }
            return base.PreThrowOut();
        }

        public override void FlyToMovementAI() {
            base.FlyToMovementAI();
            int dust = Dust.NewDust(Projectile.position, (int)Projectile.Size.X, (int)Projectile.Size.Y, DustID.Snow);
            Main.dust[dust].noGravity = true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (stealthStrike && Projectile.ai[2] == 0 && Projectile.numHits == 0) {
                Projectile.numHits++;
                Projectile.Explode();
                Projectile.NewProjectileDirect(Projectile.FromObjectGetParent(), Projectile.Center, Projectile.velocity * 0.1f
                    , ModContent.ProjectileType<IceExplosionFriend>(), 13, 0, Projectile.owner, 0);
            }
        }
    }
}
