using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Events;
using CalamityMod.Items.Materials;
using CalamityMod.NPCs.Perforator;
using CalamityMod.Projectiles.Boss;
using CalamityMod.World;
using CalamityOverhaul.Content.Items.Placeable;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.OverhaulBehavior
{
    /// <summary>
    /// 关于血肉宿主的行为定义
    /// </summary>
    internal class PerforatorBehavior
    {
        public static PerforatorBehavior Instance;

        public virtual void Load() => Instance = this;

        public bool AttributeReinforcement = true;

        public virtual void AttributeReinforcementFunc(NPC npc) {
            if (AttributeReinforcement) {
                npc.life = npc.lifeMax = npc.lifeMax * 3;
                npc.damage += 25;
                npc.defense += 15;
                npc.scale += 0.25f;
                AttributeReinforcement = false;
            }
        }

        public struct HiveBlob
        {
            public Vector2 orig;
            public float rot;
            public float scale;
            public float leng;
            public Vector2 pos => orig + rot.ToRotationVector2() * leng;
        }

        public HiveBlob[] ThisHiveBlobs;
        public HiveBlob[] ThisHiveBlobs2;

        public virtual void UpdateBlob(NPC npc, bool phase1) {
            ThisHiveBlobs = new HiveBlob[6];
            for (int i = 0; i < 6; i++) {
                HiveBlob blob = new HiveBlob() {
                    orig = npc.Center,
                    leng = phase1 ? 133 : 155,
                    rot = MathHelper.TwoPi / 6 * i + Main.GameUpdateCount * (phase1 ? 0.05f : 0.08f),
                    scale = phase1 ? 1 : 1.5f
                };
                ThisHiveBlobs[i] = blob;
            }
            ThisHiveBlobs2 = new HiveBlob[16];
            for (int i = 0; i < 16; i++) {
                HiveBlob blob = new HiveBlob() {
                    orig = npc.Center,
                    leng = phase1 ? 1299 : 1155,
                    rot = MathHelper.TwoPi / 16 * i + Main.GameUpdateCount * 0.02f,
                    scale = phase1 ? 4 : 6.5f
                };
                ThisHiveBlobs2[i] = blob;
            }
            foreach (HiveBlob hiveBlob in ThisHiveBlobs) {
                Lighting.AddLight(hiveBlob.pos, Color.Red.ToVector3());
            }
            foreach (HiveBlob hiveBlob2 in ThisHiveBlobs2) {
                Lighting.AddLight(hiveBlob2.pos, Color.Red.ToVector3() * 3);
            }
        }

        /// <summary>
        /// 处理瞬移的视觉效果
        /// </summary>
        /// <param name="npc"></param>
        public void TeleportationEffect(NPC npc) {
            Lighting.AddLight(npc.Center, Color.Red.ToVector3() * 14);
            if (!CWRUtils.isServer) {
                for (int i = 0; i < 16; i++) {
                    int ichorDust = Dust.NewDust(new Vector2(npc.position.X, npc.position.Y), npc.width, npc.height, DustID.Ichor, 0f, 0f, 100, default, 1f);
                    Main.dust[ichorDust].velocity *= 2f;
                    if (Main.rand.NextBool()) {
                        Main.dust[ichorDust].scale = 0.25f;
                        Main.dust[ichorDust].fadeIn = 1f + Main.rand.Next(10) * 0.1f;
                    }
                }
                for (int i = 0; i < 16; i++) {
                    Vector2 pos = npc.Center;
                    Vector2 particleSpeed = Main.rand.NextVector2Unit() * Main.rand.NextFloat(5.5f, 7.7f);
                    CWRParticle energyLeak = new LightParticle(pos, particleSpeed
                        , Main.rand.NextFloat(0.3f, 0.9f), Color.Red, 30, 1, 1.5f, hueShift: 0.0f);
                    CWRParticleHandler.AddParticle(energyLeak);
                }
            }
        }
        public void SpanArrowProjs(NPC npc, Vector2 orig, Vector2 toTarget) {
            SoundEngine.PlaySound(SoundID.ForceRoar, npc.position);
            Vector2 toTargetUnit = toTarget.UnitVector();
            Vector2 norlVr = toTargetUnit.GetNormalVector();
            int damage = npc.damage / (Main.masterMode ? 5 : Main.expertMode ? 3 : 2);
            float speed = 17;
            for (int i = 0; i < 13; i++) {
                int setInYnum = (i < 6 ? i : 12 - i) * 33;
                Vector2 setInXVr = norlVr * (65 - 10 * i);
                Vector2 spanPos = npc.Center + setInXVr + toTargetUnit * (setInYnum - 130);
                Projectile.NewProjectile(npc.GetSource_FromAI(), spanPos, toTargetUnit * speed, ModContent.ProjectileType<BloodGeyser>(), damage, 0f, Main.myPlayer);
            }
            for (int i = 0; i < 13; i++) {
                Vector2 spanPos = npc.Center + toTargetUnit * -i * 33;
                Projectile.NewProjectile(npc.GetSource_FromAI(), spanPos, toTargetUnit * speed, ModContent.ProjectileType<IchorShot>(), damage, 0f, Main.myPlayer);
            }
        }
        public virtual void Draw(SpriteBatch spriteBatch, NPC npc) {
            Texture2D value = CWRUtils.GetT2DValue("CalamityMod/NPCs/HiveMind/HiveBlob");
            if (ThisHiveBlobs != null) {
                foreach (HiveBlob hiveBlob in ThisHiveBlobs) {
                    spriteBatch.Draw(value, hiveBlob.pos - Main.screenPosition, null, Color.Red, 0, value.Size() / 2, hiveBlob.scale, SpriteEffects.None, 0);
                }
            }
            if (ThisHiveBlobs2 != null) {
                foreach (HiveBlob hiveBlob2 in ThisHiveBlobs2) {
                    spriteBatch.Draw(value, hiveBlob2.pos - Main.screenPosition, null, Color.Red, hiveBlob2.pos.To(Main.player[npc.target].Center).ToRotation() + MathHelper.PiOver2, value.Size() / 2, hiveBlob2.scale, SpriteEffects.None, 0);
                }
            }
        }
        /// <summary>
        /// 执行强化行为附加
        /// </summary>
        /// <param name="npc"></param>
        public virtual void Intensive(NPC npc) {
            AttributeReinforcementFunc(npc);
            PerforatorHive perforatorHive = (PerforatorHive)npc.ModNPC;
            float lifeRatio = npc.life / (float)npc.lifeMax;
            Player player = Main.player[npc.target];
            bool bossRush = BossRushEvent.BossRushActive;
            bool expertMode = Main.expertMode || bossRush;
            bool revenge = CalamityWorld.revenge || bossRush;
            bool death = CalamityWorld.death || bossRush;
            bool phase1 = lifeRatio >= 0.7f;
            int npcDamage = npc.damage / (Main.masterMode ? 5 : Main.expertMode ? 3 : 2);
            UpdateBlob(npc, phase1);
            if (phase1) {//一阶段
                if (npc.localAI[0] != 0) {
                    if (!CWRUtils.isClient) {
                        if (npc.localAI[0] % 180 == 0) {//在一阶段，我们需要周期性的发射横跨整个屏幕的水平弹幕来提高难度
                            const int maxspanWidth = 9000;
                            const int maxspanProjNum = 60;
                            int step = maxspanWidth / maxspanProjNum;
                            float spanPointXProjs = -(maxspanWidth / 2) + npc.Center.X;
                            float spanPointYProjs = npc.Center.Y - 800;
                            int type = Main.rand.NextBool() ? ModContent.ProjectileType<IchorShot>() : ModContent.ProjectileType<BloodGeyser>();
                            int damage = (int)(npcDamage * 0.75f);
                            for (int i = 0; i < 60; i++) {
                                Vector2 spanPos = new Vector2(spanPointXProjs, spanPointYProjs);
                                Projectile.NewProjectile(npc.GetSource_FromAI(), spanPos, new Vector2(0, 6), type, damage, 0f, Main.myPlayer, 0f, player.Center.Y);
                                spanPointXProjs += step;
                            }
                        }
                        if (npc.localAI[0] % 60 == 0) {//并且，我们需要让它频繁的向玩家发射危险性很大的针对性血弹                        
                            Vector2 toTarget = npc.Center.To(player.Center);
                            SpanArrowProjs(npc, npc.Center, toTarget);
                            if (ThisHiveBlobs != null) {
                                foreach (var blob in ThisHiveBlobs) {
                                    Projectile.NewProjectile(npc.GetSource_FromAI(), blob.pos, blob.rot.ToRotationVector2() * 9, ModContent.ProjectileType<BrimstoneBarrage>(), npcDamage / 2, 0f, Main.myPlayer);
                                }
                            }
                        }
                    }
                }
                npc.position.X += npc.velocity.X * 0.5f;
                npc.position.Y += npc.velocity.Y * 0.2f;

            }
            else {//在二阶段
                if (!CWRUtils.isClient) {
                    if (npc.localAI[0] % 90 == 0 && npc.localAI[0] != 0 && lifeRatio < 0.4f) {//周期性发射圆周性的灵液球弹幕
                        for (int i = 0; i < 16; i++) {
                            Vector2 vr = (MathHelper.TwoPi / 16f * i).ToRotationVector2() * 13;
                            int damage = 53;
                            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + Vector2.UnitY * 50f, vr, ModContent.ProjectileType<IchorBlob>(), damage, 0f, Main.myPlayer, 0f, player.Center.Y);
                        }
                    }
                    if (!NPC.AnyNPCs(ModContent.NPCType<PerforatorHeadMedium>())) {
                        SoundEngine.PlaySound(SoundID.ForceRoarPitched);
                        NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, ModContent.NPCType<PerforatorHeadMedium>(), 1);
                        NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, ModContent.NPCType<PerforatorHeadMedium>(), 1);
                    }
                }
                if (npc.localAI[0] % 150 == 0 && npc.localAI[0] != 0) {//我们让它每隔一小段时间就瞬移一次
                    SoundEngine.PlaySound(SoundID.NPCDeath23, npc.position);
                    TeleportationEffect(npc);
                    npc.position = player.Center + player.velocity.UnitVector().RotatedBy(Main.rand.NextFloat(-0.3f, 0.3f)) * Main.rand.Next(420, 780);
                    npc.netUpdate = true;
                    TeleportationEffect(npc);
                    if (ThisHiveBlobs != null) {
                        foreach (var blob in ThisHiveBlobs) {
                            for (int i = 0; i < 2; i++) {
                                Vector2 vr = (blob.rot + (-8 + 16 * i) * CWRUtils.atoR).ToRotationVector2() * 13;
                                Projectile.NewProjectile(npc.GetSource_FromAI(), blob.pos, vr, ProjectileID.CursedFlameHostile, npcDamage / 2, 0f, Main.myPlayer);
                            }
                        }
                    }
                }
                if (npc.localAI[0] % 75 == 0 && npc.localAI[0] != 0 && npc.localAI[0] % 150 != 0) {
                    for (int i = 0; i < 4; i++) {
                        float rot = MathHelper.TwoPi / 4f * i;
                        Vector2 spanPos = rot.ToRotationVector2() * 1200 + player.Center;
                        Vector2 vr = (rot + MathHelper.Pi).ToRotationVector2();
                        for (int j = 0; j < 16; j++) {
                            Projectile.NewProjectile(npc.GetSource_FromAI(), spanPos + vr * i * 33, vr * 17, ProjectileID.CursedFlameHostile, npcDamage / 2, 0f, Main.myPlayer);
                        }
                    }
                    if (ThisHiveBlobs != null) {
                        foreach (var blob in ThisHiveBlobs) {
                            Projectile.NewProjectile(npc.GetSource_FromAI(), blob.pos, blob.rot.ToRotationVector2() * 9, ProjectileID.CursedFlameHostile, npcDamage / 2, 0f, Main.myPlayer);
                        }
                    }
                }
                npc.position.X += npc.velocity.X * 0.25f;
                npc.position.Y += npc.velocity.Y * 0.75f;
            }
            if (player.Center.To(npc.Center).LengthSquared() > 1299 * 1299) {
                player.AddBuff(ModContent.BuffType<VulnerabilityHex>(), 30);
            }
        }

        public virtual void BloodMoonDorp(NPC npc) {
            if (npc.type == CWRIDs.PerforatorHive) {
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
