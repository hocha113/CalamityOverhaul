using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Items.Accessories;
using CalamityOverhaul.Content.Items.Accessories.JusticeUnveileds;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Painting;
using CalamityOverhaul.Content.Items.Placeable;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Items.Tools;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content
{
    public class CWRNpc : GlobalNPC
    {
        #region Data
        public override bool InstancePerEntity => true;
        /// <summary>&gt;0 冻结活动</summary>
        public int TimeFrozenTick;
        /// <summary>朗基努斯目标标记</summary>
        public bool LonginusSign;
        /// <summary>极寒神性屏障</summary>
        public bool IceParclose;
        /// <summary>地狱炎爆</summary>
        public bool HellfireExplosion;
        /// <summary>虚空终结</summary>
        public bool VoidErosionBool;
        /// <summary>灵魂火</summary>
        public bool SoulfireExplosion;
        /// <summary>染料物品 ID</summary>
        public int DyeItemID;
        /// <summary>&gt;0 虚弱中</summary>
        public int IsWeakTime;
        private bool timeFreezeSnapshotCaptured;
        private int timeFreezeSnapshotType = NPCID.None;
        private Vector2 timeFreezePosition;
        private Vector2 timeFreezeVelocity;
        #endregion

        public override GlobalNPC Clone(NPC from, NPC to) => CloneCWRNpc((CWRNpc)base.Clone(from, to));
        public CWRNpc CloneCWRNpc(CWRNpc cwr) {
            cwr.LonginusSign = LonginusSign;
            cwr.IceParclose = IceParclose;
            cwr.ClearTimeFreezeSnapshot();
            return cwr;
        }

        public override void SetDefaults(NPC npc) {
            TimeFrozenTick = 0;
            ClearTimeFreezeSnapshot();
        }

        /// <summary>接收 NPC 基本数据</summary>
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
        }

        public static void MultipleSegmentsLimitDamage(NPC target, ref NPC.HitModifiers modifiers) {
            if (CWRLoad.DestroyerSegments.Contains(target.type) || CWRLoad.AstrumDeusSegments.Contains(target.type)
                || CWRLoad.DevourerofGodsSegments.Contains(target.type) || CWRLoad.ExoMechSegments.Contains(target.type)
                || CWRLoad.ArmoredDiggerSegments.Contains(target.type) || CWRLoad.PerforatorMediumSegments.Contains(target.type)
                || CWRLoad.PerforatorLargeSegments.Contains(target.type) || CWRLoad.StormWeaverSegments.Contains(target.type)
                || CWRLoad.WormBodys.Contains(target.type) || target.type == CWRID.NPC_AquaticScourgeBodyAlt) {
                modifiers.FinalDamage *= 0.1f;
                int dmownInt = (int)(target.lifeMax * 0.001f);
                if (dmownInt < 50) {
                    dmownInt = 50;
                }
                modifiers.SetMaxDamage(dmownInt + Main.rand.Next(50));
            }
        }

        private static bool IsFinite(Vector2 value)
            => float.IsFinite(value.X) && float.IsFinite(value.Y);

        private void ClearTimeFreezeSnapshot() {
            timeFreezeSnapshotCaptured = false;
            timeFreezeSnapshotType = NPCID.None;
            timeFreezePosition = Vector2.Zero;
            timeFreezeVelocity = Vector2.Zero;
        }

        private void CaptureTimeFreezeSnapshot(NPC npc) {
            if (timeFreezeSnapshotCaptured && timeFreezeSnapshotType == npc.type) {
                return;
            }

            ClearTimeFreezeSnapshot();
            timeFreezeSnapshotCaptured = true;
            timeFreezeSnapshotType = npc.type;
            timeFreezePosition = IsFinite(npc.position)
                ? npc.position
                : IsFinite(npc.oldPosition) ? npc.oldPosition : Vector2.Zero;
            timeFreezeVelocity = IsFinite(npc.velocity) ? npc.velocity : Vector2.Zero;
        }

        internal void ApplyTimeFreeze(NPC npc) {
            CaptureTimeFreezeSnapshot(npc);
            npc.timeLeft++;
            npc.aiAction = 0;
            npc.frameCounter = 0;
            npc.velocity = Vector2.Zero;
            npc.position = timeFreezePosition;
            npc.direction = npc.oldDirection;
        }

        internal void RestoreTimeFreeze(NPC npc) {
            if (!timeFreezeSnapshotCaptured) {
                return;
            }

            if (timeFreezeSnapshotType == npc.type) {
                if (!IsFinite(npc.position) && IsFinite(timeFreezePosition)) {
                    npc.position = timeFreezePosition;
                }
                npc.velocity = IsFinite(timeFreezeVelocity) ? timeFreezeVelocity : Vector2.Zero;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    npc.netUpdate = true;
                }
            }

            ClearTimeFreezeSnapshot();
        }

        public static void DoTimeFrozen(NPC npc) => npc.CWR().ApplyTimeFreeze(npc);

        public override bool PreAI(NPC npc) {
            if (IsWeakTime > 0) {
                IsWeakTime--;
            }
            return base.PreAI(npc);
        }

        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot) {
            if (IsWeakTime > 0) {
                return false;
            }
            return true;
        }

        public override bool SpecialOnKill(NPC npc) {
            if (npc.type == CWRID.NPC_AstrumDeusHead) {
                //星神游龙跳过 SpecialOnKill，原灾 DropHelper.FindClosestWormSegment 每段每击扫全 NPC 会卡死
                return false;
            }
            return base.SpecialOnKill(npc);
        }

        public override void OnKill(NPC npc) {
            ClearTimeFreezeSnapshot();
            if (VaultUtils.isClient) {
                return;
            }

            if (npc.boss && CWRLoad.ExoMechSegments.Contains(npc.type) || npc.type == CWRID.NPC_PlaguebringerGoliath) {
                for (int i = 0; i < Main.rand.Next(3, 6); i++) {
                    int type = Item.NewItem(npc.FromObjectGetParent(), npc.Hitbox, CWRID.Item_DubiousPlating, Main.rand.Next(7, 13));
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

            if (npc.type == CWRID.NPC_PrimordialWyrmHead && !CWRRef.GetDownedPrimordialWyrm()) {//原灾未写 downed，补一次进度
                CWRRef.SetDownedPrimordialWyrm(true);
                if (Main.dedServ) {
                    NetMessage.SendData(MessageID.WorldData);
                }
            }
        }

        public override void UpdateLifeRegen(NPC npc, ref int damage) {
            if (VoidErosionBool) {
                DebuffSet(10000, 8000, ref npc.lifeRegen, ref damage);
            }
            if (HellfireExplosion) {
                DebuffSet(160, 20, ref npc.lifeRegen, ref damage);
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
                npcLoot.SimpleAdd(3380, 1, 2, 6);
            }
            else if (npc.type == CWRID.NPC_SupremeCalamitas) {
                npcLoot.SimpleAdd(ModContent.ItemType<CalSelfPortrait>(), 10);//10%
            }
            else if (npc.type == CWRID.NPC_DesertScourgeHead) {
                dontExpertRule.SimpleAdd(ModContent.ItemType<UnderTheSand>(), 10);
                dontExpertRule.SimpleAdd(ModContent.ItemType<WastelandFang>(), 10);
                dontExpertRule.SimpleAdd(ModContent.ItemType<SandDagger>(), 10);
                dontExpertRule.SimpleAdd(ModContent.ItemType<DuneStalker>(), 10);
                npcLoot.Add(dontExpertRule);
            }
            else if (npc.type == CWRID.NPC_AquaticScourgeHead) {
                dontExpertRule.SimpleAdd(ModContent.ItemType<MelodyTheSand>(), 6);
                npcLoot.Add(dontExpertRule);
            }
            else if (npc.type == CWRID.NPC_OldDuke) {
                dontExpertRule.SimpleAdd(ModContent.ItemType<SandVortexOfTheDecayedSea>(), 6);
                npcLoot.Add(dontExpertRule);
            }
            else if (npc.type == NPCID.WallofFlesh) {
                npcLoot.SimpleAdd(ModContent.ItemType<Arbiter>(), 1);//必掉断罪师
            }
        }

        public override void ModifyShop(NPCShop shop) {
            if (shop.NpcType == NPCID.Clothier) {//娃娃
                shop.Add(ModContent.ItemType<HandmadeDoll>());
            }
            if (shop.NpcType == NPCID.Merchant) {
                shop.Add(ItemID.WormholePotion, Condition.Multiplayer);
                shop.Add(ItemID.RecallPotion, Condition.DownedEyeOfCthulhu);
                shop.Add(ItemID.PotionOfReturn, Condition.Hardmode);
            }
            if (shop.NpcType == CWRID.NPC_THIEF) {
                shop.Add(ModContent.ItemType<Unsunghero>(), Condition.Hardmode);
            }
            foreach (AbstractNPCShop.Entry shopEntity in shop.Entries) {
                Item item = shopEntity.Item;
                if (item == null || item.type <= ItemID.None) {
                    continue;
                }
                Item newItem = new Item(item.type);
                CWRItem cwrItem = newItem.CWR();
                if (cwrItem.heldProjType > 0 || cwrItem.isHeldItem) {
                    item.SetDefaults(item.type);
                }
            }
        }

        public override void DrawEffects(NPC npc, ref Color drawColor) {
            if (IsWeakTime > 0) {
                drawColor = Color.Lerp(drawColor, Color.BlueViolet, 0.4f);//虚弱蓝紫
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

        /// <summary>强制指定 NPC 掉落并死亡</summary>
        public static void SetNPCLoot(int npcID) {
            if (VaultUtils.isClient) {
                ModPacket modPacket = CWRMod.Instance.GetPacket();
                modPacket.Write((byte)CWRMessageType.SetNPCLoot);
                modPacket.Write(npcID);
                modPacket.Send();
                return;
            }
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == npcID) {
                    n.active = false;
                    n.netUpdate = true;
                    n.NPCLoot();
                }
            }
        }
        public static void HandleSetNPCLoot(BinaryReader reader, int whoAmI) {
            int npcID = reader.ReadInt32();
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == npcID) {
                    n.active = false;
                    n.netUpdate = true;
                    n.NPCLoot();
                }
            }
            if (VaultUtils.isServer) {
                ModPacket modPacket = CWRMod.Instance.GetPacket();
                modPacket.Write((byte)CWRMessageType.SetNPCLoot);
                modPacket.Write(npcID);
                modPacket.Send(-1, whoAmI);
            }
        }
    }
}
