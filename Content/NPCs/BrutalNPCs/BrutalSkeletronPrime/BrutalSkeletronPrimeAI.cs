using CalamityMod;
using CalamityOverhaul.Content.NPCs.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    internal class BrutalSkeletronPrimeAI : NPCSet
    {
        public override int targetID => NPCID.SkeletronPrime;

        private int frame = 0;
        private bool spwanArm;
        private int primeCannon;
        private int primeSaw;
        private int primeVice;
        private int primeLaser;

        public override bool CanLoad() {
            return true;
        }

        public override bool? AI(NPC npc, Mod mod) {
            //npc.TargetClosest();
            //Player player = Main.player[npc.target];
            //if (!player.Alives()) {
            //    npc.ai[0] = 1;
            //}
            //switch (npc.ai[0]) {
            //    case 0:
            //        leisureAI(npc, player);
            //    break;
            //}
            npc.defense = npc.defDefense;
            if (npc.ai[3] != 0f)
                NPC.mechQueen = npc.whoAmI;

            npc.reflectsProjectiles = false;
            if (npc.ai[0] == 0f && Main.netMode != NetmodeID.MultiplayerClient) {
                npc.TargetClosest();
                npc.ai[0] = 1f;
                spanArm(npc);
            }

            if (Main.player[npc.target].dead || Math.Abs(npc.position.X - Main.player[npc.target].position.X) > 6000f || Math.Abs(npc.position.Y - Main.player[npc.target].position.Y) > 6000f) {
                npc.TargetClosest();
                if (Main.player[npc.target].dead || Math.Abs(npc.position.X - Main.player[npc.target].position.X) > 6000f || Math.Abs(npc.position.Y - Main.player[npc.target].position.Y) > 6000f)
                    npc.ai[1] = 3f;
            }

            if (Main.IsItDay() && npc.ai[1] != 3f && npc.ai[1] != 2f) {
                npc.ai[1] = 2f;
                SoundEngine.PlaySound(SoundID.ForceRoar, npc.Center);
            }

            if (npc.ai[1] == 0f) {
                npc.damage = 0;

                npc.ai[2] += 1f;
                if (npc.ai[2] >= (Main.masterMode ? 300f : 600f)) {
                    npc.ai[2] = 0f;
                    npc.ai[1] = 1f;
                    npc.TargetClosest();
                    npc.netUpdate = true;
                }

                if (NPC.IsMechQueenUp)
                    npc.rotation = npc.rotation.AngleLerp(npc.velocity.X / 15f * 0.5f, 0.75f);
                else
                    npc.rotation = npc.velocity.X / 15f;

                float num496 = 0.1f;
                float num497 = 2f;
                float num498 = 0.1f;
                float num499 = 8f;
                float deceleration = Main.masterMode ? 0.94f : Main.expertMode ? 0.96f : 0.98f;
                int num500 = 200;
                int num501 = 500;
                float num502 = 0f;
                int num503 = ((!(Main.player[npc.target].Center.X < npc.Center.X)) ? 1 : (-1));
                if (NPC.IsMechQueenUp) {
                    num502 = -450f * num503;
                    num500 = 300;
                    num501 = 350;
                }

                if (Main.expertMode) {
                    num496 = Main.masterMode ? 0.05f : 0.03f;
                    num497 = Main.masterMode ? 5f : 4f;
                    num498 = Main.masterMode ? 0.15f : 0.12f;
                    num499 = Main.masterMode ? 11f : 9.5f;
                }

                if (npc.position.Y > Main.player[npc.target].position.Y - num500) {
                    if (npc.velocity.Y > 0f)
                        npc.velocity.Y *= deceleration;

                    npc.velocity.Y -= num496;
                    if (npc.velocity.Y > num497)
                        npc.velocity.Y = num497;
                }
                else if (npc.position.Y < Main.player[npc.target].position.Y - num501) {
                    if (npc.velocity.Y < 0f)
                        npc.velocity.Y *= deceleration;

                    npc.velocity.Y += num496;
                    if (npc.velocity.Y < -num497)
                        npc.velocity.Y = -num497;
                }

                if (npc.Center.X > Main.player[npc.target].Center.X + 100f + num502) {
                    if (npc.velocity.X > 0f)
                        npc.velocity.X *= deceleration;

                    npc.velocity.X -= num498;
                    if (npc.velocity.X > num499)
                        npc.velocity.X = num499;
                }

                if (npc.Center.X < Main.player[npc.target].Center.X - 100f + num502) {
                    if (npc.velocity.X < 0f)
                        npc.velocity.X *= deceleration;

                    npc.velocity.X += num498;
                    if (npc.velocity.X < 0f - num499)
                        npc.velocity.X = 0f - num499;
                }
            }
            else if (npc.ai[1] == 1f) {
                npc.defense *= 2;
                npc.damage = npc.defDamage * 2;

                npc.Calamity().CurrentlyIncreasingDefenseOrDR = true;

                npc.ai[2] += 1f;
                if (npc.ai[2] == 2f)
                    SoundEngine.PlaySound(SoundID.ForceRoar, npc.Center);

                if (npc.ai[2] >= (Main.masterMode ? 300f : 400f)) {
                    npc.ai[2] = 0f;
                    npc.ai[1] = 0f;
                }

                if (NPC.IsMechQueenUp)
                    npc.rotation = npc.rotation.AngleLerp(npc.velocity.X / 15f * 0.5f, 0.75f);
                else
                    npc.rotation += npc.direction * 0.3f;

                Vector2 vector54 = npc.Center;
                float num504 = Main.player[npc.target].Center.X - vector54.X;
                float num505 = Main.player[npc.target].Center.Y - vector54.Y;
                float num506 = (float)Math.Sqrt(num504 * num504 + num505 * num505);
                float num507 = 5f;
                if (Main.expertMode) {
                    num507 = Main.masterMode ? 7f : 6f;
                    if (num506 > 150f)
                        num507 *= (Main.masterMode ? 1.075f : 1.05f);

                    float additionalMultiplier = Main.masterMode ? 1.15f : 1.1f;
                    if (num506 > 200f)
                        num507 *= additionalMultiplier;

                    if (num506 > 250f)
                        num507 *= additionalMultiplier;

                    if (num506 > 300f)
                        num507 *= additionalMultiplier;

                    if (num506 > 350f)
                        num507 *= additionalMultiplier;

                    if (num506 > 400f)
                        num507 *= additionalMultiplier;

                    if (num506 > 450f)
                        num507 *= additionalMultiplier;

                    if (num506 > 500f)
                        num507 *= additionalMultiplier;

                    if (num506 > 550f)
                        num507 *= additionalMultiplier;

                    if (num506 > 600f)
                        num507 *= additionalMultiplier;
                }

                if (NPC.IsMechQueenUp) {
                    float num508 = (NPC.npcsFoundForCheckActive[NPCID.TheDestroyerBody] ? 0.6f : 0.75f);
                    num507 *= num508;
                }

                num506 = num507 / num506;
                npc.velocity.X = num504 * num506;
                npc.velocity.Y = num505 * num506;
                if (NPC.IsMechQueenUp) {
                    float num509 = Vector2.Distance(npc.Center, Main.player[npc.target].Center);
                    if (num509 < 0.1f)
                        num509 = 0f;

                    if (num509 < num507)
                        npc.velocity = npc.velocity.SafeNormalize(Vector2.Zero) * num509;
                }
            }
            else if (npc.ai[1] == 2f) {
                npc.damage = 1000;
                npc.defense = 9999;

                npc.Calamity().CurrentlyEnraged = true;
                npc.Calamity().CurrentlyIncreasingDefenseOrDR = true;

                if (NPC.IsMechQueenUp)
                    npc.rotation = npc.rotation.AngleLerp(npc.velocity.X / 15f * 0.5f, 0.75f);
                else
                    npc.rotation += npc.direction * 0.3f;

                Vector2 vector55 = npc.Center;
                float num510 = Main.player[npc.target].Center.X - vector55.X;
                float num511 = Main.player[npc.target].Center.Y - vector55.Y;
                float num512 = (float)Math.Sqrt(num510 * num510 + num511 * num511);
                float num513 = 10f;
                num513 += num512 / 100f;
                if (num513 < 8f)
                    num513 = 8f;

                if (num513 > 32f)
                    num513 = 32f;

                num512 = num513 / num512;
                npc.velocity.X = num510 * num512;
                npc.velocity.Y = num511 * num512;
            }
            else {
                if (npc.ai[1] != 3f)
                    return false;

                if (NPC.IsMechQueenUp) {
                    int num514 = NPC.FindFirstNPC(NPCID.Retinazer);
                    if (num514 >= 0)
                        Main.npc[num514].EncourageDespawn(5);

                    num514 = NPC.FindFirstNPC(NPCID.Spazmatism);
                    if (num514 >= 0)
                        Main.npc[num514].EncourageDespawn(5);

                    if (!NPC.AnyNPCs(NPCID.Retinazer) && !NPC.AnyNPCs(NPCID.Spazmatism)) {
                        num514 = NPC.FindFirstNPC(NPCID.TheDestroyer);
                        if (num514 >= 0)
                            Main.npc[num514].Transform(NPCID.TheDestroyerTail);

                        npc.EncourageDespawn(5);
                    }

                    npc.velocity.Y += 0.1f;
                    if (npc.velocity.Y < 0f)
                        npc.velocity.Y *= 0.95f;

                    npc.velocity.X *= 0.95f;
                    if (npc.velocity.Y > 13f)
                        npc.velocity.Y = 13f;
                }
                else {
                    npc.EncourageDespawn(500);
                    npc.velocity.Y += 0.1f;
                    if (npc.velocity.Y < 0f)
                        npc.velocity.Y *= 0.95f;

                    npc.velocity.X *= 0.95f;
                }
            }

            return false;
        }

        private void leisureAI(NPC npc, Player player) {
            npc.Move(player.Center + new Vector2(0, -450), 12, 0);
            if (!spwanArm) {
                SoundEngine.PlaySound(SoundID.Roar);
                spanArm(npc);
                spwanArm = true;
            }
        }

        private void killArm() {
            Main.npc[primeCannon].active = false;
            Main.npc[primeSaw].active = false;
            Main.npc[primeVice].active = false;
            Main.npc[primeLaser].active = false;
        }

        private void spanArm(NPC npc, int limit = 0) {
            if (limit == 1 || limit == 0) {
                primeCannon = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, NPCID.PrimeCannon, npc.whoAmI);
                Main.npc[primeCannon].ai[0] = -1f;
                Main.npc[primeCannon].ai[1] = npc.whoAmI;
                Main.npc[primeCannon].target = npc.target;
                Main.npc[primeCannon].netUpdate = true;
            }
            if (limit == 2 || limit == 0) {
                primeSaw = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, NPCID.PrimeSaw, npc.whoAmI);
                Main.npc[primeSaw].ai[0] = 1f;
                Main.npc[primeSaw].ai[1] = npc.whoAmI;
                Main.npc[primeSaw].target = npc.target;
                Main.npc[primeSaw].netUpdate = true;
            }
            if (limit == 3 || limit == 0) {
                primeVice = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, NPCID.PrimeVice, npc.whoAmI);
                Main.npc[primeVice].ai[0] = -1f;
                Main.npc[primeVice].ai[1] = npc.whoAmI;
                Main.npc[primeVice].target = npc.target;
                Main.npc[primeVice].ai[3] = 150f;
                Main.npc[primeVice].netUpdate = true;
            }
            if (limit == 4 || limit == 0) {
                primeLaser = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, NPCID.PrimeLaser, npc.whoAmI);
                Main.npc[primeLaser].ai[0] = 1f;
                Main.npc[primeLaser].ai[1] = npc.whoAmI;
                Main.npc[primeLaser].target = npc.target;
                Main.npc[primeLaser].ai[3] = 150f;
                Main.npc[primeLaser].netUpdate = true;
            }
        }

        public override bool? Draw(Mod mod, NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => false;

        public override bool PostDraw(Mod mod, NPC NPC, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D mainValue = CalamityMod.CalamityMod.ChadPrime.Value;
            Main.EntitySpriteDraw(mainValue, NPC.Center - Main.screenPosition, CWRUtils.GetRec(mainValue, frame, 6)
                , drawColor, NPC.rotation, CWRUtils.GetOrig(mainValue, 6), NPC.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
