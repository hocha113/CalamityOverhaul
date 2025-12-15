using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems
{
    internal class RWarbanneroftheSun : CWRItemOverride
    {
        public override int TargetID => CWRID.Item_WarbanneroftheSun;
        public override bool DrawingInfo => false;

        public override bool On_UpdateAccessory(Item item, Player player, bool hideVisual) {
            Item item2 = player.GetItem();
            if (item2.IsAir) {
                return true;
            }
            if (item2.type == CWRID.Item_Murasama) {
                player.SetPlayerWarbannerOfTheSun(true);
                float bonus = 0f;
                int closestNPC = -1;
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC nPC = Main.npc[i];
                    if (nPC.GetNPCIsAnEnemy() && !nPC.dontTakeDamage) {
                        closestNPC = i;
                        break;
                    }
                }
                float distance = -1f;
                for (int j = 0; j < Main.maxNPCs; j++) {
                    NPC nPC = Main.npc[j];
                    if (nPC.GetNPCIsAnEnemy() && !nPC.dontTakeDamage) {
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

                bonus /= 2f;

                player.GetAttackSpeed<MeleeDamageClass>() += bonus;
                player.GetDamage<MeleeDamageClass>() += bonus;
                player.GetDamage(CWRRef.GetTrueMeleeDamageClass()) += bonus;

                return false;
            }

            return base.On_UpdateAccessory(item, player, hideVisual);
        }
    }
}
