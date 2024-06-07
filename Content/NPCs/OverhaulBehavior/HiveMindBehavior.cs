using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Events;
using CalamityMod.Items.Materials;
using CalamityMod.NPCs.HiveMind;
using CalamityMod.World;
using CalamityOverhaul.Content.Items.Placeable;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.OverhaulBehavior
{
    /// <summary>
    /// 关于腐巢意志的行为定义
    /// </summary>
    internal class HiveMindBehavior : PerforatorBehavior
    {
        public static new HiveMindBehavior Instance;

        public override void Load() => Instance = this;

        private int Time;

        private int stateValueInt;

        public override void AttributeReinforcementFunc(NPC npc) {
            if (AttributeReinforcement) {
                npc.life = npc.lifeMax = (int)(npc.lifeMax * 3.5f);
                npc.damage += 15;
                npc.scale += 0.25f;
                AttributeReinforcement = false;
            }
        }

        public override void Draw(SpriteBatch spriteBatch, NPC npc) {
            Texture2D value = CWRUtils.GetT2DValue("CalamityMod/NPCs/HiveMind/DankCreeper");
            if (ThisHiveBlobs != null) {
                foreach (HiveBlob hiveBlob in ThisHiveBlobs) {
                    spriteBatch.Draw(value, hiveBlob.pos - Main.screenPosition, null, Color.White, 0, value.Size() / 2, hiveBlob.scale, SpriteEffects.None, 0);
                }
            }
            if (ThisHiveBlobs2 != null && stateValueInt != 3) {
                foreach (HiveBlob hiveBlob2 in ThisHiveBlobs2) {
                    spriteBatch.Draw(value, hiveBlob2.pos - Main.screenPosition, null, Color.White, hiveBlob2.pos.To(Main.player[npc.target].Center).ToRotation() + MathHelper.PiOver2, value.Size() / 2, hiveBlob2.scale, SpriteEffects.None, 0);
                }
            }
        }

        public override void UpdateBlob(NPC npc, bool phase2) {
            ThisHiveBlobs = new HiveBlob[6];
            for (int i = 0; i < 6; i++) {
                HiveBlob blob = new HiveBlob() {
                    orig = npc.Center,
                    leng = phase2 ? 133 : 155,
                    rot = MathHelper.TwoPi / 6 * i + Main.GameUpdateCount * (phase2 ? 0.05f : 0.08f),
                    scale = phase2 ? 1 : 1f
                };
                ThisHiveBlobs[i] = blob;
            }
            ThisHiveBlobs2 = new HiveBlob[16];
            for (int i = 0; i < 16; i++) {
                HiveBlob blob = new HiveBlob() {
                    orig = npc.Center,
                    leng = phase2 ? 1099 : 1155,
                    rot = MathHelper.TwoPi / 16 * i + Main.GameUpdateCount * 0.02f,
                    scale = phase2 ? 3.5f : 2.5f
                };
                ThisHiveBlobs2[i] = blob;
            }
            foreach (HiveBlob hiveBlob in ThisHiveBlobs) {
                Lighting.AddLight(hiveBlob.pos, Color.DarkBlue.ToVector3());
            }
            foreach (HiveBlob hiveBlob2 in ThisHiveBlobs2) {
                Lighting.AddLight(hiveBlob2.pos, Color.DarkBlue.ToVector3() * 3);
            }
        }

        /// <summary>
        /// 执行强化行为附加
        /// </summary>
        /// <param name="npc"></param>
        public override void Intensive(NPC npc) {
            AttributeReinforcementFunc(npc);
            HiveMind hiveMind = (HiveMind)npc.ModNPC;
            Player player = Main.player[npc.target];
            float lifeRatio = npc.life / (float)npc.lifeMax;
            int damage = npc.damage / (Main.masterMode ? 5 : Main.expertMode ? 3 : 2);
            bool bossRush = BossRushEvent.BossRushActive;
            bool expertMode = Main.expertMode || bossRush;
            bool revenge = CalamityWorld.revenge || bossRush;
            bool death = CalamityWorld.death || bossRush;
            bool phase2 = lifeRatio < 0.8f;
            UpdateBlob(npc, phase2);

            Type type = typeof(HiveMind);
            FieldInfo privateState = type.GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo privateBurrowTimer = type.GetField("burrowTimer", BindingFlags.NonPublic | BindingFlags.Instance);

            if (privateState == null || privateBurrowTimer == null)
                return;

            object stateValue = privateState.GetValue(hiveMind);
            object burrowTimerValue = privateBurrowTimer.GetValue(hiveMind);
            stateValueInt = (int)stateValue;
            int burrowTimerValueInt = (int)burrowTimerValue;
            if (stateValueInt == 0 && !phase2 && !CWRUtils.isClient) {
                if (burrowTimerValueInt == 0) {
                    for (int i = 0; i < 3; i++) {
                        foreach (HiveBlob hiveBlob in ThisHiveBlobs) {
                            int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), hiveBlob.pos, hiveBlob.rot.ToRotationVector2() * (9 + i), ProjectileID.CursedFlameHostile, damage, 0f, 0);
                            Main.projectile[proj].tileCollide = false;
                            Main.projectile[proj].scale = 2;
                        }
                    }
                }
                if (burrowTimerValueInt % 60 == 0) {
                    if (Main.npc.Count((n) => n.type == NPCID.EaterofSouls) <= 33) {
                        int npcwho = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.position.X + Main.rand.Next(npc.width), (int)npc.position.Y + Main.rand.Next(npc.height), NPCID.EaterofSouls);
                        NPC eaterofSouls = Main.npc[npcwho];
                        eaterofSouls.scale = Main.rand.NextFloat(1.25f, 3);
                        eaterofSouls.life = eaterofSouls.lifeMax = (int)(eaterofSouls.lifeMax * eaterofSouls.scale * 2);
                        eaterofSouls.netUpdate = true;
                        eaterofSouls.netSpam = 0;
                    }
                }
            }
            if (phase2 && !CWRUtils.isClient) {
                if (stateValueInt == 3) {
                    if (Time % 20 == 0) {
                        foreach (HiveBlob hiveBlob in ThisHiveBlobs) {
                            int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), hiveBlob.pos, hiveBlob.rot.ToRotationVector2() * 6, ProjectileID.CursedFlameHostile, damage, 0f, 0);
                            Main.projectile[proj].tileCollide = false;
                        }
                    }
                }
                npc.position.X += npc.velocity.X * 0.75f;
                npc.position.Y += npc.velocity.Y * 0.75f;
            }
            if (player.Center.To(npc.Center).LengthSquared() > 1299 * 1299) {
                player.AddBuff(ModContent.BuffType<VulnerabilityHex>(), 30);
            }

            Time++;
        }

        public override void BloodMoonDorp(NPC npc) {
            if (npc.type == CWRLoad.HiveMind) {
                if (Main.bloodMoon && !CWRUtils.isClient) {
                    for (int i = 0; i < Main.rand.Next(19, 26); i++) {
                        int type = Item.NewItem(npc.parent(), npc.Hitbox, ModContent.ItemType<BloodOrb>(), Main.rand.Next(7, 13));
                        Main.item[type].velocity = Main.rand.NextVector2Unit() * Main.rand.Next(12, 15);
                        if (CWRUtils.isClient) {
                            NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
                        }
                    }
                    int type2 = Item.NewItem(npc.parent(), npc.Hitbox, ModContent.ItemType<BloodAltar>());
                    if (CWRUtils.isClient) {
                        NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type2, 0f, 0f, 0f, 0, 0, 0);
                    }
                }
                AttributeReinforcement = true;
            }
        }
    }
}
