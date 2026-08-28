using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BloomsandSerpents;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Summon.BloomCallers
{
    /// <summary>
    /// 唤蕾号，荒花号角。吹响后唤来荒花幼蕾并肩作战：幼蕾投掷垂刺，
    /// 每第三次攻击改为冲身撞击并自旋绽放花瓣。行为在 <see cref="BloomCallerSprout"/>
    /// </summary>
    internal class BloomCaller : BssModItem
    {
        public override string Texture => CWRConstant.Item_Summon + "BloomCaller";

        public override void SetDefaults() {
            Item.width = 42;
            Item.height = 44;
            Item.damage = 13;
            Item.mana = 10;
            Item.knockBack = 3f;
            Item.useTime = Item.useAnimation = 32;
            Item.useStyle = ItemUseStyleID.HoldUp;
            //吹号与沙蟒同声线，音高抬起：小号角在回应大蟒
            Item.UseSound = CWRSound.SendRoar with { Pitch = 0.5f, Volume = 0.45f };
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<BloomCallerSprout>();
            Item.shootSpeed = 5f;
            Item.buffType = ModContent.BuffType<BloomCallerBuff>();
            Item.DamageType = DamageClass.Summon;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(0, 1, 20);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            player.AddBuff(Item.buffType, 2);
            Vector2 spawn = player.Center + new Vector2(player.direction * 14f, -26f);
            Projectile.NewProjectile(source, spawn, new Vector2(0f, -2.2f)
                , type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    /// <summary>荒花幼蕾在场的召唤增益</summary>
    internal class BloomCallerBuff : ModBuff
    {
        public override string Texture => CWRConstant.Buff + "BloomCallerBuff";

        public override bool IsLoadingEnabled(Mod mod) => BssGate.Enabled;

        public override void SetStaticDefaults() {
            Main.buffNoTimeDisplay[Type] = true;
            Main.buffNoSave[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<BloomCallerSprout>()] > 0) {
                player.buffTime[buffIndex] = 18000;
            }
            else {
                player.DelBuff(buffIndex);
                buffIndex--;
            }
        }
    }
}
