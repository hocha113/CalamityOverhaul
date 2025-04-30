using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Enums;
using Terraria.Graphics.Effects;
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
            Item.UseSound = SoundID.Item68;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.buyPrice(0, 1, 60, 10);
            Item.SetHeldProj<CommandersStaffHeld>();
            Item.CWR().DeathModeItem = true;
        }
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
            Item.UseSound = SoundID.Item68;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.buyPrice(0, 8, 60, 10);
            Item.SetHeldProj<CommandersStaffEXHeld>();
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<CommandersStaff>().
                AddIngredient<SoulofFrightEX>().
                AddTile(TileID.LunarCraftingStation).
                Register();
        }
    }

    internal class CommandersStaffHeld : BaseMagicStaff<CommandersStaff>
    {
        public override string Texture => CWRConstant.Item_Magic + "CommandersStaffHeld";
        public override void PostSetRangedProperty() => ShootPosToMouLengValue = 90;
        public override void FiringShoot() {
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity, AmmoTypes
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, Projectile.identity);
        }
    }

    internal class CommandersStaffEXHeld : BaseMagicStaff<CommandersStaffEX>
    {
        public override string Texture => CWRConstant.Item_Magic + "CommandersStaffEXHeld";
        public override void PostSetRangedProperty() => ShootPosToMouLengValue = 90;
        public override void FiringShoot() {
            for (int i = 0; i < 5; i++) {
                int proj = Projectile.NewProjectile(Source, ShootPos, ShootVelocity, AmmoTypes
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, ai0: Projectile.identity, ai1: (-2 + i) * 0.01f, ai2: 1);
                Main.projectile[proj].netUpdate = true;
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

            homeProj = CWRUtils.GetProjectileInstance((int)Projectile.ai[0]);
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
                        PRTLoader.AddParticle(new PRT_HeavenfallStar(targetPos, CWRUtils.randVr(6), false, 2, Main.rand.NextFloat(0.6f, 1.6f), color));
                    }
                    else if (toTileLeng > 90) {
                        PRTLoader.AddParticle(new PRT_HeavenfallStarAlpha(targetPos, unitVer, false, 2, Main.rand.NextFloat(0.2f, 0.4f) * scaleTimer * 0.2f, color));
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
