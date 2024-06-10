using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
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
    internal class RBlightedCleaver : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.BlightedCleaver>();
        public override int ProtogenesisID => ModContent.ItemType<BlightedCleaverEcType>();
        public override string TargetToolTipItemName => "BlightedCleaverEcType";
        public override void SetDefaults(Item item) {
            item.width = 88;
            item.damage = 60;
            item.DamageType = DamageClass.Melee;
            item.useAnimation = 26;
            item.useStyle = ItemUseStyleID.Swing;
            item.useTime = 26;
            item.useTurn = true;
            item.knockBack = 5.5f;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.height = 88;
            item.value = CalamityGlobalItem.RarityYellowBuyPrice;
            item.rare = ItemRarityID.Yellow;
            item.shoot = ModContent.ProjectileType<BlazingPhantomBlade>();
            item.shootSpeed = 12f;
            item.CWR().heldProjType = ModContent.ProjectileType<DefiledGreatswordHeld>();
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (!item.CWR().closeCombat) {
                if (item.CWR().MeleeCharge > 0) {
                    item.CWR().MeleeCharge -= damage;
                    if (item.CWR().MeleeCharge < 0) {
                        item.CWR().MeleeCharge = 0;
                    }
                    float adjustedItemScale = player.GetAdjustedItemScale(item);
                    Projectile.NewProjectile(source, player.MountedCenter, velocity, type
                        , (int)(damage * 0.75), knockback * 0.5f, player.whoAmI
                        , (float)player.direction * player.gravDir, 32f, adjustedItemScale);
                    NetMessage.SendData(MessageID.PlayerControls, -1, -1, null, player.whoAmI);
                    return false;
                }
            }
            item.CWR().closeCombat = false;
            return false;
        }

        public override void OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            item.CWR().closeCombat = true;
            float addnum = hit.Damage;
            if (addnum > target.lifeMax) {
                addnum = 0;
            }
            else {
                addnum *= 1.5f;
            }

            item.CWR().MeleeCharge += addnum;

            if (CWRLoad.WormBodys.Contains(target.type) && !Main.rand.NextBool(5)) {
                return;
            }

            int type = ModContent.ProjectileType<HyperBlade>();
            for (int i = 0; i < 4; i++) {
                Vector2 offsetvr = CWRUtils.GetRandomVevtor(-127.5f, -52.5f, 360);
                Vector2 spanPos = target.Center + offsetvr;
                int proj = Projectile.NewProjectile(CWRUtils.parent(player), spanPos, CWRUtils.UnitVector(offsetvr) * -15, type, item.damage / 2, 0, player.whoAmI);
                Main.projectile[proj].timeLeft = 50;
                Main.projectile[proj].usesLocalNPCImmunity = true;
                Main.projectile[proj].localNPCHitCooldown = 15;
            }
            target.AddBuff(70, 150);
        }
    }
}