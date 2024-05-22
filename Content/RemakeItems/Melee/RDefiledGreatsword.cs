using CalamityMod.Buffs.StatBuffs;
using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RDefiledGreatsword : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.DefiledGreatsword>();
        public override int ProtogenesisID => ModContent.ItemType<DefiledGreatswordEcType>();
        public override string TargetToolTipItemName => "DefiledGreatswordEcType";
        public override void SetDefaults(Item item) {
            item.width = 102;
            item.damage = 112;
            item.DamageType = DamageClass.Melee;
            item.useAnimation = 18;
            item.useStyle = ItemUseStyleID.Swing;
            item.useTime = 18;
            item.useTurn = true;
            item.knockBack = 7.5f;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.height = 102;
            item.shootSpeed = 12f;
            item.value = CalamityGlobalItem.RarityTurquoiseBuyPrice;
            item.rare = ModContent.RarityType<Turquoise>();
            item.shoot = ModContent.ProjectileType<BlazingPhantomBlade>();
            item.CWR().heldProjType = ModContent.ProjectileType<DefiledGreatswordHeld>();
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (item.CWR().MeleeCharge > DefiledGreatswordEcType.DefiledGreatswordMaxRageEnergy) {
                item.CWR().MeleeCharge = DefiledGreatswordEcType.DefiledGreatswordMaxRageEnergy;
            }
            if (!item.CWR().closeCombat) {
                item.CWR().MeleeCharge -= damage * 1.25f;
                if (item.CWR().MeleeCharge < 0) {
                    item.CWR().MeleeCharge = 0;
                }

                if (item.CWR().MeleeCharge == 0) {
                    float adjustedItemScale = player.GetAdjustedItemScale(item);
                    float ai1 = 40;
                    float velocityMultiplier = 2;
                    Projectile.NewProjectile(source, player.MountedCenter, velocity * velocityMultiplier, ModContent.ProjectileType<BlazingPhantomBlade>(), (int)(damage * 0.75)
                        , knockback * 0.5f, player.whoAmI, (float)player.direction * player.gravDir, ai1, adjustedItemScale);
                }
                else {
                    float adjustedItemScale = player.GetAdjustedItemScale(item);
                    for (int i = 0; i < 3; i++) {
                        float ai1 = 40 + i * 8;
                        float velocityMultiplier = 1f - i / (float)3;
                        Projectile.NewProjectile(source, player.MountedCenter, velocity * velocityMultiplier, ModContent.ProjectileType<BlazingPhantomBlade>(), (int)(damage * 0.75)
                            , knockback * 0.5f, player.whoAmI, (float)player.direction * player.gravDir, ai1, adjustedItemScale);
                    }
                }
            }
            item.CWR().closeCombat = false;
            return false;
        }

        public override void OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            item.CWR().closeCombat = true;
            float addnum = hit.Damage;
            if (addnum > target.lifeMax)
                addnum = 0;
            else addnum *= 2;
            item.CWR().MeleeCharge += addnum;

            player.AddBuff(ModContent.BuffType<BrutalCarnage>(), 300);
            target.AddBuff(70, 150);

            if (CWRIDs.WormBodys.Contains(target.type) && !Main.rand.NextBool(3)) {
                return;
            }
            int type = ModContent.ProjectileType<SunlightBlades>();
            int randomLengs = Main.rand.Next(150);
            for (int i = 0; i < 3; i++) {
                Vector2 offsetvr = CWRUtils.GetRandomVevtor(-15, 15, 900 + randomLengs);
                Vector2 spanPos = target.Center + offsetvr;
                int proj1 = Projectile.NewProjectile(
                    CWRUtils.parent(player), spanPos,
                    CWRUtils.UnitVector(offsetvr) * -13,
                    type, item.damage - 50, 0, player.whoAmI);
                Vector2 offsetvr2 = CWRUtils.GetRandomVevtor(165, 195, 900 + randomLengs);
                Vector2 spanPos2 = target.Center + offsetvr2;
                int proj2 = Projectile.NewProjectile(
                    CWRUtils.parent(player), spanPos2,
                    CWRUtils.UnitVector(offsetvr2) * -13, type,
                    item.damage - 50, 0, player.whoAmI);
                Main.projectile[proj1].extraUpdates += 1;
                Main.projectile[proj2].extraUpdates += 1;
            }
        }
    }
}
