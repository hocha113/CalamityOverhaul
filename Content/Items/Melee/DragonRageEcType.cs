using CalamityMod.NPCs.SupremeCalamitas;
using CalamityMod.Sounds;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 巨龙之怒
    /// </summary>
    internal class DragonRageEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "DragonRage";
        private int Level;
        private int LevelAlt;
        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = 74;
            Item.height = 74;
            Item.rare = ItemRarityID.Purple;
            Item.value = Item.sellPrice(gold: 75);
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useAnimation = 32;
            Item.useTime = 32;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.damage = 950;
            Item.crit = 16;
            Item.knockBack = 7.5f;
            Item.noUseGraphic = true;
            Item.DamageType = DamageClass.Melee;
            Item.noMelee = true;
            Item.channel = true;
            Item.shootSpeed = 10f;
            Item.shoot = ModContent.ProjectileType<DragonRageHeld>();
            Level = 0;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                if (LevelAlt < 2) {
                    SoundEngine.PlaySound(SupremeCalamitas.CatastropheSwing with { MaxInstances = 6, Volume = 0.6f }, position);
                    Projectile.NewProjectile(source, position, velocity, Item.shoot, damage, knockback, player.whoAmI, 4 + LevelAlt);
                    LevelAlt++;
                    return false;
                }
                SoundEngine.PlaySound(in CommonCalamitySounds.MeatySlashSound, player.Center);
                SoundEngine.PlaySound(SupremeCalamitas.CatastropheSwing with { MaxInstances = 6, Volume = 1.06f }, position);
                Projectile.NewProjectile(source, position, velocity, Item.shoot, damage, knockback, player.whoAmI, 6);
                LevelAlt = 0;
                return false;
            }
            if (!Main.dedServ) {
                SoundStyle sound = MurasamaEcType.BigSwing with { Pitch = (0.3f + Level * 0.25f)};
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
            Projectile.NewProjectile(source, position, velocity, Item.shoot, newdmg, knockback, player.whoAmI, Level);
            if (++Level > 3) {
                Level = 0;
            }
            LevelAlt = 0;
            return false;
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 0;
    }
}
