using CalamityMod;
using CalamityMod.NPCs.SupremeCalamitas;
using CalamityMod.Rarities;
using CalamityMod.Sounds;
using CalamityOverhaul.Content.LegendWeapon.MurasamaLegend;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
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
        internal static bool coolWorld => Main.zenithWorld || Main.getGoodWorld || Main.drunkWorld || Main.worldName == "HoCha113";
        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }
        public override void SetDefaults() => SetDefaultsFunc(Item);
        public static void SetDefaultsFunc(Item Item) {
            Item.width = 74;
            Item.height = 74;
            Item.value = Item.sellPrice(gold: 75);
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useAnimation = 32;
            Item.useTime = 32;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.damage = 920;
            Item.crit = 16;
            Item.knockBack = 7.5f;
            Item.noUseGraphic = true;
            Item.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            Item.noMelee = true;
            Item.channel = true;
            Item.shootSpeed = 10f;
            Item.shoot = ModContent.ProjectileType<DragonRageHeld>();
            Item.rare = ModContent.RarityType<Violet>();
            Item.CWR().GetMeleePrefix = true;
        }

        internal static bool ShootFunc(ref int Level, ref int LevelAlt, Item Item, Player player
            , EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                if (LevelAlt < 2) {
                    SoundEngine.PlaySound(SupremeCalamitas.CatastropheSwing with { MaxInstances = 6, Volume = 0.6f }, position);
                    int newLevel = 4 + LevelAlt;
                    int newDmg = damage;
                    if (newLevel == 6 && coolWorld) {
                        newDmg = (int)(damage * 0.6f);
                    }
                    Projectile.NewProjectile(source, position, velocity, type, newDmg, knockback, player.whoAmI, newLevel);
                    LevelAlt++;
                    return false;
                }
                SoundEngine.PlaySound(in CommonCalamitySounds.MeatySlashSound, player.Center);
                SoundEngine.PlaySound(SupremeCalamitas.CatastropheSwing with { MaxInstances = 6, Volume = 1.06f }, position);
                Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, 6);
                LevelAlt = 0;
                return false;
            }
            if (!Main.dedServ) {
                SoundStyle sound = MurasamaOverride.BigSwing with { Pitch = (0.3f + Level * 0.25f) };
                if (Level == 3) {
                    sound = SoundID.Item71 with { Volume = 1.5f, Pitch = 0.75f };
                }
                SoundEngine.PlaySound(sound, player.position);
            }
            int newdmg = damage;
            if (Level == 1) {
                newdmg = (int)(damage * 1.15f);
            }
            else if (Level == 2) {
                newdmg = (int)(damage * 1.25f);
            }
            else if (Level == 3) {
                newdmg = (int)(damage * 1.55f);
            }
            Projectile.NewProjectile(source, position, velocity, type, newdmg, knockback, player.whoAmI, Level);
            if (++Level > 3) {
                Level = 0;
            }
            LevelAlt = 0;
            return false;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return ShootFunc(ref Level, ref LevelAlt, Item, player, source, position, velocity, type, damage, knockback);
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 0;
    }
}
