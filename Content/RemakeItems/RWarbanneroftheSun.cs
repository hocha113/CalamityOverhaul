using CalamityMod;
using CalamityMod.CalPlayer;
using CalamityMod.Items.Accessories;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.LegendWeapon.MurasamaLegend;
using CalamityOverhaul.Content.RemakeItems.Core;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems
{
    internal class RWarbanneroftheSun : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<WarbanneroftheSun>();
        public override bool DrawingInfo => false;
        public override bool FormulaSubstitution => false;
        public override bool On_UpdateAccessory(Item item, Player player, bool hideVisual) {
            Item item2 = player.GetItem();
            if (item2.IsAir) {
                return true;
            }
            if (item2.type == ModContent.ItemType<Murasama>() || item2.type == ModContent.ItemType<MurasamaEcType>()) {
                CalamityPlayer modPlayer = player.Calamity();
                modPlayer.warbannerOfTheSun = true;
                float bonus = 0f;
                int closestNPC = -1;
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC nPC = Main.npc[i];
                    if (nPC.IsAnEnemy() && !nPC.dontTakeDamage) {
                        closestNPC = i;
                        break;
                    }
                }
                float distance = -1f;
                for (int j = 0; j < Main.maxNPCs; j++) {
                    NPC nPC = Main.npc[j];
                    if (nPC.IsAnEnemy() && !nPC.dontTakeDamage) {
                        float distance2 = Math.Abs(nPC.position.X + nPC.width / 2 - (player.position.X + player.width / 2)) + Math.Abs(nPC.position.Y + nPC.height / 2 - (player.position.Y + player.height / 2));
                        if (distance == -1f || distance2 < distance) {
                            distance = distance2;
                            closestNPC = j;
                        }
                    }
                }
                if (closestNPC != -1) {
                    NPC actualClosestNPC = Main.npc[closestNPC];

                    float generousHitboxWidth = Math.Max(actualClosestNPC.Hitbox.Width / 2f, actualClosestNPC.Hitbox.Height / 2f);
                    float hitboxEdgeDist = actualClosestNPC.Distance(player.Center) - generousHitboxWidth;

                    if (hitboxEdgeDist < 0)
                        hitboxEdgeDist = 0;

                    if (hitboxEdgeDist < 480f) {
                        bonus = MathHelper.Lerp(0f, 0.2f, 1f - (hitboxEdgeDist / 480f));

                        if (bonus > 0.2f)
                            bonus = 0.2f;
                    }
                }

                bonus /= 4f;

                player.GetAttackSpeed<MeleeDamageClass>() += bonus;
                player.GetDamage<MeleeDamageClass>() += bonus;
                player.GetDamage<TrueMeleeDamageClass>() += bonus;

                return false;
            }

            return base.On_UpdateAccessory(item, player, hideVisual);
        }
    }
}
