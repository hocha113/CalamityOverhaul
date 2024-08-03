using CalamityMod;
using CalamityOverhaul.Content.Items.Magic.Extras;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.Others
{
    internal class MarbleElemental : ModNPC
    {
        public override string Texture => CWRConstant.NPC + "MarbleElemental";
        public ref float FallDelay => ref NPC.ai[0];
        public ref float Time => ref NPC.ai[1];
        public ref float Speed => ref NPC.ai[2];
        public bool Jump {
            get => NPC.ai[3] == 1f;
            set {
                if (CWRUtils.isServer && value != Jump) {
                    NPC.netUpdate = true;
                }
                NPC.ai[3] = value.ToInt();
            }
        }

        public override void SetDefaults() {
            NPC.width = NPC.height = 22;
            NPC.damage = 12;
            NPC.lifeMax = 50;
            NPC.defense = 10;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.friendly = false;
            NPC.HitSound = SoundID.NPCHit7;
            NPC.DeathSound = SoundID.NPCDeath6;
            Main.npcFrameCount[NPC.type] = 12;
        }

        public override void AI() {
            CWRUtils.ClockFrame(ref NPC.frameCounter, 8, 11);
            Time++;

            NPC.TargetClosest(false);

            float maxSpeed = DownedBossSystem.downedPolterghast ? 12.8f : 10.5f;
            if (!Main.player.IndexInRange(NPC.target))
                return;

            Player player = Main.player[NPC.target];

            if (Time % 250f < 180f) {
                if (Jump) {
                    Jump = false;
                }
                if (!NPC.WithinRange(player.Center, 150f)) {
                    NPC.velocity = (NPC.velocity * 17f + NPC.SafeDirectionTo(player.Center, -Vector2.UnitY) * maxSpeed) / 18f;
                    if (FallDelay != 12f) {
                        FallDelay = 12f;
                        NPC.netUpdate = true;
                    }
                }

                Speed = NPC.velocity.X;

                if (Speed == 0f) {
                    Speed = 0.1f;
                }

                if (Math.Abs(Speed) < 7f) {
                    Speed = Math.Abs(Speed) * 7f;
                }
                if (Math.Abs(Speed) > 16f) {
                    Speed = Math.Abs(Speed) * 16f;
                }

                Speed = Math.Abs(Speed) * (player.Center.X - NPC.Center.X > 0).ToDirectionInt();
            }

            else if (Time % 220f > 180f) {
                float verticalAcceleration = DownedBossSystem.downedPolterghast ? 0.07f : 0.05f;
                if (Time % 220f < 200f) {
                    NPC.velocity.Y -= verticalAcceleration;
                }
                else {
                    NPC.velocity.Y += verticalAcceleration;
                }
                if (Time % 220f == 219f) {
                    Jump = true;
                }
            }

            if (Time % 220f > 180f && Jump) {
                Time = 0f;
                NPC.netUpdate = true;
            }

            NPC.velocity = Vector2.Clamp(NPC.velocity, new Vector2(-maxSpeed), new Vector2(maxSpeed));
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(DropHelper.PerPlayer(ItemID.Marble, denominator: 5, minQuantity: 3, maxQuantity: 5));
            npcLoot.Add(DropHelper.PerPlayer(ModContent.ItemType<Palustris>(), denominator: 55, minQuantity: 1, maxQuantity: 1));
            npcLoot.Add(DropHelper.PerPlayer(ModContent.ItemType<MarbleRifle>(), denominator: 55, minQuantity: 1, maxQuantity: 1));
        }

        public override float SpawnChance(NPCSpawnInfo spawnInfo) {
            return spawnInfo.Player.ZoneMarble ? Main.worldName == "ForByMarble" ? 10 : 0.5f : 0;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D mainValue = TextureAssets.Npc[Type].Value;
            Main.EntitySpriteDraw(mainValue, NPC.Center - Main.screenPosition, CWRUtils.GetRec(mainValue, (int)NPC.frameCounter, 12)
                , drawColor, NPC.rotation, CWRUtils.GetOrig(mainValue, 12), NPC.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
