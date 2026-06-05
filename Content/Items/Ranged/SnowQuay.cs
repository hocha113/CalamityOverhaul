using CalamityOverhaul.Content.RangedModify.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class SnowQuay : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "SnowQuay";
        public override void SetDefaults() {
            Item.CloneDefaults(CWRID.Item_Onyxia);
            Item.damage = 22;
            Item.useAmmo = AmmoID.Snowball;
            Item.UseSound = SoundID.Item36 with { Pitch = -0.2f };
            Item.SetHeldProj<SnowQuayHeld>();
            Item.value = Terraria.Item.buyPrice(0, 1, 75, 0);
        }

        public override void AddRecipes() {
            if (!CWRRef.Has) {
                CreateRecipe().
                AddIngredient(ItemID.IceBlock, 600).
                AddTile(TileID.IceMachine).
                Register();
                return;
            }
            _ = CreateRecipe().
                AddIngredient(CWRID.Item_FlurrystormCannon).
                AddIngredient(CWRID.Item_EssenceofEleum, 10).
                AddIngredient(ItemID.IceBlock, 600).
                AddTile(TileID.IceMachine).
                Register();
        }
    }

    internal class SnowQuayHeld : BaseGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "SnowQuayHeld";
        public override int TargetID => ModContent.ItemType<SnowQuay>();
        public override void SetRangedProperty() {
            GunPressure = 0;
            HandIdleDistanceX = 32;
            HandIdleDistanceY = 6;
            HandFireDistanceX = 32;
            HandFireDistanceY = -2;
            ShootPosNorlLengValue = -4;
            ShootPosToMouLengValue = 8;
            RecoilRetroForceMagnitude = 5;
            EnableRecoilRetroEffect = true;
            ForcedConversionTargetAmmoFunc = () => true;
            ToTargetAmmo = ModContent.ProjectileType<SnowQuayBall>();
            SpwanGunDustData.dustID1 = 76;
            SpwanGunDustData.dustID2 = 149;
            SpwanGunDustData.dustID3 = 76;
        }

        public override void PostInOwner() {
            if (DownLeft && !Owner.mouseInterface) {
                fireIndex++;
                if (fireIndex < 90) {
                    VaultUtils.ClockFrame(ref Projectile.frame, 2, 3);
                    if (fireIndex % 10 == 0) {
                        SoundEngine.PlaySound(SoundID.Item23 with { MaxInstances = 3, Volume = 0.2f + fireIndex * 0.006f }, Projectile.Center);
                    }

                    FiringDefaultSound = EnableRecoilRetroEffect = false;
                    ShootCoolingValue = 2;
                }
                else {
                    Projectile.frameCounter++;
                    if (Projectile.frameCounter >= 2) {
                        Projectile.frame++;
                        if (Projectile.frame > 5) {
                            Projectile.frame = 4;
                        }
                        Projectile.frameCounter = 0;
                    }
                    FiringDefaultSound = EnableRecoilRetroEffect = true;
                }
            }
            else {
                fireIndex = 0;
                Projectile.frame = 0;
            }
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            Main.EntitySpriteDraw(TextureValue, drawPos, TextureValue.GetRectangle(Projectile.frame, 6), lightColor
                , Projectile.rotation, VaultUtils.GetOrig(TextureValue, 6), Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }

    internal class SnowQuayBall : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder3;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.light = 0.2f;
            Projectile.ArmorPenetration = 10;
        }

        public override void AI() {
            Projectile.rotation += Projectile.velocity.X * 0.1f;
            if (Main.rand.NextBool()) {
                int index2 = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height
                    , DustID.BlueCrystalShard, Projectile.velocity.X, Projectile.velocity.Y, 0, default, 1.1f);
                Main.dust[index2].noGravity = true;
            }
            Projectile.velocity.Y += 0.1f;
            if (++Projectile.ai[0] > 30) {
                Projectile.velocity.Y += 1f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => target.AddBuff(BuffID.Frostburn, 180);

        public override void OnKill(int timeLeft) {
            var source = Main.player[Projectile.owner].GetShootState().Source;
            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectileDirect(source, Projectile.Center, new Vector2(Main.rand.NextFloat(-3, 3), -3)
                    , ModContent.ProjectileType<IceExplosionFriend>(), 13, 0, Projectile.owner, 0);
                Projectile proj2 = Projectile.NewProjectileDirect(source, Projectile.Center, new Vector2(Main.rand.NextFloat(-5, 5), -13)
                    , ProjectileID.SnowBallFriendly, 13, 0, Projectile.owner, 0);
                proj2.scale += Main.rand.NextFloat(0.5f);
                proj2.light += 0.5f;
                proj2.penetrate += 2;
                proj2.extraUpdates += 2;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            return base.OnTileCollide(oldVelocity);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[ProjectileID.SnowBallFriendly].Value;
            Vector2 drawPos = Projectile.Center;
            Vector2 origPos = value.Size();
            drawPos.X -= Main.screenPosition.X;
            drawPos.Y -= Main.screenPosition.Y;
            origPos.X /= 2;
            origPos.Y /= 2;
            Main.EntitySpriteDraw(value, drawPos, null, Color.White, Projectile.rotation, origPos, Projectile.scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(value, drawPos, null, Color.White, Projectile.rotation, origPos, Projectile.scale + 0.1f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(value, drawPos, null, Color.White, Projectile.rotation, origPos, Projectile.scale + 0.2f, SpriteEffects.None, 0);
            return false;
        }
    }
}
