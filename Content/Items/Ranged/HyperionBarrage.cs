using CalamityMod;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.RangedModify.Core;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class HyperionBarrage : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "HyperionBarrage";
        [VaultLoaden(CWRConstant.Item_Ranged + "HyperionBarrageGlow")]
        public static Asset<Texture2D> Glow = null;
        public override void SetDefaults() {
            Item.DamageType = DamageClass.Ranged;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.width = 94;
            Item.height = 34;
            Item.damage = 452;
            Item.useTime = 80;
            Item.useAnimation = 80;
            Item.useAmmo = AmmoID.Bullet;
            Item.shootSpeed = 15;
            Item.UseSound = SoundID.Item15 with { Pitch = -0.2f };
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.buyPrice(0, 2, 60, 10);
            Item.SetHeldProj<HyperionBarrageHeld>();
            Item.CWR().DeathModeItem = true;
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor
            , Color alphaColor, float rotation, float scale, int whoAmI) {
            spriteBatch.Draw(Glow.Value, Item.Center - Main.screenPosition
                , null, Color.White, rotation, Glow.Value.Size() / 2, scale, SpriteEffects.None, 0);
        }
    }

    internal class HyperionBarrageEX : ModItem, ICWRLoader
    {
        public override string Texture => CWRConstant.Item_Ranged + "HyperionBarrageEX";
        [VaultLoaden(CWRConstant.Item_Ranged + "HyperionBarrageEXGlow")]
        public static Asset<Texture2D> Glow = null;
        public override void SetDefaults() {
            Item.DamageType = DamageClass.Ranged;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.width = 124;
            Item.height = 46;
            Item.damage = 1052;
            Item.useTime = 60;
            Item.useAnimation = 60;
            Item.useAmmo = AmmoID.Bullet;
            Item.shootSpeed = 15;
            Item.UseSound = SoundID.Item15 with { Pitch = -0.2f };
            Item.rare = ItemRarityID.Red;
            Item.value = Item.buyPrice(0, 8, 60, 10);
            Item.SetHeldProj<HyperionBarrageEXHeld>();
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor
            , Color alphaColor, float rotation, float scale, int whoAmI) {
            spriteBatch.Draw(Glow.Value, Item.Center - Main.screenPosition
                , null, Color.White, rotation, Glow.Value.Size() / 2, scale, SpriteEffects.None, 0);
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<HyperionBarrage>().
                AddIngredient<SoulofFrightEX>().
                AddTile(TileID.LunarCraftingStation).
                Register();
        }
    }

    internal class HyperionBarrageHeld : BaseGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "HyperionBarrage";
        public override string GlowTexPath => CWRConstant.Item_Ranged + "HyperionBarrageGlow";
        public override int TargetID => ModContent.ItemType<HyperionBarrage>();
        public override void SetRangedProperty() {
            Recoil = 0.3f;
            GunPressure = 0;
            ControlForce = 0;
            HandIdleDistanceX = 26;
            HandIdleDistanceY = 2;
            HandFireDistanceX = 26;
            HandFireDistanceY = -2;
            ShootPosNorlLengValue = -4;
            ShootPosToMouLengValue = 8;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            CanCreateCaseEjection = false;
            CanCreateSpawnGunDust = false;
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, Owner.Center, ShootVelocity
                    , ModContent.ProjectileType<PrimeCannonOnSpanFriendly>()
                    , WeaponDamage, WeaponKnockback, Owner.whoAmI, Projectile.identity);
            if (++fireIndex > 3) {
                Projectile.NewProjectile(Source, Owner.Center, ShootVelocity
                    , ModContent.ProjectileType<PrimeCannonOnSpanFriendly>()
                    , WeaponDamage, WeaponKnockback, Owner.whoAmI, Projectile.identity, -0.1f);
                Projectile.NewProjectile(Source, Owner.Center, ShootVelocity
                        , ModContent.ProjectileType<PrimeCannonOnSpanFriendly>()
                        , WeaponDamage, WeaponKnockback, Owner.whoAmI, Projectile.identity, 0.1f);
                fireIndex = 0;
            }
        }
    }

    internal class HyperionBarrageEXHeld : BaseGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "HyperionBarrageEX";
        public override string GlowTexPath => CWRConstant.Item_Ranged + "HyperionBarrageEXGlow";
        public override int TargetID => ModContent.ItemType<HyperionBarrageEX>();
        public override void SetRangedProperty() {
            Recoil = 0.2f;
            GunPressure = 0;
            ControlForce = 0;
            HandIdleDistanceX = 26;
            HandIdleDistanceY = 2;
            HandFireDistanceX = 26;
            HandFireDistanceY = -2;
            ShootPosNorlLengValue = -4;
            ShootPosToMouLengValue = 8;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            CanCreateCaseEjection = false;
            CanCreateSpawnGunDust = false;
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, Owner.Center, ShootVelocity
                    , ModContent.ProjectileType<PrimeCannonOnSpanFriendly>()
                    , WeaponDamage, WeaponKnockback, Owner.whoAmI, Projectile.identity, 0, 1);
        }
    }

    internal class RocketSkeletonFriendly : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder3;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.hostile = false;
            Projectile.friendly = true;
            Projectile.timeLeft = 600;
            Projectile.extraUpdates = 6;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Projectile.scale += 0.012f;

            if (PRTLoader.NumberUsablePRT() > 10) {
                BasePRT spark2 = new PRT_SparkAlpha(Projectile.Center, Projectile.velocity * 0.7f, false, 6, 1.4f, Color.DarkRed);
                PRTLoader.AddParticle(spark2);
                BasePRT spark = new PRT_Spark(Projectile.Center, Projectile.velocity * 0.7f, false, 10, 1f, Color.LightGoldenrodYellow);
                PRTLoader.AddParticle(spark);
            }
            else {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.FireworkFountain_Red, Projectile.velocity);
                dust.noGravity = true;
                dust.scale *= Main.rand.NextFloat(0.3f, 1.2f);
            }
            Projectile.localAI[0]++;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.localAI[0] <= 0) {
                Projectile.tileCollide = false;
                return false;
            }
            return base.OnTileCollide(oldVelocity);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            Projectile.LargeFieryExplosion();
            target.AddBuff(ModContent.BuffType<HellburnBuff>(), 60);
            Projectile.numHits++;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Projectile.LargeFieryExplosion();
            target.AddBuff(ModContent.BuffType<HellburnBuff>(), 60);
            Projectile.numHits++;
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.numHits <= 0) {
                Projectile.LargeFieryExplosion();
            }
            if (Projectile.IsOwnedByLocalPlayer() && Projectile.ai[2] > 0) {
                for (int i = 0; i < 6; i++) {
                    Projectile.NewProjectile(Projectile.FromObjectGetParent(), Projectile.Center, (MathHelper.TwoPi / 6 * i).ToRotationVector2() * 8
                    , ModContent.ProjectileType<PrimeCannonOnSpanFriendly>(), Projectile.damage, Projectile.knockBack, Projectile.owner, ai0: -1);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = TextureAssets.Projectile[ProjectileID.RocketSkeleton].Value;
            Main.EntitySpriteDraw(mainValue, Projectile.Center - Main.screenPosition, null, Color.White, Projectile.rotation
                , mainValue.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    internal class PrimeCannonOnSpanFriendly : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";
        private int scaleTimer = 0;
        private int scaleIndex = 0;
        private const int disengage = 20;
        private Projectile homeProj;
        public override bool ShouldUpdatePosition() => false;
        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = true;
            Projectile.scale = 1f;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = disengage + 40;
            Projectile.alpha = 0;
        }

        public override void AI() {
            homeProj = CWRUtils.GetProjectileInstance((int)Projectile.ai[0]);
            if (homeProj.Alives()) {
                Projectile.Center = homeProj.Center;
                Projectile.rotation = homeProj.rotation + Projectile.ai[1];
            }
            else {
                Projectile.rotation = Projectile.velocity.ToRotation();
            }

            if (scaleTimer < 8 && scaleIndex == 0) {
                scaleTimer++;
            }

            if (Projectile.timeLeft < disengage) {
                scaleIndex = 1;
            }

            if (scaleIndex > 0) {
                if (--scaleTimer <= 0) {
                    Projectile.Kill();
                }
            }

            Projectile.localAI[0]++;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item62, Projectile.Center);
            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile projectile = Projectile.NewProjectileDirect(Projectile.GetSource_FromAI(), Projectile.Center, Projectile.rotation.ToRotationVector2() * 13
                        , ModContent.ProjectileType<RocketSkeletonFriendly>(), Projectile.damage, 0f, Main.myPlayer, 0, 0, Projectile.ai[2]);
                projectile.tileCollide = !CWRUtils.GetTile(CWRUtils.WEPosToTilePos(Projectile.Center)).HasSolidTile();
                projectile.netUpdate = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (scaleTimer < 0) {
                return false;
            }

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Color drawColor = Color.White;
            drawColor.A = 0;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, drawColor
                , Projectile.rotation, new Vector2(0, tex.Height / 2f), new Vector2(1000, scaleTimer * 0.016f), SpriteEffects.None, 0);

            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs
            , List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);
    }
}
