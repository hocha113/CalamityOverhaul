using CalamityOverhaul.Common;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Neutrons;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class NeutronGlaive : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "NeutronGlaive";
        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 16));
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
            Item.SetKnifeHeld<NeutronGlaiveHeld>(true);
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems_NeutronGlaive;
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
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<NeutronGlaiveHeldAlt>(), damage, knockback, player.whoAmI);
                return false;
            }
            return base.Shoot(player, source, position, velocity, type, damage, knockback);
        }
    }

    internal class NeutronGlaiveHeld : BaseKnife, IWarpDrawable
    {
        public override int TargetID => ModContent.ItemType<NeutronGlaive>();
        public override void SetKnifeProperty() {
            ShootSpeed = 18;
            AnimationMaxFrme = 16;
            Projectile.width = Projectile.height = 66;
            canDrawSlashTrail = true;
            distanceToOwner = 20;
            drawTrailBtommWidth = 50;
            drawTrailTopWidth = 20;
            drawTrailCount = 16;
            Length = 120;
            Projectile.scale = 1.25f;
            SwingData.starArg = 80;
            SwingData.baseSwingSpeed = 5.4f;
            SwingData.ler1_UpLengthSengs = 0.1f;
            SwingData.minClampLength = 120;
            SwingData.maxClampLength = 130;
            SwingData.ler1_UpSizeSengs = 0.056f;
        }

        public override void Shoot() {
            int type = ModContent.ProjectileType<NeutronGlaiveBeam>();
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                , type, Projectile.damage, Projectile.knockBack, Projectile.owner);
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
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

        bool IWarpDrawable.CanDrawCustom() => true;

        bool IWarpDrawable.DontUseBlueshiftEffect() => true;

        void IWarpDrawable.Warp() => WarpDraw();

        void IWarpDrawable.DrawCustom(SpriteBatch spriteBatch) {
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
                , color, drawRoting, drawOrigin, Projectile.scale * MeleeSize, effects, 0);
        }

        public override void DrawSwing(SpriteBatch spriteBatch, Color lightColor) { }
    }
}
