using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RTerrorBlade : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.TerrorBlade>();
        public override int ProtogenesisID => ModContent.ItemType<TerrorBladeEcType>();
        public override string TargetToolTipItemName => "TerrorBladeEcType";
        public override void SetDefaults(Item item) {
            item.width = 88;
            item.damage = 560;
            item.DamageType = DamageClass.Melee;
            item.useAnimation = 20;
            item.useTime = 20;
            item.useTurn = true;
            item.useStyle = ItemUseStyleID.Swing;
            item.knockBack = 8.5f;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.height = 80;
            item.shoot = ModContent.ProjectileType<RTerrorBeam>();
            item.shootSpeed = 20f;
            item.value = CalamityGlobalItem.RarityPureGreenBuyPrice;
            item.rare = ModContent.RarityType<PureGreen>();
            item.CWR().heldProjType = ModContent.ProjectileType<TerrorBladeHeld>();
        }

        public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage) {
            item.initialize();
            damage *= item.CWR().ai[0] == 1 ? 1.25f : 1;
        }

        public override void ModifyWeaponKnockback(Item item, Player player, ref StatModifier knockback) {
            item.initialize();
            knockback *= item.CWR().ai[0] == 1 ? 1.25f : 1;
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            bool shootBool = false;
            if (!item.CWR().closeCombat) {
                bool olduseup = item.CWR().MeleeCharge > 0;//这里使用到了效差的流程思想，用于判断能量耗尽的那一刻            
                if (item.CWR().MeleeCharge > 0) {
                    item.CWR().MeleeCharge -= damage / 10;
                    Projectile.NewProjectileDirect(source, player.GetPlayerStabilityCenter(), velocity, type, damage, knockback, player.whoAmI, 1);
                    shootBool = false;
                }
                else {
                    shootBool = true;
                }
                bool useup = item.CWR().MeleeCharge > 0;
                if (useup != olduseup) {
                    SoundEngine.PlaySound(CWRSound.Peuncharge with { Volume = 0.4f }, player.Center);
                }
            }

            item.CWR().closeCombat = false;
            return shootBool;
        }

        public override void OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            item.CWR().closeCombat = true;
            target.AddBuff(ModContent.BuffType<SoulBurning>(), 600);

            bool oldcharge = item.CWR().MeleeCharge > 0;//与OnHitPvp一致，用于判断能量出现的那一刻
            item.CWR().MeleeCharge += hit.Damage / 5;
            bool charge = item.CWR().MeleeCharge > 0;
            if (charge != oldcharge) {
                SoundEngine.PlaySound(CWRSound.Pecharge with { Volume = 0.4f }, player.Center);
            }
        }
    }
}
