using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.RangedModify.Core;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class Dicoria : ModItem, ICWRLoader
    {
        public override string Texture => CWRConstant.Item_Ranged + "Dicoria";
        [VaultLoaden(CWRConstant.Item_Ranged + "DicoriaGlow")]
        public static Asset<Texture2D> Glow = null;
        public override void SetDefaults() {
            Item.DamageType = DamageClass.Ranged;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.width = 118;
            Item.height = 52;
            Item.damage = 48;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useAmmo = AmmoID.Bullet;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 10;
            Item.UseSound = null;//开火音效在HeldProj
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.buyPrice(0, 1, 60, 10);
            Item.CWR().DeathModeItem = true;
        }

        //物品使用本身不消耗子弹，由手持弹幕在实际开火时自行拾取
        public override bool CanConsumeAmmo(Item ammo, Player player) => BaseHeldGun.AmmoConsumeContext;

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<DicoriaHeld>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<DicoriaHeld>(player, source);

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor
            , Color alphaColor, float rotation, float scale, int whoAmI) {
            spriteBatch.Draw(Glow.Value, Item.Center - Main.screenPosition, null, Color.White
                , rotation, Glow.Value.Size() / 2, scale, SpriteEffects.None, 0);
        }
    }

    internal class DicoriaHeld : BaseHeldGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "Dicoria";
        public override Asset<Texture2D> GlowAsset => Dicoria.Glow;
        public override int TargetID => ModContent.ItemType<Dicoria>();
        public override SoundStyle? ShootSound => CWRSound.Gun_50CAL_Shoot with { Volume = 0.6f };
        /// <summary>每打满一个弹巢循环后，下一发转为发射射线</summary>
        private bool RayShot => fireIndex > 6;
        private bool RayAlive => Owner.ownedProjectileCounts[ModContent.ProjectileType<DicoriaRay>()] != 0;

        //射线持续期间枪体不能消失，需要为射线提供锚点
        public override bool StayAlive() => RayAlive;
        public override void SetGunProperty() {
            GunPressure = 0.1f;
            ControlForce = 0.02f;
            HandIdleDistanceX = 26;
            HandIdleDistanceY = -4;
            HandFireDistanceX = 30;
            HandFireDistanceY = -8;
            MuzzleForwardOffset = 2;
            MuzzleNormalOffset = -16;
            RecoilRetroForceMagnitude = 12;
            RecoilOffsetRecoverValue = 0.9f;
        }

        public override void AI() {
            bool rayAlive = RayAlive;
            //射线持续期间保持瞄准姿势并把枪口对齐射线出口
            AlwaysAimPose = rayAlive;
            MuzzleNormalOffset = rayAlive ? 4 : -16;
            UpdateHeldPose(WantsFireLeft);

            if (!rayAlive && WantsFireLeft && FireCooldown <= 0 && HasAmmo) {
                Fire();
                SetFireCooldown();
            }
            Time++;
        }

        private void Fire() {
            fireIndex++;
            bool rayShot = RayShot;
            if (rayShot) {
                MuzzleNormalOffset = 4;
            }
            SnapToAimPose();
            CreateFireLight();

            if (rayShot) {
                SoundEngine.PlaySound(SoundID.Item69, Projectile.Center);
            }
            else {
                PlayShootSound();
                CreateRecoil();
            }

            if (Projectile.IsOwnedByLocalPlayer()) {
                int shootType = AmmoTypes == ProjectileID.Bullet ? ProjectileID.BulletHighVelocity : AmmoTypes;
                if (rayShot) {
                    Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                        , ModContent.ProjectileType<DicoriaRay>()
                        , WeaponDamage, WeaponKnockback, Owner.whoAmI, Projectile.identity);
                }
                else {
                    Projectile.NewProjectile(Source, ShootPos, ShootVelocity / 2
                        , shootType, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                    Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                        , shootType, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                }
            }

            if (rayShot) {
                fireIndex = 0;
            }
            ConsumeAmmo();
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
            homeProj = Main.projectile.FindByIdentity((int)Projectile.ai[0]);
            if (homeProj.Alives() && homeProj.ModProjectile is DicoriaHeld gun) {
                Projectile.Center = gun.ShootPos;
                Projectile.rotation = homeProj.rotation;
            }

            Color color = VaultUtils.MultiStepColorLerp(Projectile.timeLeft / 60f, Color.IndianRed, Color.Red, Color.DarkRed, Color.Red, Color.IndianRed, Color.OrangeRed);

            toTileLeng = 0;
            Vector2 unitVer = Projectile.rotation.ToRotationVector2();
            Tile tile = Framing.GetTileSafely(Projectile.Center + unitVer * toTileLeng);
            bool isSolid = tile.HasSolidTile();
            while (!isSolid && toTileLeng < 2000) {
                toTileLeng += 8;
                Vector2 targetPos = Projectile.Center + unitVer * toTileLeng;
                tile = Framing.GetTileSafely(targetPos);
                isSolid = tile.HasSolidTile();

                if (toTileLeng % 32 == 0) {
                    Lighting.AddLight(targetPos, color.ToVector3() * (Projectile.timeLeft / 60f));
                }

                if (isSolid) {
                    PRTLoader.NewParticle<PRT_LavaFire>(
                        targetPos + VaultUtils.RandVr(36),
                        new Vector2(unitVer.X, -Main.rand.NextFloat(2, 6)),
                        color, Main.rand.NextFloat(0.8f, 1.2f))?.SetLifetime(8, 20);
                }
                else if (toTileLeng > 90) {
                    if (Main.rand.NextBool(16)) {
                        PRTLoader.NewParticle<PRT_LonginusWave>(targetPos, unitVer, color, 1f).Configure(new Vector2(0.1f, 0.2f), Projectile.rotation, 3f, 2);
                    }
                    PRTLoader.NewParticle<PRT_SparkAlpha>(targetPos, unitVer, color, Main.rand.NextFloat(0.2f, 0.4f) * scaleTimer * 0.6f).Configure(false, 2);
                }
            }

            newPoss = [];
            for (int i = 0; i < MaxPosNum; i++) {
                newPoss.Add(Projectile.Center + unitVer * (i / (float)MaxPosNum * toTileLeng));
            }

            if (!Main.dedServ) {
                Trail ??= new Trail([.. newPoss], (float sengs) => scaleTimer, (Vector2 _) => Color.Red);
                Trail.TrailPositions = [.. newPoss];
            }

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

            Effect effect = EffectLoader.GradientTrail.Value;
            effect.Parameters["transformMatrix"].SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"].SetValue((float)Main.timeForVisualEffects * -0.08f);
            effect.Parameters["uTimeG"].SetValue(Main.GlobalTimeWrappedHourly * -0.2f);
            effect.Parameters["udissolveS"].SetValue(1f);
            effect.Parameters["uBaseImage"].SetValue(CWRAsset.Placeholder_White.Value);
            effect.Parameters["uFlow"].SetValue(CWRAsset.Placeholder_White.Value);
            effect.Parameters["uGradient"].SetValue(CWRAsset.BloodRed_Bar.Value);
            effect.Parameters["uDissolve"].SetValue(CWRAsset.Placeholder_White.Value);

            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            for (int i = 0; i < 6; i++) {
                Trail?.DrawTrail(effect);
            }
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
            return false;
        }
    }
}
