using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.RangedModify.Core;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class CommandersStaff : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "CommandersStaff";
        public override void SetDefaults() {
            Item.DamageType = DamageClass.Magic;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.width = 32;
            Item.height = 32;
            Item.damage = 72;
            Item.useTime = 62;
            Item.useAnimation = 62;
            Item.mana = 20;
            Item.shoot = ModContent.ProjectileType<CommandersRay>();
            Item.shootSpeed = 10;
            Item.UseSound = null;//开火音效在HeldProj
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.buyPrice(0, 1, 60, 10);
            Item.CWR().DeathModeItem = true;
        }

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<CommandersStaffHeld>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<CommandersStaffHeld>(player, source);
    }

    internal class CommandersStaffEX : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "CommandersStaffEX";
        public override void SetDefaults() {
            Item.DamageType = DamageClass.Magic;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.width = 32;
            Item.height = 32;
            Item.damage = 202;
            Item.useTime = 62;
            Item.useAnimation = 62;
            Item.mana = 20;
            Item.shoot = ModContent.ProjectileType<CommandersRay>();
            Item.shootSpeed = 10;
            Item.UseSound = null;//开火音效在HeldProj
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.buyPrice(0, 8, 60, 10);
        }

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<CommandersStaffEXHeld>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<CommandersStaffEXHeld>(player, source);

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<CommandersStaff>().
                AddIngredient<SoulofFrightEX>().
                AddTile(TileID.LunarCraftingStation).
                Register();
        }
    }

    /// <summary>
    /// 指挥官法杖的共同持握行为：单手45°持杖指向鼠标，从杖尖放出指挥射线
    /// </summary>
    internal abstract class BaseCommandersStaffHeld : BaseHeldGun
    {
        public override SoundStyle? ShootSound => SoundID.Item68;
        public sealed override void SetGunProperty() {
            Projectile.DamageType = DamageClass.Magic;
            HandFireDistanceX = 0;
            HandFireDistanceY = 0;
            MuzzleForwardOffset = 90;
            GunPressure = 0;
            ControlForce = 0;
            Onehanded = true;
            AlwaysAimPose = true;
        }

        public override void AI() {
            UpdateHeldPose(WantsFireLeft);

            if (CanFire) {
                HoldManaRegenDelay();
            }

            if (WantsFireLeft && FireCooldown <= 0 && PayMana()) {
                SnapToAimPose();
                PlayShootSound();
                CreateFireLight();
                if (Projectile.IsOwnedByLocalPlayer()) {
                    FireRay();
                }
                SetFireCooldown();
            }
            Time++;
        }

        /// <summary>
        /// 放出指挥射线，仅在弹幕主人端调用
        /// </summary>
        public abstract void FireRay();

        //法杖持握绘制：原点设在握把端，旋转角附加45°，让杖体从手中向外延伸
        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            float rot = DirSign > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4;
            float offsetRot = DrawGunBodyRotOffset * (DirSign > 0 ? 1 : -1);
            Vector2 orig = DirSign > 0 ? new Vector2(0, TextureValue.Height) : new Vector2(0, 0);
            Main.EntitySpriteDraw(TextureValue, drawPos, null, lightColor
                , Projectile.rotation + offsetRot + rot, orig, Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }

    internal class CommandersStaffHeld : BaseCommandersStaffHeld
    {
        public override string Texture => CWRConstant.Item_Magic + "CommandersStaffHeld";
        public override int TargetID => ModContent.ItemType<CommandersStaff>();
        public override void FireRay() {
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                , ModContent.ProjectileType<CommandersRay>()
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, Projectile.identity);
        }
    }

    internal class CommandersStaffEXHeld : BaseCommandersStaffHeld
    {
        public override string Texture => CWRConstant.Item_Magic + "CommandersStaffEXHeld";
        public override int TargetID => ModContent.ItemType<CommandersStaffEX>();
        public override void FireRay() {
            for (int i = 0; i < 5; i++) {
                Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                    , ModContent.ProjectileType<CommandersRay>()
                    , WeaponDamage, WeaponKnockback, Owner.whoAmI
                    , ai0: Projectile.identity, ai1: (-2 + i) * 0.01f, ai2: 1);
            }
        }
    }

    internal class CommandersRay : ModProjectile
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
            Projectile.DamageType = DamageClass.Magic;
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
            if (Projectile.ai[2] != 0) {
                Projectile.usesLocalNPCImmunity = true;
                Projectile.localNPCHitCooldown = 10;
                Projectile.ai[1] *= 1.06f;
            }

            homeProj = Main.projectile.FindByIdentity((int)Projectile.ai[0]);
            if (homeProj.Alives() && homeProj.type > ProjectileID.None) {
                Projectile.Center = homeProj.Center;
                Projectile.rotation = homeProj.rotation + Projectile.ai[1];
            }
            else {
                Projectile.Kill();
            }

            if (!VaultUtils.isServer) {
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
                        PRTLoader.NewParticle<PRT_HeavenfallStar>(targetPos, VaultUtils.RandVr(6), color, Main.rand.NextFloat(0.6f, 1.6f)).Configure(false, 2);
                    }
                    else if (toTileLeng > 90) {
                        PRTLoader.NewParticle<PRT_HeavenfallStarAlpha>(targetPos, unitVer, color, Main.rand.NextFloat(0.2f, 0.4f) * scaleTimer * 0.2f).Configure(false, 2);
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
