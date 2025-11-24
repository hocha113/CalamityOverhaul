using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RDragonRage : CWRItemOverride
    {
        public static readonly SoundStyle SwingSound = new("CalamityMod/Sounds/Custom/SCalSounds/CatastropheResonanceSlash1");
        private int Level;
        private int LevelAlt;
        internal static bool CoolWorld => Main.zenithWorld || Main.getGoodWorld || Main.drunkWorld || Main.worldName == "HoCha113";
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return ShootFunc(ref Level, ref LevelAlt, item, player, source, position, velocity, type, damage, knockback);
        }

        public override bool? AltFunctionUse(Item item, Player player) => true;

        public override bool? UseItem(Item item, Player player) => player.ownedProjectileCounts[item.shoot] == 0;

        public static void SetDefaultsFunc(Item Item) {
            Item.width = 74;
            Item.height = 74;
            Item.value = Item.sellPrice(gold: 75);
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useAnimation = 32;
            Item.useTime = 32;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.damage = 1840;
            Item.crit = 16;
            Item.knockBack = 7.5f;
            Item.noUseGraphic = true;
            Item.DamageType = CWRRef.GetTrueMeleeDamageClass();
            Item.noMelee = true;
            Item.channel = true;
            Item.shootSpeed = 10f;
            Item.shoot = ModContent.ProjectileType<DragonRageHeld>();
            ItemMeleePrefixDic[Item.type] = true;
            ItemRangedPrefixDic[Item.type] = false;
        }

        internal static bool ShootFunc(ref int Level, ref int LevelAlt, Item Item, Player player
            , EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                SoundEngine.PlaySound(new("CalamityMod/Sounds/Custom/MeatySlash"), player.Center);
                SoundEngine.PlaySound(SwingSound with { MaxInstances = 6, Volume = 1.06f }, position);
                Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, 6);
                LevelAlt = 0;
                return false;
            }
            if (!Main.dedServ) {
                SoundStyle sound = SwingSound with { MaxInstances = 6, Volume = 0.6f, Pitch = -0.2f };
                if (Level == 3) {
                    sound = SwingSound with { MaxInstances = 6, Volume = 0.6f, Pitch = -0.1f };
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
    }
}
