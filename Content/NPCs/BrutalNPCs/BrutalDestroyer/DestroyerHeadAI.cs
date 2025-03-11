using CalamityMod;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Summon;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.NPCs.Core;
using CalamityOverhaul.Content.RemakeItems.ModifyBag;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.IO;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer
{
    internal class DestroyerHeadAI : NPCOverride, ICWRLoader
    {
        public override int TargetID => NPCID.TheDestroyer;
        private const int maxFindMode = 20000 * 20000;
        private int frame;
        private int glowFrame;
        private bool openMouth;
        private int dontOpenMouthTime;
        private Player player;
        internal static Asset<Texture2D> HeadIcon;
        internal static Asset<Texture2D> Head;
        internal static Asset<Texture2D> Head_Glow;
        internal static int iconIndex;
        internal static int iconIndex_Void;
        internal const int StretchTime = 300;
        private int time;
        private Vector2 dashVer;
        void ICWRLoader.LoadData() {
            CWRMod.Instance.AddBossHeadTexture(CWRConstant.NPC + "BTD/BTD_Head", -1);
            iconIndex = ModContent.GetModBossHeadSlot(CWRConstant.NPC + "BTD/BTD_Head");
            CWRMod.Instance.AddBossHeadTexture(CWRConstant.Placeholder, -1);
            iconIndex_Void = ModContent.GetModBossHeadSlot(CWRConstant.Placeholder);
        }

        void ICWRLoader.LoadAsset() {
            Head = CWRUtils.GetT2DAsset(CWRConstant.NPC + "BTD/Head");
            Head_Glow = CWRUtils.GetT2DAsset(CWRConstant.NPC + "BTD/Head_Glow");
            HeadIcon = CWRUtils.GetT2DAsset(CWRConstant.NPC + "BTD/BTD_Head");
        }
        void ICWRLoader.UnLoadData() {
            Head = null;
            Head_Glow = null;
        }

        public override void OtherNetWorkSend(ModPacket netMessage) {
            netMessage.WriteVector2(dashVer);
        }

        public override void OtherNetWorkReceive(BinaryReader reader) {
            dashVer = reader.ReadVector2();
        }

        public override void SetProperty() {
            if (CWRWorld.MachineRebellion) {
                npc.life = npc.lifeMax *= 32;
                npc.defDefense = npc.defense = 40;
                npc.defDamage = npc.damage *= 3;
            }
        }

        public override bool? CanOverride() {
            if (CWRWorld.MachineRebellion) {
                return true;
            }
            return base.CanOverride();
        }

        public override void BossHeadSlot(ref int index) {
            if (!HeadPrimeAI.DontReform()) {
                index = iconIndex_Void;
            }
        }

        public override void BossHeadRotation(ref float rotation) => rotation = npc.rotation + MathHelper.Pi;

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            IItemDropRuleCondition condition = new DropInDeathMode();
            LeadingConditionRule rule = new LeadingConditionRule(condition);
            rule.Add(ModContent.ItemType<DestroyersBlade>(), 4);
            rule.Add(ModContent.ItemType<StaffoftheDestroyer>(), 4);
            rule.Add(ModContent.ItemType<Observer>(), 4);
            rule.Add(ModContent.ItemType<ForgedLash>(), 4);
            npcLoot.Add(rule);
        }

        private static void SendBodyNetWork() {
            int npcCount = 0;
            foreach (var npc in Main.ActiveNPCs) {
                if (npc.type != NPCID.TheDestroyerBody && npc.type != NPCID.TheDestroyerTail) {
                    continue;
                }
                npcCount++;
            }

            ModPacket modPacket = CWRMod.Instance.GetPacket();

            modPacket.Write((byte)CWRMessageType.DestroyerData);

            modPacket.Write(npcCount);

            foreach (var npc in Main.ActiveNPCs) {
                if (npc.type != NPCID.TheDestroyerBody && npc.type != NPCID.TheDestroyerTail) {
                    continue;
                }
                modPacket.Write(npc.whoAmI);
                modPacket.WriteVector2(npc.position);
                modPacket.Write(npc.rotation);
            }

            modPacket.Send();//发送给所有客户端
        }

        internal static void HandlerBodyNetWork(BinaryReader reader) {
            int npcCount = reader.ReadInt32();
            for (int i = 0; i < npcCount; i++) {
                int whoAmI = reader.ReadInt32();
                Vector2 npcPos = reader.ReadVector2();
                float npcRot = reader.ReadSingle();
                NPC body = CWRUtils.GetNPCInstance(whoAmI);
                if (body != null && (body.type == NPCID.TheDestroyerBody || body.type == NPCID.TheDestroyerTail)) {
                    body.position = npcPos;
                    body.rotation = npcRot;
                }
            }
        }

        public override bool AI() {
            time++;

            if (VaultUtils.isServer && time % 300 == 0) {//在多人模式下，每间隔5秒发送一次体节的信息
                SendBodyNetWork();
            }

            npc.timeLeft = 1800;//愚蠢的自然脱战

            if (ai[0] == 0) {
                if (!HeadPrimeAI.DontReform() && !VaultUtils.isClient) {
                    NPC.NewNPCDirect(npc.FromObjectGetParent(), npc.Center
                        , ModContent.NPCType<DestroyerDrawHeadIconNPC>(), 0, npc.whoAmI);
                }
                ai[0] = 1;
            }

            if (npc.target < 0 || npc.target >= 255) {
                npc.FindClosestPlayer();
                player = Main.player[npc.target];
            }
            if (!player.Alives() || player.DistanceSQ(npc.Center) > maxFindMode) {
                npc.FindClosestPlayer();
                player = Main.player[npc.target];
                if (!player.Alives() || player.DistanceSQ(npc.Center) > maxFindMode) {
                    npc.ai[0] = 99;
                }
            }

            HandleMouth();

            //这里判定一个时间进行冲刺，用于展开体节，实际冲刺的时间需要比预定的展开时间长一些
            if (time < StretchTime + 60 && time > 10) {
                if (dashVer == Vector2.Zero) {
                    dashVer = npc.Center.To(player.Center).UnitVector();
                    netOtherWorkSend = true;
                }
                npc.velocity = dashVer * 32;
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                return false;
            }

            if (CWRWorld.MachineRebellion) {
                MachineRebellionAI();
                return false;
            }

            return true;
        }

        internal static void SpawnBody(NPC npc) {
            // 生成毁灭者身体的多个部分
            if (!VaultUtils.isClient) {
                int index = npc.whoAmI;
                for (int i = 0; i < 88; i++) {
                    index = NPC.NewNPC(npc.FromObjectGetParent(), (int)npc.Center.X, (int)npc.Center.Y
                        , i == 87 ? NPCID.TheDestroyerTail : NPCID.TheDestroyerBody, 0, 0, index);
                    Main.npc[index].realLife = npc.whoAmI;
                    Main.npc[index].netUpdate = true;
                }
            }

            foreach (var body in Main.ActiveNPCs) {
                if (body.type == NPCID.TheDestroyerBody || body.type == NPCID.TheDestroyerTail) {
                    SetDefaults(body, body.CWR(), body.Calamity());
                }
            }
        }

        private void MachineRebellionAI() {
            if (npc.ai[0] == 99) {
                npc.velocity = new Vector2(0, 56);
                if (++ai[0] > 280) {
                    npc.active = false;
                }
                return;
            }
            // 初始化时进行冲刺操作
            if (npc.ai[0] == 0) {
                npc.ai[0] = 1;
                SpawnBody(npc);
                npc.netUpdate = true;
            }

            if (--ai[4] > 0) {
                npc.VanillaAI();
                if (npc.Distance(player.Center) > maxFindMode / 4) {//如果发现跑远了就里面切换阶段向玩家冲刺回来
                    dashVer = npc.Center.To(player.Center).UnitVector();
                    netOtherWorkSend = true;
                    ai[4] = 0;
                    ai[2] = 2;
                    NetAISend();
                }
            }
            else {
                if (ai[2] == 0) {
                    if (++ai[1] > 120) {
                        ai[1] = 0;
                        ai[2] = 1;
                        NetAISend();
                    }
                }

                if (ai[2] == 1) {
                    dashVer = npc.Center.To(player.Center).UnitVector();
                    netOtherWorkSend = true;
                    ai[2] = 2;
                    NetAISend();
                }

                if (ai[2] == 2) {
                    npc.velocity = dashVer * 44;
                    if (npc.Distance(player.Center) > maxFindMode / 4) {
                        dashVer = npc.Center.To(player.Center).UnitVector();
                        netOtherWorkSend = true;
                    }
                    if (++ai[3] > 180) {
                        ai[4] = 300;
                        ai[3] = 0;
                        ai[2] = 0;
                        ai[1] = 0;
                        NetAISend();
                    }
                }
            }
        }

        private void HandleMouth() {
            CWRUtils.ClockFrame(ref glowFrame, 5, 3);

            float dotProduct = Vector2.Dot(npc.velocity.UnitVector(), npc.Center.To(player.Center).UnitVector());
            float toPlayerLang = npc.Distance(player.Center);
            if (toPlayerLang < 660 && toPlayerLang > 100 && dotProduct > 0.8f) {
                if (dontOpenMouthTime <= 0) {
                    openMouth = true;
                }
            }
            else {
                openMouth = false;
            }

            if (openMouth) {
                if (frame < 3) {
                    frame++;
                }
                dontOpenMouthTime = 120;
            }
            else {
                if (frame > 0) {
                    frame--;
                }
            }

            if (dontOpenMouthTime > 0) {
                dontOpenMouthTime--;
            }
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (HeadPrimeAI.DontReform()) {
                return true;
            }

            Texture2D value = Head.Value;
            Rectangle rectangle = CWRUtils.GetRec(value, frame, 4);
            Rectangle glowRectangle = CWRUtils.GetRec(value, glowFrame, 4);

            spriteBatch.Draw(value, npc.Center - Main.screenPosition
                , rectangle, drawColor, npc.rotation + MathHelper.Pi, rectangle.Size() / 2, npc.scale, SpriteEffects.None, 0);

            if (time >= StretchTime) {
                Texture2D value2 = Head_Glow.Value;
                spriteBatch.Draw(value2, npc.Center - Main.screenPosition
                    , glowRectangle, Color.White, npc.rotation + MathHelper.Pi, glowRectangle.Size() / 2, npc.scale, SpriteEffects.None, 0);
            }

            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return HeadPrimeAI.DontReform();
        }
    }
}
