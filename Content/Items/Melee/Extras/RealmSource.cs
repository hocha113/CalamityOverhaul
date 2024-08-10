using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Items.Materials;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Extras
{
    internal class RealmSource : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "RealmSource";
        public override void SetDefaults() {
            Item.width = Item.height = 54;
            Item.shootSpeed = 9;
            Item.crit = 8;
            Item.damage = 485;
            Item.useTime = 16;
            Item.useAnimation = 16;
            Item.knockBack = 6;
            Item.value = Item.buyPrice(0, 83, 55, 0);
            Item.rare = ItemRarityID.Lime;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item1;
            Item.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            Item.CWR().isHeldItem = true;
            Item.SetKnifeHeld<RealmSourceHeld>();
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient(ItemID.LunarBar, 8).
                AddIngredient<Lumenyl>(6).
                AddTile(TileID.LunarCraftingStation).
                Register();
        }
    }

    internal class RealmSourceHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<RealmSource>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "ExaltedOathblade_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 116;
            canDrawSlashTrail = true;
            drawTrailCount = 8;
            distanceToOwner = 80;
            drawTrailTopWidth = 60;
            ownerOrientationLock = true;
            SwingData.starArg = 66;
            SwingData.baseSwingSpeed = 4.25f;
            IgnoreImpactBoxSize = true;
            Length = 80;
        }

        public override void MeleeEffect() {
            Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height
                , DustID.GemDiamond, 0, 0, 0, Main.DiscoColor);
            dust.noGravity = true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<Nightwither>(), 180);
            target.AddBuff(ModContent.BuffType<ElementalMix>(), 180);
            target.AddBuff(ModContent.BuffType<RiptideDebuff>(), 180);
            for (int i = 0; i < 66; i++) {
                Dust dust = Dust.NewDustDirect(target.position, target.width, target.height
                , DustID.GemDiamond, 0, 0, 0, Main.DiscoColor);
                dust.noGravity = true;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<Nightwither>(), 180);
            target.AddBuff(ModContent.BuffType<ElementalMix>(), 180);
            target.AddBuff(ModContent.BuffType<RiptideDebuff>(), 180);
            Dust.NewDust(target.position, target.width, target.height
                , DustID.GemDiamond, 0, 0, 0, Main.DiscoColor);
        }
    }
}
