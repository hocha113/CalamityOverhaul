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
using CalamityOverhaul.Content.Items.Placeable;
using CalamityOverhaul.Content.Items.Rogue;
using CalamityOverhaul.Content.Items.Tools;
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
        public byte PhosphorescentGauntletHitCount = 0;
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
        /// <summary>
        /// 受到的染料物品ID
        /// </summary>
        public int DyeItemID;
        /// <summary>
        /// 如果大于0，则表示该NPC处于虚弱时间状态
        /// </summary>
        public int IsWeakTime;
        #endregion

        public override GlobalNPC Clone(NPC from, NPC to) => CloneCWRNpc((CWRNpc)base.Clone(from, to));
        public CWRNpc CloneCWRNpc(CWRNpc cwr) {
            cwr.CreateHitPlayer = CreateHitPlayer;
            cwr.ContagionOnHitNum = ContagionOnHitNum;
            cwr.PhosphorescentGauntletHitCount = PhosphorescentGauntletHitCount;
            cwr.OrderbringerOnHitNum = OrderbringerOnHitNum;
            cwr.TheEndSunOnHitNum = TheEndSunOnHitNum;
            cwr.WhipHitNum = WhipHitNum;
            cwr.WhipHitType = WhipHitType;
            cwr.LonginusSign = LonginusSign;
            cwr.IceParclose = IceParclose;
            return cwr;
        }

        /// <summary>
        /// 接收NPC基本数据
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="whoAmI"></param>
        public static void NPCbasicDataHandler(BinaryReader reader) {
            int whoAmI = reader.ReadByte();
            Vector2 pos = reader.ReadVector2();
            float rot = reader.ReadSingle();

            if (!whoAmI.TryGetNPC(out NPC npc)) {
                return;
            }

            npc.position = pos;
            npc.rotation = rot;

            if (!VaultUtils.isServer) {
                return;
            }

            ModPacket modPacket = CWRMod.Instance.GetPacket();
            modPacket.Write((byte)CWRMessageType.NPCbasicData);
            modPacket.Write((byte)npc.whoAmI);
            modPacket.WriteVector2(npc.position);
            modPacket.Write(npc.rotation);
            modPacket.Send();
        }

        public override void ResetEffects(NPC npc) {
            IceParclose = false;
            VoidErosionBool = false;
            HellfireExplosion = false;
            SoulfireExplosion = false;
            FrozenActivity = false;
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

        public static void DoTimeFrozen(NPC npc) {
            npc.timeLeft++;
            npc.aiAction = 0;
            npc.frameCounter = 0;
            npc.velocity = Vector2.Zero;
            npc.position = npc.oldPosition;
            npc.direction = npc.oldDirection;
        }

        public override bool PreAI(NPC npc) {
            if (CWRWorld.CanTimeFrozen() || FrozenActivity) {
                DoTimeFrozen(npc);
                return false;
            }
            if (IsWeakTime > 0) {
                IsWeakTime--;
            }
            return base.PreAI(npc);
        }

        public override void PostAI(NPC npc) {
            if (!VaultUtils.isClient) {
                if (WhipHitNum > 10) {
                    WhipHitNum = 10;
                }
            }
        }

        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot) {
            if (IsWeakTime > 0) {
                return false;
            }
            return true;
        }

        public override bool SpecialOnKill(NPC npc) {
            if (npc.type == CWRLoad.AstrumDeusHead) {
                //经典的星神游龙，每次都会让电脑死机，简直像是回到了1999年
                //怎么这么多年过去了，原灾厄那个bug竟然还没修复
                //写这个Boss的脑袋肯定有问题
                //一个Boss上百个体节，每个体节每帧受伤好几十次，而每次伤害都要调用
                //DropHelper.FindClosestWormSegment——这个方法居然要遍历200个NPC
                //n³复杂度，天才级别的优化
                return false;
            }
            return base.SpecialOnKill(npc);
        }

        public override bool PreKill(NPC npc) {
            if (ContagionOnHitNum > 0 && CreateHitPlayer != null) {
                if (Main.myPlayer == CreateHitPlayer.whoAmI && CreateHitPlayer.ownedProjectileCounts[ModContent.ProjectileType<NurgleSoul>()] <= 13) {
                    Projectile proj = Projectile.NewProjectileDirect(CreateHitPlayer.FromObjectGetParent(), npc.Center, VaultUtils.RandVr(13)
                        , ModContent.ProjectileType<NurgleSoul>(), npc.damage, 2, CreateHitPlayer.whoAmI);
                    proj.scale = (npc.width / proj.width) * npc.scale;
                }
            }
            return base.PreKill(npc);
        }

        public override void OnKill(NPC npc) {
            if (VaultUtils.isClient) {
                return;
            }

            if (npc.boss && CWRLoad.targetNpcTypes7.Contains(npc.type) || npc.type == CWRLoad.PlaguebringerGoliath) {
                for (int i = 0; i < Main.rand.Next(3, 6); i++) {
                    int type = Item.NewItem(npc.FromObjectGetParent(), npc.Hitbox, ModContent.ItemType<DubiousPlating>(), Main.rand.Next(7, 13));
                    if (!VaultUtils.isSinglePlayer) {
                        NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
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
            if (Main.rand.NextBool(HoChaMeditatorItem.DropProbabilityDenominator)) {
                int type = Item.NewItem(npc.FromObjectGetParent(), npc.Hitbox, ModContent.ItemType<HoChaMeditatorItem>());
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
                CWRItem cwrItem = newItem.CWR();
                if (cwrItem.HasCartridgeHolder || cwrItem.heldProjType > 0 || cwrItem.isHeldItem) {
                    item.SetDefaults(item.type);
                }
            }
        }

        public override void DrawEffects(NPC npc, ref Color drawColor) {
            if (IsWeakTime > 0) {
                drawColor = Color.Lerp(drawColor, Color.BlueViolet, 0.4f);//虚弱时间时，NPC会变成蓝紫色
            }
            if (VoidErosionBool) {
                drawColor.R = 100;
                VoidErosion.SpanStar(npc, VaultUtils.RandVr(npc.width / 2));
            }
        }

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (DyeItemID > 0) {
                npc.BeginDyeEffectForWorld(DyeItemID);
            }
            return true;
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (IceParclose) {
                Texture2D value = CWRAsset.IceParcloseAsset.Value;
                float slp = npc.scale * (npc.height / (float)value.Height) * 2;
                float sengs = 0.3f + Math.Abs(MathF.Sin(Main.GameUpdateCount * 0.1f) * 0.3f);
                spriteBatch.Draw(value, npc.Center - Main.screenPosition, null, Color.White * sengs, 0, value.Size() / 2, slp, SpriteEffects.None, 0);
            }
            if (DyeItemID > 0) {
                npc.EndDyeEffectForWorld();
            }
        }

        public override void ChatBubblePosition(NPC npc, ref Vector2 position, ref SpriteEffects spriteEffects) {
            if (CWRWorld.CanTimeFrozen()) {
                position = new Vector2(-200, -200);
            }
            base.ChatBubblePosition(npc, ref position, ref spriteEffects);
        }
        public override void EmoteBubblePosition(NPC npc, ref Vector2 position, ref SpriteEffects spriteEffects) {
            if (CWRWorld.CanTimeFrozen()) {
                position = new Vector2(-200, -200);
            }
            base.EmoteBubblePosition(npc, ref position, ref spriteEffects);
        }
        public override bool? DrawHealthBar(NPC npc, byte hbPosition, ref float scale, ref Vector2 position) {
            if (CWRWorld.CanTimeFrozen()) {
                return false;
            }
            return base.DrawHealthBar(npc, hbPosition, ref scale, ref position);
        }
    }
}
