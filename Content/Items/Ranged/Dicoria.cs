using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.RangedModify.Core;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria;
using Terraria.Enums;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class Dicoria : ModItem, ICWRLoader
    {
        public override string Texture => CWRConstant.Item_Ranged + "Dicoria";
        public static Asset<Texture2D> Glow;
        void ICWRLoader.LoadAsset() => Glow = CWRUtils.GetT2DAsset(Texture + "Glow");
        void ICWRLoader.UnLoadData() => Glow = null;
        public override void SetDefaults() {
            Item.DamageType = DamageClass.Ranged;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.width = 32;
            Item.height = 32;
            Item.damage = 98;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useAmmo = AmmoID.Bullet;
            Item.shootSpeed = 10;
            Item.UseSound = CWRSound.Gun_50CAL_Shoot with { Volume = 0.6f };
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.buyPrice(0, 1, 60, 10);
            Item.SetCartridgeGun<DicoriaHeld>(60);
            Item.CWR().DeathModeItem = true;
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor
            , Color alphaColor, float rotation, float scale, int whoAmI) {
            spriteBatch.Draw(Glow.Value, Item.Center - Main.screenPosition, null, Color.White
                , rotation, Glow.Value.Size() / 2, scale, SpriteEffects.None, 0);
        }
    }

    internal class DicoriaHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "Dicoria";
        public override string GlowTexPath => CWRConstant.Item_Ranged + "DicoriaGlow";
        public override int TargetID => ModContent.ItemType<Dicoria>();
        public override void SetRangedProperty() {
            Recoil = 0.3f;
            GunPressure = 0.1f;
            ControlForce = 0.02f;
            HandIdleDistanceX = 26;
            HandIdleDistanceY = -4;
            HandFireDistanceX = 30;
            HandFireDistanceY = -8;
            ShootPosNorlLengValue = -16;
            ShootPosToMouLengValue = 2;
            RecoilRetroForceMagnitude = 12;
            RecoilOffsetRecoverValue = 0.9f;
            EnableRecoilRetroEffect = true;
            CanCreateSpawnGunDust = false;
        }

        public override void SetShootAttribute() {
            InOwner_HandState_AlwaysSetInFireRoding = false;
            ShootPosNorlLengValue = -16;
            if (++fireIndex > 6) {
                FireTime = 40;
                GunPressure = 0;
                ControlForce = 0;
                ShootPosNorlLengValue = 4;
                InOwner_HandState_AlwaysSetInFireRoding = true;
                CanUpdateMagazineContentsInShootBool = false;
                AmmoTypes = ModContent.ProjectileType<DicoriaRay>();
                Item.UseSound = SoundID.Item69;
            }
        }

        public override void PostShootEverthing() {
            FireTime = Item.useTime;
            GunPressure = 0.1f;
            ControlForce = 0.02f;
            Item.UseSound = CWRSound.Gun_50CAL_Shoot with { Volume = 0.6f };
            CanUpdateMagazineContentsInShootBool = true;
            if (fireIndex > 6) {
                fireIndex = 0;
            }
        }

        public override bool CanSpanProj() {
            if (Owner.ownedProjectileCounts[ModContent.ProjectileType<DicoriaRay>()] != 0) {
                return false;
            }
            return base.CanSpanProj();
        }

        public override void FiringShoot() {
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = ProjectileID.BulletHighVelocity;
            }
            if (fireIndex > 6) {
                Projectile.NewProjectile(Source, ShootPos, ShootVelocity, AmmoTypes
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, Projectile.whoAmI);
            }
            else {
                Projectile.NewProjectile(Source, ShootPos, ShootVelocity / 2
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
        }
    }

    internal class DicoriaRay : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private const int MaxPosNum = 100;
        private int scaleTimer = 0;
        private int scaleIndex = 0;
        private float toTileLeng;
        private const int disengage = 20;
        private Trail Trail;
        private List<Vector2> newPoss;
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
            if (homeProj.Alives() && homeProj.ModProjectile is BaseFeederGun gun) {
                Projectile.Center = gun.ShootPos;
                Projectile.rotation = homeProj.rotation;
            }

            Color color = VaultUtils.MultiStepColorLerp(Projectile.timeLeft / 60f, Color.IndianRed, Color.Red, Color.DarkRed, Color.Red, Color.IndianRed, Color.OrangeRed);

            toTileLeng = 0;
            Vector2 unitVer = Projectile.rotation.ToRotationVector2();
            Tile tile = CWRUtils.GetTile(CWRUtils.WEPosToTilePos(Projectile.Center + unitVer * toTileLeng));
            bool isSolid = tile.HasSolidTile();
            while (!isSolid && toTileLeng < 2000) {
                toTileLeng += 8;
                Vector2 targetPos = Projectile.Center + unitVer * toTileLeng;
                tile = CWRUtils.GetTile(CWRUtils.WEPosToTilePos(targetPos));
                isSolid = tile.HasSolidTile();

                if (toTileLeng % 32 == 0) {
                    Lighting.AddLight(targetPos, color.ToVector3() * (Projectile.timeLeft / 60f));
                }

                if (isSolid) {
                    PRT_LavaFire lavaFire = new PRT_LavaFire {
                        Velocity = new Vector2(unitVer.X, -Main.rand.NextFloat(2, 6)),
                        Position = targetPos + CWRUtils.randVr(36),
                        Scale = Main.rand.NextFloat(0.8f, 1.2f),
                        maxLifeTime = 20,
                        minLifeTime = 8,
                        Color = color
                    };
                    PRTLoader.AddParticle(lavaFire);
                }
                else if (toTileLeng > 90) {
                    if (Main.rand.NextBool(16)) {
                        PRT_LonginusWave wave = new PRT_LonginusWave(targetPos, unitVer
                            , color, new Vector2(0.1f, 0.2f), Projectile.rotation, 1, 3, 2, null);
                        PRTLoader.AddParticle(wave);
                    }
                    PRTLoader.AddParticle(new PRT_SparkAlpha(targetPos, unitVer, false, 2, Main.rand.NextFloat(0.2f, 0.4f) * scaleTimer * 0.6f, color));
                }
            }

            newPoss = [];
            for (int i = 0; i < MaxPosNum; i++) {
                newPoss.Add(Projectile.Center + unitVer * (i / (float)MaxPosNum * toTileLeng));
            }
            Trail ??= new Trail(newPoss.ToArray(), (float sengs) => scaleTimer, (Vector2 _) => Color.Red);
            Trail.TrailPositions = newPoss.ToArray();

            if (Projectile.alpha < 255) {
                Projectile.alpha += 15;
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

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , Projectile.Center, Projectile.rotation.ToRotationVector2() * toTileLeng + Projectile.Center, scaleTimer * 4, ref point);
        }

        public override void CutTiles() {
            DelegateMethods.tilecut_0 = TileCuttingContext.AttackProjectile;
            Utils.PlotTileLine(Projectile.Center, Projectile.rotation.ToRotationVector2() * toTileLeng + Projectile.Center, Projectile.width, DelegateMethods.CutTiles);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            int starPoints = 8;
            for (int i = 0; i < starPoints; i++) {
                float angle = MathHelper.TwoPi * i / starPoints;
                for (int j = 0; j < 12; j++) {
                    float starSpeed = MathHelper.Lerp(2f, 10f, j / 12f);
                    Color dustColor = Color.Lerp(Color.Red, Color.DarkRed, j / 12f);
                    float dustScale = MathHelper.Lerp(1.6f, 0.85f, j / 12f);

                    Dust fire = Dust.NewDustPerfect(target.Center, DustID.RedTorch);
                    fire.velocity = angle.ToRotationVector2() * starSpeed;
                    fire.color = dustColor;
                    fire.scale = dustScale;
                    fire.noGravity = true;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Trail == null) {
                return false;
            }

            Effect effect = Filters.Scene["CWRMod:gradientTrail"].GetShader().Shader;
            effect.Parameters["transformMatrix"].SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"].SetValue((float)Main.timeForVisualEffects * -0.08f);
            effect.Parameters["uTimeG"].SetValue(Main.GlobalTimeWrappedHourly * -0.2f);
            effect.Parameters["udissolveS"].SetValue(1f);
            effect.Parameters["uBaseImage"].SetValue(CWRUtils.GetT2DValue(CWRConstant.Placeholder2));
            effect.Parameters["uFlow"].SetValue(CWRUtils.GetT2DValue(CWRConstant.Placeholder2));
            effect.Parameters["uGradient"].SetValue(CWRUtils.GetT2DValue(CWRConstant.ColorBar + "BloodRed_Bar"));
            effect.Parameters["uDissolve"].SetValue(CWRUtils.GetT2DValue(CWRConstant.Placeholder2));

            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            for (int i = 0; i < 6; i++) {
                Trail?.DrawTrail(effect);
            }
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
            return false;
        }
    }
}
