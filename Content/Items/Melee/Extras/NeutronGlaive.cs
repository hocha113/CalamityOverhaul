using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Projectiles.Weapons;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Neutrons;
using CalamityOverhaul.Content.Tiles;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Extras
{
    internal class NeutronGlaive : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "NeutronGlaive";
        public override bool IsLoadingEnabled(Mod mod) {
            return !CWRServerConfig.Instance.AddExtrasContent ? false : base.IsLoadingEnabled(mod);
        }

        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 6));
        }

        public override void SetDefaults() {
            Item.height = 154;
            Item.width = 154;
            Item.damage = 855;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 13;
            Item.scale = 1;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.knockBack = 7.5f;
            Item.UseSound = SoundID.Item60;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(13, 53, 75, 0);
            Item.rare = ItemRarityID.Red;
            Item.crit = 8;
            Item.shoot = ModContent.ProjectileType<NeutronGlaiveBeam>();
            Item.shootSpeed = 18f;
            Item.SetKnifeHeld<NeutronGlaiveHeld>();
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems17;
        }

        public override bool CanUseItem(Player player) {
            Item.UseSound = SoundID.Item60;
            if (player.altFunctionUse == 2) {
                Item.UseSound = SoundID.AbigailAttack;
            }
            return player.ownedProjectileCounts[ModContent.ProjectileType<NeutronGlaiveHeldAlt>()] == 0;
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<NeutronGlaiveHeldAlt>(), damage * 5, knockback, player.whoAmI);
                return false;
            }
            return base.Shoot(player, source, position, velocity, type, damage, knockback);
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<BlackMatterStick>(23)
                .AddConsumeItemCallback((Recipe recipe, int type, ref int amount) => {
                    amount = 0;
                })
                .AddOnCraftCallback(CWRRecipes.SpawnAction)
                .AddTile(ModContent.TileType<TransmutationOfMatter>())
                .Register();
        }
    }

    internal class NeutronGlaiveHeld : BaseKnife, IDrawWarp
    {
        public override int TargetID => ModContent.ItemType<NeutronGlaive>();
        public override void SetKnifeProperty() {
            ShootSpeed = 18;
            AnimationMaxFrme = 6;
            Projectile.width = Projectile.height = 66;
            canDrawSlashTrail = true;
            distanceToOwner = 40;
            drawTrailBtommWidth = 70;
            drawTrailTopWidth = 20;
            drawTrailCount = 16;
            Length = 130;
            overOffsetCachesRoting = MathHelper.ToRadians(2);
            Projectile.scale = 1.25f;
            SwingData.starArg = 80;
            SwingData.baseSwingSpeed = 5.4f;
            SwingData.ler1_UpLengthSengs = 0.1f;
            SwingData.minClampLength = 130;
            SwingData.maxClampLength = 140;
            SwingData.ler1_UpSizeSengs = 0.056f;
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                , ModContent.ProjectileType<NeutronGlaiveBeam>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits == 0) {
                Projectile.NewProjectile(Source, target.Center, Vector2.Zero
                , ModContent.ProjectileType<NeutronExplode>(), Projectile.damage / 2, 0);
            }
            
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (Projectile.numHits == 0) {
                Projectile.NewProjectile(Source, target.Center, Vector2.Zero
                , ModContent.ProjectileType<NeutronExplode>(), Projectile.damage / 2, 0);
            }
                
        }

        bool IDrawWarp.canDraw() => true;

        bool IDrawWarp.noBlueshift() => true;

        void IDrawWarp.Warp() => WarpDraw();

        void IDrawWarp.costomDraw(SpriteBatch spriteBatch) {
            Texture2D texture = TextureValue;
            Rectangle rect = CWRUtils.GetRec(texture, Projectile.frame, AnimationMaxFrme);
            Vector2 drawOrigin = rect.Size() / 2;
            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            Vector2 offsetOwnerPos = safeInSwingUnit.GetNormalVector() * unitOffsetDrawZkMode * Projectile.spriteDirection;
            float drawRoting = Projectile.rotation;
            if (Projectile.spriteDirection == -1) {
                drawRoting += MathHelper.Pi;
            }

            Vector2 drawPosValue = Projectile.Center - RodingToVer(toProjCoreMode, (Projectile.Center - Owner.Center).ToRotation()) + offsetOwnerPos;
            Color color = Color.White;

            Vector2 trueDrawPos = drawPosValue - Main.screenPosition + Vector2.UnitY * Projectile.gfxOffY;

            Main.EntitySpriteDraw(texture, trueDrawPos, new Rectangle?(rect)
                , color, drawRoting, drawOrigin, Projectile.scale, effects, 0);
        }

        public override void DrawSwing(SpriteBatch spriteBatch, Color lightColor) { }
    }
}
