using CalamityMod;
using CalamityMod.Events;
using CalamityMod.Items;
using CalamityMod.Items.Materials;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.NPCs.AquaticScourge;
using CalamityMod.NPCs.DesertScourge;
using CalamityMod.NPCs.OldDuke;
using CalamityMod.NPCs.SupremeCalamitas;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Items.Accessories;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Painting;
using CalamityOverhaul.Content.Items.Rogue;
using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.NPCs.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using static CalamityMod.DropHelper;

namespace CalamityOverhaul.Content
{
    public class CWRNpc : GlobalNPC
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
        /// <summary>
        /// 瘟疫攻击计数
        /// </summary>
        public byte ContagionOnHitNum = 0;
        /// <summary>
        /// 磷光拳套攻击计数
        /// </summary>
        public byte PhosphorescentGauntletOnHitNum = 0;
        /// <summary>
        /// 携序之刃攻击计数
        /// </summary>
        public byte OrderbringerOnHitNum = 0;
        /// <summary>
        /// 阳炎命中次数
        /// </summary>
        public bool TheEndSunOnHitNum;
        /// <summary>
        /// 鞭子击中次数
        /// </summary>
        public byte WhipHitNum = 0;
        /// <summary>
        /// 鞭子击中类型
        /// </summary>
        public byte WhipHitType = 0;
        /// <summary>
        /// 如果为<see langword="true"/>，将停止该NPC的大部分活动以模拟冻结效果
        /// </summary>
        public bool FrozenActivity;
        /// <summary>
        /// 一个特殊标记，用于朗基努斯识别目标
        /// </summary>
        public bool LonginusSign;
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
        /// <summary>
        /// 是否受到地狱炎爆debuff
        /// </summary>
        public bool HellfireExplosion;
        /// <summary>
        /// 是否受到虚空终结debuff
        /// </summary>
        public bool VoidErosionBool;
        /// <summary>
        /// 是否受到灵魂火debuff
        /// </summary>
        public bool SoulfireExplosion;
        #endregion

        public override GlobalNPC Clone(NPC from, NPC to) => CloneCWRNpc((CWRNpc)base.Clone(from, to));
        public CWRNpc CloneCWRNpc(CWRNpc cwr) {
            cwr.CreateHitPlayer = CreateHitPlayer;
            cwr.ContagionOnHitNum = ContagionOnHitNum;
            cwr.PhosphorescentGauntletOnHitNum = PhosphorescentGauntletOnHitNum;
            cwr.OrderbringerOnHitNum = OrderbringerOnHitNum;
            cwr.TheEndSunOnHitNum = TheEndSunOnHitNum;
            cwr.WhipHitNum = WhipHitNum;
            cwr.WhipHitType = WhipHitType;
            cwr.LonginusSign = LonginusSign;
            cwr.OverBeatBackBool = OverBeatBackBool;
            cwr.OverBeatBackVr = OverBeatBackVr;
            cwr.OverBeatBackAttenuationForce = OverBeatBackAttenuationForce;
            cwr.IceParclose = IceParclose;
            return cwr;
        }

        public static void OverBeatBackSend(NPC npc, float power = 0.99f) {
            if (VaultUtils.isClient) {
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
            if (VaultUtils.isServer) {
                var netMessage = CWRMod.Instance.GetPacket();
                netMessage.Write((byte)CWRMessageType.OverBeatBack);
                netMessage.Write((byte)npc.whoAmI);
                netMessage.WriteVector2(npc.CWR().OverBeatBackVr);
                netMessage.Write(power);
                netMessage.Send(-1, whoAmI);
            }
        }

        public override void ResetEffects(NPC npc) {
            IceParclose = false;
            VoidErosionBool = false;
            HellfireExplosion = false;
            SoulfireExplosion = false;
            FrozenActivity = false;
        }

        public override void SetDefaults(NPC npc) {
            NPCOverride.SetDefaults(npc);
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

        public override void BossHeadSlot(NPC npc, ref int index) => NPCOverride.BossHeadSlot(ref index);

        public override void BossHeadRotation(NPC npc, ref float rotation) => NPCOverride.BossHeadRotation(ref rotation);

        public override bool PreAI(NPC npc) {
            UpdateOverBeatBack(npc);
            return base.PreAI(npc);
        }

        public override void PostAI(NPC npc) {
            if (!VaultUtils.isClient) {
                if (WhipHitNum > 10) {
                    WhipHitNum = 10;
                }
            }
        }

        public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers) {
            NPCOverride.ModifyHitByItem(player, item, ref modifiers);
        }

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers) {
            NPCOverride.ModifyHitByProjectile(projectile, ref modifiers);
        }

        public override bool CheckActive(NPC npc) => NPCOverride.CheckActive();

        public override bool SpecialOnKill(NPC npc) {
            if (npc.type == CWRLoad.AstrumDeusHead) {
                // 啊，经典的星神游龙，每次都会让电脑死机，简直像是回到了1999年
                // 真是不可思议，怎么这么多年过去了，原灾厄那个bug竟然还没修复？
                // 写这个Boss的程序员脑袋肯定有问题，性能优化根本不在他们的词典里
                // 一个Boss上百个体节，每个体节每帧受伤好几十次，而每次伤害都要调用
                // DropHelper.FindClosestWormSegment——这个方法居然要遍历200个NPC
                // 哇，n³复杂度，绝对是天才级别的优化
                return false;
            }
            return base.SpecialOnKill(npc);
        }

        public override bool PreKill(NPC npc) {
            if (ContagionOnHitNum > 0 && CreateHitPlayer != null) {
                if (Main.myPlayer == CreateHitPlayer.whoAmI && CreateHitPlayer.ownedProjectileCounts[ModContent.ProjectileType<NurgleSoul>()] <= 13) {
                    Projectile proj = Projectile.NewProjectileDirect(CreateHitPlayer.FromObjectGetParent(), npc.Center, CWRUtils.randVr(13)
                        , ModContent.ProjectileType<NurgleSoul>(), npc.damage, 2, CreateHitPlayer.whoAmI);
                    proj.scale = (npc.width / proj.width) * npc.scale;
                }
            }
            return base.PreKill(npc);
        }

        public override void OnKill(NPC npc) {
            if (!VaultUtils.isClient) {
                if (npc.boss) {
                    if (CWRLoad.targetNpcTypes7.Contains(npc.type) || npc.type == CWRLoad.PlaguebringerGoliath) {
                        for (int i = 0; i < Main.rand.Next(3, 6); i++) {
                            int type = Item.NewItem(npc.FromObjectGetParent(), npc.Hitbox, ModContent.ItemType<DubiousPlating>(), Main.rand.Next(7, 13));
                            if (!VaultUtils.isSinglePlayer) {
                                NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
                            }
                        }
                    }
                }
            }

            if (Main.rand.NextBool(JusticeUnveiled.DropProbabilityDenominator) || (npc.type == NPCID.Spazmatism && Main.LocalPlayer.ZoneSkyHeight)) {
                int type = Item.NewItem(npc.FromObjectGetParent(), npc.Hitbox, ModContent.ItemType<JusticeUnveiled>());
                if (!VaultUtils.isSinglePlayer) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
                }
            }
            if (Main.rand.NextBool(WUTIVSelfPortrait.DropProbabilityDenominator)) {
                int type = Item.NewItem(npc.FromObjectGetParent(), npc.Hitbox, ModContent.ItemType<WUTIVSelfPortrait>());
                if (!VaultUtils.isSinglePlayer) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
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
                        int type = Item.NewItem(npc.FromObjectGetParent(), npc.Hitbox, ModContent.ItemType<Rock>());
                        if (VaultUtils.isClient) {
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
            if (SoulfireExplosion) {
                DebuffSet(1000, 80, ref npc.lifeRegen, ref damage);
            }
        }

        public static void DebuffSet(int lifeRegenSet, int damageSet, ref int lifeRegen, ref int damage) {
            if (lifeRegen > 0) {
                lifeRegen = 0;
            }
            lifeRegen -= lifeRegenSet;
            if (damage < damageSet) {
                damage = damageSet;
            }
        }

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot) {
            IItemDropRuleCondition dontExpertCondition = new Conditions.NotExpert();
            LeadingConditionRule dontExpertRule = new LeadingConditionRule(dontExpertCondition);

            if (npc.type == NPCID.TombCrawlerHead) {
                npcLoot.RemoveWhere(rule => true);
                npcLoot.Add(3380, 1, 2, 6);
            }
            else if (npc.type == ModContent.NPCType<SupremeCalamitas>()) {
                npcLoot.Add(ModContent.ItemType<CalSelfPortrait>(), 20);//5%概率掉落自画像
            }
            else if (npc.type == ModContent.NPCType<DesertScourgeHead>()) {
                dontExpertRule.Add(ModContent.ItemType<UnderTheSand>(), 10);
                dontExpertRule.Add(ModContent.ItemType<WastelandFang>(), 10);
                dontExpertRule.Add(ModContent.ItemType<SandDagger>(), 10);
                dontExpertRule.Add(ModContent.ItemType<BurntSienna>(), 10);
                npcLoot.Add(dontExpertRule);
            }
            else if (npc.type == ModContent.NPCType<AquaticScourgeHead>()) {
                dontExpertRule.Add(ModContent.ItemType<MelodyTheSand>(), 6);
                npcLoot.Add(dontExpertRule);
            }
            else if (npc.type == ModContent.NPCType<OldDuke>()) {
                dontExpertRule.Add(ModContent.ItemType<SandVortexOfTheDecayedSea>(), 6);
                npcLoot.Add(dontExpertRule);
            }
            //不要用TryFetchByID或者直接访问NPCOverride
            if (NPCSystem.IDToNPCSetDic.TryGetValue(npc.type, out var npcOverride)) {
                npcOverride.ModifyNPCLoot(npc, npcLoot);
            }
        }

        public override void ModifyShop(NPCShop shop) {
            if (shop.NpcType == NPCID.Clothier) {//裁缝将会售卖娃娃
                shop.Add(ModContent.ItemType<HandmadeDoll>());
            }
            if (shop.NpcType == NPCID.Merchant) {
                shop.Add(ItemID.WormholePotion, Condition.Multiplayer);//商人会在多人模式下售卖传送药水
                shop.Add(ItemID.RecallPotion, Condition.DownedEyeOfCthulhu);//击败克苏鲁之眼后售卖回忆药水
                shop.Add(ItemID.PotionOfReturn, Condition.Hardmode);//击败血肉墙之眼后售卖返回药水
            }
            foreach (AbstractNPCShop.Entry shopEntity in shop.Entries) {
                Item item = shopEntity.Item;
                if (item == null || item.type <= ItemID.None) {
                    continue;
                }
                Item newItem = new Item(item.type);
                CWRItems cwrItem = newItem.CWR();
                if (cwrItem.HasCartridgeHolder || cwrItem.heldProjType > 0 || cwrItem.isHeldItem) {
                    item.SetDefaults(item.type);
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
                Texture2D value = CWRAsset.IceParcloseAsset.Value;
                float slp = npc.scale * (npc.height / (float)value.Height) * 2;
                float sengs = 0.3f + Math.Abs(MathF.Sin(Main.GameUpdateCount * 0.1f) * 0.3f);
                spriteBatch.Draw(value, npc.Center - Main.screenPosition, null, Color.White * sengs, 0, value.Size() / 2, slp, SpriteEffects.None, 0);
            }
        }

        public override void ChatBubblePosition(NPC npc, ref Vector2 position, ref SpriteEffects spriteEffects) {
            if (CWRPlayer.CanTimeFrozen()) {
                position = new Vector2(-200, -200);
            }
            base.ChatBubblePosition(npc, ref position, ref spriteEffects);
        }
        public override void EmoteBubblePosition(NPC npc, ref Vector2 position, ref SpriteEffects spriteEffects) {
            if (CWRPlayer.CanTimeFrozen()) {
                position = new Vector2(-200, -200);
            }
            base.EmoteBubblePosition(npc, ref position, ref spriteEffects);
        }
        public override bool? DrawHealthBar(NPC npc, byte hbPosition, ref float scale, ref Vector2 position) {
            if (CWRPlayer.CanTimeFrozen()) {
                return false;
            }
            return base.DrawHealthBar(npc, hbPosition, ref scale, ref position);
        }
    }
}
