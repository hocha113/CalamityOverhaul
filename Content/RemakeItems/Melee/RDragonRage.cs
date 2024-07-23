using CalamityMod;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.NPCs.SupremeCalamitas;
using CalamityMod.Rarities;
using CalamityMod.Sounds;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RDragonRage : BaseRItem
    {
        private int Level;
        private int LevelAlt;
        public override int TargetID => ModContent.ItemType<DragonRage>();
        public override int ProtogenesisID => ModContent.ItemType<DragonRageEcType>();
        public override string TargetToolTipItemName => "DragonRageEcType";
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[ModContent.ItemType<DragonRage>()] = true;
        public override void SetDefaults(Item item) {
            item.width = 74;
            item.height = 74;
            item.value = Item.sellPrice(gold: 75);
            item.useStyle = ItemUseStyleID.Shoot;
            item.useAnimation = 32;
            item.useTime = 32;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.damage = 950;
            item.crit = 16;
            item.knockBack = 7.5f;
            item.noUseGraphic = true;
            item.DamageType = ModContent.GetInstance<TrueMeleeNoSpeedDamageClass>();
            item.noMelee = true;
            item.channel = true;
            item.shootSpeed = 10f;
            item.shoot = ModContent.ProjectileType<DragonRageHeld>();
            item.rare = ModContent.RarityType<Violet>();
            Level = 0;
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                if (LevelAlt < 2) {
                    SoundEngine.PlaySound(SupremeCalamitas.CatastropheSwing with { MaxInstances = 6, Volume = 0.6f }, position);
                    Projectile.NewProjectile(source, position, velocity, item.shoot, damage, knockback, player.whoAmI, 4 + LevelAlt);
                    LevelAlt++;
                    return false;
                }
                SoundEngine.PlaySound(in CommonCalamitySounds.MeatySlashSound, player.Center);
                SoundEngine.PlaySound(SupremeCalamitas.CatastropheSwing with { MaxInstances = 6, Volume = 1.06f }, position);
                Projectile.NewProjectile(source, position, velocity, item.shoot, damage, knockback, player.whoAmI, 6);
                LevelAlt = 0;
                return false;
            }
            if (!Main.dedServ) {
                SoundStyle sound = MurasamaEcType.BigSwing with { Pitch = (0.3f + Level * 0.25f) };
                if (Level == 3) {
                    sound = SoundID.Item71 with { Volume = 1.5f, Pitch = 0.75f };
                }
                SoundEngine.PlaySound(sound, player.position);
            }
            int newdmg = damage;
            if (Level == 2) {
                newdmg = (int)(damage * 1.25f);
            }
            else if (Level == 3) {
                newdmg = (int)(damage * 2.05f);
            }
            Projectile.NewProjectile(source, position, velocity, item.shoot, newdmg, knockback, player.whoAmI, Level);
            if (++Level > 3) {
                Level = 0;
            }
            LevelAlt = 0;
            return false;
        }

        public override bool? AltFunctionUse(Item item, Player player) => true;

        public override bool? UseItem(Item item, Player player) => player.ownedProjectileCounts[item.shoot] == 0;
    }
}
