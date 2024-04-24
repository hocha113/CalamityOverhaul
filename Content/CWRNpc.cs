using CalamityMod;
using CalamityMod.Events;
using CalamityMod.Items;
using CalamityMod.Items.Accessories;
using CalamityMod.Items.Materials;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.NPCs.AquaticScourge;
using CalamityMod.NPCs.NormalNPCs;
using CalamityMod.World;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Events;
using CalamityOverhaul.Content.Items;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using CalamityOverhaul.Content.Items.Summon.Extras;
using CalamityOverhaul.Content.NPCs.OverhaulBehavior;
using CalamityOverhaul.Content.Projectiles;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content
{
    public class CWRNpc : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public Player CreateHitPlayer;
        public byte ContagionOnHitNum = 0;
        public byte PhosphorescentGauntletOnHitNum = 0;
        public byte TerratomereBoltOnHitNum = 0;
        public byte OrderbringerOnHitNum = 0;
        public bool TheEndSunOnHitNum;
        public byte WhipHitNum = 0;
        public byte WhipHitType = 0;
        public bool SprBoss;
        public bool ObliterateBool;
        public bool GangarusSign;
        public ushort colldHitTime = 0;
        /// <summary>
        /// 实体是否受到鬼妖升龙斩的击退
        /// </summary>
        public bool MurasamabrBeatBackBool;
        /// <summary>
        /// 击退力的具体向量
        /// </summary>
        public Vector2 MurasamabrBeatBackVr;
        /// <summary>
        /// 升龙击退的衰减力度系数，1为不衰减
        /// </summary>
        public float MurasamabrBeatBackAttenuationForce;
        /// <summary>
        /// 上一帧实体的位置
        /// </summary>
        public Vector2 oldNPCPos;
        /// <summary>
        /// 极寒神性屏障
        /// </summary>
        public bool IceParclose;
        public static Asset<Texture2D> IceParcloseAsset;

        public override void Load() {
            IceParcloseAsset = CWRUtils.GetT2DAsset(CWRConstant.Projectile + "IceParclose", true);
        }

        public override void ResetEffects(NPC npc) {
            IceParclose = false;
        }

        public override void SetDefaults(NPC npc) {
            if (TungstenRiot.Instance.TungstenRiotIsOngoing) {
                if (TungstenRiot.TungstenEventNPCDic.ContainsKey(npc.type)) {
                    npc.life = npc.lifeMax = (int)(npc.lifeMax * 1.2f);
                    npc.defense += 3;
                }
                if (npc.type == ModContent.NPCType<WulfrumAmplifier>()) {
                    npc.life = npc.lifeMax = npc.lifeMax * 10;
                    npc.defense += 10;
                    npc.scale += 0.5f;
                    npc.boss = true;
                }
            }
        }

        public static void MultipleSegmentsLimitDamage(NPC target, ref NPC.HitModifiers modifiers) {
            if (CWRIDs.targetNpcTypes15.Contains(target.type) || CWRIDs.targetNpcTypes10.Contains(target.type)
                || CWRIDs.targetNpcTypes8.Contains(target.type) || CWRIDs.targetNpcTypes7.Contains(target.type)
                || CWRIDs.targetNpcTypes6.Contains(target.type) || CWRIDs.targetNpcTypes5.Contains(target.type)
                || CWRIDs.targetNpcTypes4.Contains(target.type) || CWRIDs.targetNpcTypes2.Contains(target.type)
                || CWRIDs.WormBodys.Contains(target.type) || target.type == ModContent.NPCType<AquaticScourgeBodyAlt>()) {
                modifiers.FinalDamage *= 0.1f;
                int dmownInt = (int)(target.lifeMax * 0.001f);
                if (dmownInt < 50) {
                    dmownInt = 50;
                }
                modifiers.SetMaxDamage(dmownInt + Main.rand.Next(50));
            }
        }

        public override bool CanBeHitByNPC(NPC npc, NPC attacker) {
            return base.CanBeHitByNPC(npc, attacker);
        }

        public override bool CheckDead(NPC npc) {
            if (ObliterateBool) {
                return true;
            }
            return base.CheckDead(npc);
        }

        public override bool PreAI(NPC npc) {
            if (IceParclose) {
                return false;
            }
            if (MurasamabrBeatBackBool) {
                npc.position += MurasamabrBeatBackVr;
                if (oldNPCPos.Y - npc.position.Y < 0) {
                    MurasamabrBeatBackVr.Y *= 0.9f;
                }
                oldNPCPos = npc.position;
                MurasamabrBeatBackVr *= MurasamabrBeatBackAttenuationForce;
                MurasamabrBeatBackVr.X *= MurasamabrBeatBackAttenuationForce;
                if (MurasamabrBeatBackVr.LengthSquared() < 2) {
                    MurasamabrBeatBackBool = false;
                }
            }
            if (TungstenRiot.Instance.TungstenRiotIsOngoing) {
                if (npc.target >= 0 && npc.target < Main.player.Length) {
                    Player player = Main.player[npc.target];
                    if (TungstenRiot.TungstenEventNPCDic.ContainsKey(npc.type)) {
                        if (Main.GameUpdateCount % 60 == 0 && npc.type == ModContent.NPCType<WulfrumDrone>()) {
                            SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.7f, Pitch = -0.2f }, npc.Center);
                            if (!CWRUtils.isClient) {
                                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + Vector2.UnitX * 6f * npc.spriteDirection
                                , npc.SafeDirectionTo(player.Center, Vector2.UnitY) * 6f, ProjectileID.SaucerLaser, 12, 0f);
                            }
                        }
                    }
                    if (npc.type == ModContent.NPCType<WulfrumAmplifier>()) {
                        CWRUtils.WulfrumAmplifierAI(npc, 700, 300);
                        if (Main.GameUpdateCount % 60 == 0) {
                            SoundEngine.PlaySound(ScorchedEarthEcType.ShootSound with { Volume = 0.4f, Pitch = 0.6f }, npc.Center);
                            if (!CWRUtils.isClient) {
                                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + Vector2.UnitX * 6f * npc.spriteDirection
                                , npc.SafeDirectionTo(player.Center, Vector2.UnitY) * 6f, ProjectileID.SaucerMissile, 12, 0f);
                            }
                        }
                        return false;
                    }
                }
            }
            return base.PreAI(npc);
        }

        public override void PostAI(NPC npc) {
            if (!CWRUtils.isClient) {
                if (WhipHitNum > 10) {
                    WhipHitNum = 10;
                }
            }
            if (Main.bloodMoon) {//在血月的情况下让一些生物执行特殊的行为，将这段代码写在PostAI中是防止被覆盖
                if (npc.type == CWRIDs.PerforatorHive)//改动血肉宿主的行为，这会让它在血月更加的暴躁和危险
                    PerforatorBehavior.Instance.Intensive(npc);
                if (npc.type == CWRIDs.HiveMind)//改动腐巢意志的行为，这会让它在血月更加的恐怖和强大
                    HiveMindBehavior.Instance.Intensive(npc);
            }
        }

        public override bool PreKill(NPC npc) {
            if (ContagionOnHitNum > 0 && CreateHitPlayer != null) {
                if (Main.myPlayer == CreateHitPlayer.whoAmI && CreateHitPlayer.ownedProjectileCounts[ModContent.ProjectileType<NurgleSoul>()] <= 13) {
                    Projectile proj = Projectile.NewProjectileDirect(CreateHitPlayer.parent(), npc.Center, CWRUtils.randVr(13)
                        , ModContent.ProjectileType<NurgleSoul>(), npc.damage, 2, CreateHitPlayer.whoAmI);
                    proj.scale = (npc.width / proj.width) * npc.scale;
                }
            }
            return base.PreKill(npc);
        }

        public override void OnKill(NPC npc) {
            if (npc.boss) {
                if (CWRIDs.targetNpcTypes7.Contains(npc.type) || npc.type == CWRIDs.PlaguebringerGoliath) {
                    for (int i = 0; i < Main.rand.Next(3, 6); i++) {
                        int type = Item.NewItem(npc.parent(), npc.Hitbox, CWRIDs.DubiousPlating, Main.rand.Next(7, 13));
                        if (CWRUtils.isClient) {
                            NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
                        }
                    }
                }
                if (npc.type == CWRIDs.PrimordialWyrmHead) {
                    int type = Item.NewItem(npc.parent(), npc.Hitbox, ModContent.ItemType<TerminusOver>());
                    if (CWRUtils.isClient) {
                        NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
                    }
                }
                if (npc.type == CWRIDs.Yharon && InWorldBossPhase.Instance.Level() >= 13 && Main.zenithWorld && !CWRUtils.isClient) {
                    Player target = CWRUtils.GetPlayerInstance(npc.target);
                    if (target.Alives()) {
                        float dir = npc.Center.To(target.Center).X;
                        int dirs = dir < 0 ? 1 : 0;
                        Projectile.NewProjectile(npc.parent(), npc.position, Vector2.Zero
                        , ModContent.ProjectileType<YharonOreProj>(), 0, 0, -1, dirs);
                    }
                }
            }
            if (npc.type == CWRIDs.PrimordialWyrmHead) {//我不知道为什么原灾厄没有设置这个字段，为了保持进度的正常，我在这里额外设置一次
                DownedBossSystem.downedPrimordialWyrm = true;
            }
            if (TungstenRiot.Instance.TungstenRiotIsOngoing) {
                TungstenRiot.Instance.TungstenKillNPC(npc);
            }
            PerforatorBehavior.Instance.BloodMoonDorp(npc);
            HiveMindBehavior.Instance.BloodMoonDorp(npc);
            base.OnKill(npc);
        }

        public override void HitEffect(NPC npc, NPC.HitInfo hit) {
            if (npc.life <= 0) {
                if (TheEndSunOnHitNum) {
                    if (!BossRushEvent.BossRushActive) {
                        for (int i = 0; i < Main.rand.Next(16, 33); i++) {
                            npc.NPCLoot();
                        }
                    }
                    else {
                        if (Main.rand.NextBool(5)) {//如果是在BossRush时期，让Boss有一定概率掉落古恒石，这是额外的掉落
                            int type = Item.NewItem(npc.parent(), npc.Hitbox, ModContent.ItemType<Rock>());
                            if (CWRUtils.isClient) {
                                NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
                            }
                        }
                    }
                }
            }
            base.HitEffect(npc, hit);
        }

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot) {
            if (npc.type == CWRIDs.Polterghast) {
                npcLoot.DefineConditionalDropSet(CWRDorp.InHellDropRule).Add(ModContent.ItemType<GhostFireWhip>());
            }
            else if (npc.type == CWRIDs.Yharon) {
                npcLoot.DefineConditionalDropSet(CWRDorp.GlodDragonDropRule).Add(CWRDorp.Quantity(ModContent.ItemType<AuricBar>(), 1, 36, 57, 77, 158));
            }
            else if (npc.type == CWRIDs.DevourerofGodsHead) {
                npcLoot.Add(DropHelper.PerPlayer(ModContent.ItemType<Ataraxia>(), denominator: 3, minQuantity: 1, maxQuantity: 1));
                npcLoot.Add(DropHelper.PerPlayer(ModContent.ItemType<Nadir>(), denominator: 3, minQuantity: 1, maxQuantity: 1));
            }
            else if (npc.type == CWRIDs.RavagerBody) {
                npcLoot.Add(DropHelper.PerPlayer(ModContent.ItemType<PetrifiedDisease>(), denominator: 5, minQuantity: 1, maxQuantity: 1));
            }
            TungstenRiot.Instance.ModifyEventNPCLoot(npc, ref npcLoot);
        }

        public override void ModifyShop(NPCShop shop) {
            foreach (AbstractNPCShop.Entry i in shop.Entries) {
                Item item = i.Item;
                if (item?.type != ItemID.None) {
                    Item item2 = new Item(item.type);
                    item2.SetDefaults(item.type);
                    CWRItems cwr = item2.CWR();
                    if (cwr.HasCartridgeHolder || cwr.heldProjType > 0 || cwr.isHeldItem) {
                        item.SetDefaults(item.type);
                    }
                }
            }
        }

        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns) {
            if (TungstenRiot.Instance.TungstenRiotIsOngoing) {
                maxSpawns = (int)(maxSpawns * 1.15f);
                spawnRate = 2;
            }
        }

        public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo) {
            if (TungstenRiot.Instance.TungstenRiotIsOngoing) {
                pool.Clear();
                foreach (int type in TungstenRiot.TungstenEventNPCDic.Keys) {
                    if (!pool.ContainsKey(type)) {
                        pool.Add(type, TungstenRiot.TungstenEventNPCDic[type].SpawnRate);
                    }
                }
                if (TungstenRiot.Instance.EventKillRatio < 0.5f) {
                    pool.Add(ModContent.NPCType<WulfrumAmplifier>(), 0.25f);
                }
            }
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.bloodMoon) {
                if (npc.type == CWRIDs.PerforatorHive) {
                    PerforatorBehavior.Instance.Draw(spriteBatch, npc);
                }
                if (npc.type == CWRIDs.HiveMind) {
                    HiveMindBehavior.Instance.Draw(spriteBatch, npc);
                }
            }
            if (IceParclose) {
                float slp = npc.scale * (npc.height / (float)IceParcloseAsset.Value.Height) * 2;
                float sengs = 0.3f + Math.Abs(MathF.Sin(Main.GameUpdateCount * 0.1f) * 0.3f);
                spriteBatch.Draw(IceParcloseAsset.Value, npc.Center - Main.screenPosition, null, Color.White * sengs, 0, IceParcloseAsset.Value.Size() / 2, slp, SpriteEffects.None, 0);
            }
        }
    }
}
