using CalamityMod;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Summon;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.NPCs.Core;
using CalamityOverhaul.Content.RemakeItems.ModifyBag;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

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
        internal static Asset<Texture2D> Head;
        internal static Asset<Texture2D> Head_Glow;
        internal static int iconIndex;
        internal static int iconIndex_Void;
        void ICWRLoader.LoadData() {
            CWRMod.Instance.AddBossHeadTexture(CWRConstant.NPC + "BTD/BTD_Head", -1);
            iconIndex = ModContent.GetModBossHeadSlot(CWRConstant.NPC + "BTD/BTD_Head");
            CWRMod.Instance.AddBossHeadTexture(CWRConstant.Placeholder, -1);
            iconIndex_Void = ModContent.GetModBossHeadSlot(CWRConstant.Placeholder);
        }

        void ICWRLoader.LoadAsset() {
            Head = CWRUtils.GetT2DAsset(CWRConstant.NPC + "BTD/Head");
            Head_Glow = CWRUtils.GetT2DAsset(CWRConstant.NPC + "BTD/Head_Glow");
        }
        void ICWRLoader.UnLoadData() {
            Head = null;
            Head_Glow = null;
        }

        public static void SetMachineRebellion(NPC npc) {
            npc.life = npc.lifeMax *= 22;
            npc.defDefense = npc.defense = 80;
            npc.defDamage = npc.damage *= 2;
        }

        public override void SetProperty() {
            if (CWRWorld.MachineRebellion) {
                npc.life = npc.lifeMax *= 22;
                npc.defDefense = npc.defense = 40;
                npc.defDamage = npc.damage *= 2;
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

        public override bool AI() {
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
            }

            // 设置npc的朝向
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

            // 计算与玩家的距离
            float distanceToPlayer = Vector2.Distance(npc.Center, player.Center);

            // 冲刺行为（当玩家距离较近时）
            if (distanceToPlayer < 400f) {
                // 设置冲刺速度
                float dashSpeed = 12f;
                Vector2 dashDirection = (player.Center - npc.Center).SafeNormalize(Vector2.Zero);
                npc.velocity = dashDirection * dashSpeed;
            }
            // 迂回巡空行为（当玩家距离较远时）
            else {
                // 按常规速度巡航并绕开障碍物
                npc.ChasingBehavior(player.Center, 23);

                // 随机改变方向模拟迂回行为
                if (Main.rand.NextBool(60)) {
                    float randomAngle = MathHelper.ToRadians(Main.rand.NextFloat(-45f, 45f));
                    Vector2 newVelocity = npc.velocity.RotatedBy(randomAngle);
                    npc.velocity = newVelocity.SafeNormalize(Vector2.Zero) * 6f; // 6f为巡航速度
                }
            }

            // 每帧微调，防止卡住
            npc.velocity = npc.velocity.SafeNormalize(Vector2.Zero) * Math.Min(npc.velocity.Length(), 12f); // 最大冲刺速度限制
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
            Texture2D value2 = Head_Glow.Value;
            spriteBatch.Draw(value2, npc.Center - Main.screenPosition
                , glowRectangle, Color.White, npc.rotation + MathHelper.Pi, glowRectangle.Size() / 2, npc.scale, SpriteEffects.None, 0);
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return HeadPrimeAI.DontReform();
        }
    }
}
