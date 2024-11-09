using CalamityMod;
using CalamityMod.Events;
using CalamityMod.Items;
using CalamityMod.NPCs.AquaticScourge;
using CalamityMod.NPCs.NormalNPCs;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Events;
using CalamityOverhaul.Content.NPCs.Core;
using CalamityOverhaul.Content.Projectiles;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content
{
    public class CWRNpc : GlobalNPC, ICWRLoader
    {
        #region Data
        public override bool InstancePerEntity => true;
        /// <summary>
        /// 这个NPC实体的对应修改副本
        /// </summary>
        internal NPCOverride NPCOverride = new NPCOverride();
        /// <summary>
        /// 瘟疫命中锚点
        /// </summary>
        public Player CreateHitPlayer;
        public byte ContagionOnHitNum = 0;
        public byte PhosphorescentGauntletOnHitNum = 0;
        public byte TerratomereBoltOnHitNum = 0;
        public byte OrderbringerOnHitNum = 0;
        public bool TheEndSunOnHitNum;
        public byte WhipHitNum = 0;
        public byte WhipHitType = 0;
        /// <summary>
        /// 一个特殊标记，用于朗基努斯识别目标
        /// </summary>
        public bool GangarusSign;
        /// <summary>
        /// 实体是否受到额外的击退
        /// </summary>
        public bool OverBeatBackBool;
        /// <summary>
        /// 击退力的具体向量
        /// </summary>
        public Vector2 OverBeatBackVr;
        /// <summary>
        /// 击退的衰减力度系数，1为不衰减
        /// </summary>
        public float OverBeatBackAttenuationForce;
        /// <summary>
        /// 上一帧实体的位置
        /// </summary>
        public Vector2 OldNPCPos;
        /// <summary>
        /// 极寒神性屏障
        /// </summary>
        public bool IceParclose;
        public bool HellfireExplosion;
        public bool VoidErosionBool;

        public static Asset<Texture2D> IceParcloseAsset { get; private set; }
        #endregion

        public override GlobalNPC Clone(NPC from, NPC to) => CloneCWRNpc((CWRNpc)base.Clone(from, to));
        public CWRNpc CloneCWRNpc(CWRNpc cwr) {
            cwr.CreateHitPlayer = CreateHitPlayer;
            cwr.ContagionOnHitNum = ContagionOnHitNum;
            cwr.PhosphorescentGauntletOnHitNum = PhosphorescentGauntletOnHitNum;
            cwr.TerratomereBoltOnHitNum = TerratomereBoltOnHitNum;
            cwr.OrderbringerOnHitNum = OrderbringerOnHitNum;
            cwr.TheEndSunOnHitNum = TheEndSunOnHitNum;
            cwr.WhipHitNum = WhipHitNum;
            cwr.WhipHitType = WhipHitType;
            cwr.GangarusSign = GangarusSign;
            cwr.OverBeatBackBool = OverBeatBackBool;
            cwr.OverBeatBackVr = OverBeatBackVr;
            cwr.OverBeatBackAttenuationForce = OverBeatBackAttenuationForce;
            cwr.IceParclose = IceParclose;
            return cwr;
        }

        public static void OverBeatBackSend(NPC npc, float power = 0.99f) {
            if (CWRUtils.isClient) {
                var netMessage = CWRMod.Instance.GetPacket();
                netMessage.Write((byte)CWRMessageType.OverBeatBack);
                netMessage.Write((byte)npc.whoAmI);
                netMessage.WriteVector2(npc.CWR().OverBeatBackVr);
                netMessage.Write(power);
                netMessage.Send();
            }
        }

        public static void OtherBeatBackReceive(BinaryReader reader, int whoAmI) {
            NPC npc = Main.npc[reader.ReadByte()];
            Vector2 overBeatBackVr = reader.ReadVector2();
            float power = reader.ReadSingle();
            if (npc.type == NPCID.None || !npc.active) {
                return;
            }
            CWRNpc modnpc = npc.CWR();
            modnpc.OverBeatBackBool = true;
            modnpc.OverBeatBackVr = overBeatBackVr;
            modnpc.OverBeatBackAttenuationForce = power;
            if (CWRUtils.isServer) {
                var netMessage = CWRMod.Instance.GetPacket();
                netMessage.Write((byte)CWRMessageType.OverBeatBack);
                netMessage.Write((byte)npc.whoAmI);
                netMessage.WriteVector2(npc.CWR().OverBeatBackVr);
                netMessage.Write(power);
                netMessage.Send(-1, whoAmI);
            }
        }

        void ICWRLoader.LoadAsset() => IceParcloseAsset = CWRUtils.GetT2DAsset(CWRConstant.Projectile + "IceParclose");

        public override void ResetEffects(NPC npc) {
            IceParclose = false;
            VoidErosionBool = false;
        }
        public override void SetDefaults(NPC npc) {
            NPCOverride.SetDefaults(npc, this, npc.Calamity());
            TungstenRiot.SetEventNPC(npc);
        }

        public static void MultipleSegmentsLimitDamage(NPC target, ref NPC.HitModifiers modifiers) {
            if (CWRLoad.targetNpcTypes15.Contains(target.type) || CWRLoad.targetNpcTypes10.Contains(target.type)
                || CWRLoad.targetNpcTypes8.Contains(target.type) || CWRLoad.targetNpcTypes7.Contains(target.type)
                || CWRLoad.targetNpcTypes6.Contains(target.type) || CWRLoad.targetNpcTypes5.Contains(target.type)
                || CWRLoad.targetNpcTypes4.Contains(target.type) || CWRLoad.targetNpcTypes2.Contains(target.type)
                || CWRLoad.WormBodys.Contains(target.type) || target.type == ModContent.NPCType<AquaticScourgeBodyAlt>()) {
                modifiers.FinalDamage *= 0.1f;
                int dmownInt = (int)(target.lifeMax * 0.001f);
                if (dmownInt < 50) {
                    dmownInt = 50;
                }
                modifiers.SetMaxDamage(dmownInt + Main.rand.Next(50));
            }
        }

        private void UpdateOverBeatBack(NPC npc) {
            if (OverBeatBackBool) {
                Vector2 v = Collision.TileCollision(npc.position, OverBeatBackVr, npc.width, npc.height);
                if (OverBeatBackVr != v) {
                    OverBeatBackBool = false;
                    return;
                }
                npc.position += OverBeatBackVr;
                if (OldNPCPos.Y - npc.position.Y < 0) {
                    OverBeatBackVr.Y *= 0.9f;
                }
                OldNPCPos = npc.position;
                OverBeatBackVr *= OverBeatBackAttenuationForce;
                OverBeatBackVr.X *= OverBeatBackAttenuationForce;
                if (OverBeatBackVr.LengthSquared() < 2) {
                    OverBeatBackBool = false;
                }
            }
        }

        public override bool PreAI(NPC npc) {
            if (IceParclose) {
                return false;
            }
            UpdateOverBeatBack(npc);
            bool? tungstenset = TungstenRiot.Instance.UpdateNPCPreAISet(npc);
            return tungstenset.HasValue ? tungstenset.Value : base.PreAI(npc);
        }

        public override void PostAI(NPC npc) {
            if (!CWRUtils.isClient) {
                if (WhipHitNum > 10) {
                    WhipHitNum = 10;
                }
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
                if (CWRLoad.targetNpcTypes7.Contains(npc.type) || npc.type == CWRLoad.PlaguebringerGoliath) {
                    for (int i = 0; i < Main.rand.Next(3, 6); i++) {
                        int type = Item.NewItem(npc.parent(), npc.Hitbox, CWRLoad.DubiousPlating, Main.rand.Next(7, 13));
                        if (CWRUtils.isClient) {
                            NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
                        }
                    }
                }
                if (npc.type == CWRLoad.Yharon && InWorldBossPhase.Instance.level11 && Main.zenithWorld && !CWRUtils.isClient) {
                    Player target = CWRUtils.GetPlayerInstance(npc.target);
                    if (target.Alives()) {
                        float dir = npc.Center.To(target.Center).X;
                        int dirs = dir < 0 ? 1 : 0;
                        Projectile.NewProjectile(npc.parent(), npc.position, Vector2.Zero
                        , ModContent.ProjectileType<YharonOreProj>(), 0, 0, -1, dirs);
                    }
                }
            }
            if (npc.type == CWRLoad.PrimordialWyrmHead && !DownedBossSystem.downedPrimordialWyrm) {//我不知道为什么原灾厄没有设置这个字段，为了保持进度的正常，我在这里额外设置一次
                DownedBossSystem.downedPrimordialWyrm = true;
                if (Main.dedServ) {
                    NetMessage.SendData(MessageID.WorldData);
                }
            }
            if (npc.type == CWRLoad.Yharon) {
                InWorldBossPhase.YharonKillCount++;
                if (Main.dedServ) {
                    NetMessage.SendData(MessageID.WorldData);
                }
            }
            if (TungstenRiot.Instance.TungstenRiotIsOngoing) {
                TungstenRiot.Instance.TungstenKillNPC(npc);
            }
        }

        public override void HitEffect(NPC npc, NPC.HitInfo hit) {
            if (npc.life <= 0 && TheEndSunOnHitNum) {
                if (!BossRushEvent.BossRushActive) {
                    for (int i = 0; i < Main.rand.Next(16, 23); i++) {
                        npc.DropItem();
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

        public override void UpdateLifeRegen(NPC npc, ref int damage) {
            if (VoidErosionBool) {
                DebuffSet(10000, 8000, ref npc.lifeRegen, ref damage);
            }
            if (HellfireExplosion) {
                DebuffSet(1000, 80, ref npc.lifeRegen, ref damage);
            }
        }

        public void DebuffSet(int lifeRegenSet, int damageSet, ref int lifeRegen, ref int damage) {
            if (lifeRegen > 0) {
                lifeRegen = 0;
            }
            lifeRegen -= lifeRegenSet;
            if (damage < damageSet) {
                damage = damageSet;
            }
        }

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot) => TungstenRiot.Instance.ModifyEventNPCLoot(npc, ref npcLoot);

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

        public override void DrawEffects(NPC npc, ref Color drawColor) {
            if (VoidErosionBool) {
                drawColor.R = 100;
                VoidErosion.SpanStar(npc, CWRUtils.randVr(npc.width / 2));
            }
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (IceParclose) {
                float slp = npc.scale * (npc.height / (float)IceParcloseAsset.Value.Height) * 2;
                float sengs = 0.3f + Math.Abs(MathF.Sin(Main.GameUpdateCount * 0.1f) * 0.3f);
                spriteBatch.Draw(IceParcloseAsset.Value, npc.Center - Main.screenPosition, null, Color.White * sengs, 0, IceParcloseAsset.Value.Size() / 2, slp, SpriteEffects.None, 0);
            }
        }
    }
}
