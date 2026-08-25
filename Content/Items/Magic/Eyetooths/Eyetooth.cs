using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Eyetooths
{
    /// <summary>泣血瞳牙，克眼掉落的法师牙镖杖</summary>
    internal class Eyetooth : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "Eyetooth";

        public override void SetStaticDefaults() => Item.staff[Type] = true;

        public override void SetDefaults() {
            Item.width = 38;
            Item.height = 60;
            Item.damage = 10;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 6;
            Item.useTime = Item.useAnimation = 16;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 1.6f;
            Item.UseSound = SoundID.Item17 with { Volume = 0.6f, Pitch = 0.25f };
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<EyetoothDart>();
            Item.shootSpeed = 7.5f;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(0, 1, 20);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 dir = velocity.SafeNormalize(Vector2.UnitX);
            Vector2 muzzle = position + dir * 26f;
            if (!Collision.CanHitLine(position, 4, 4, muzzle, 4, 4)) {
                muzzle = position;
            }
            Projectile.NewProjectile(source, muzzle, velocity, type, damage, knockback
                , player.whoAmI, 0f, -1f);
            EyetoothVFX.LaunchSpit(muzzle, velocity);
            return false;
        }
    }

    /// <summary>
    /// 牙创流血，DoT 走 <see cref="Content.CWRNpc.EyetoothBleed"/> 标志 → UpdateLifeRegen，
    /// 这里只置标志与限频渗血
    /// </summary>
    internal class EyetoothWound : ModBuff
    {
        public override string Texture => CWRConstant.Buff + "EyetoothWound";
        private int time;

        public override void SetStaticDefaults() => Main.debuff[Type] = true;

        public override void Update(NPC npc, ref int buffIndex) {
            npc.CWR().EyetoothBleed = true;
            if (++time % 8 == 0) {
                EyetoothVFX.WoundDrip(npc);
            }
        }
    }
}
