﻿using CalamityMod;
using CalamityMod.Events;
using CalamityMod.World;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Summon;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.Projectiles.Boss.Destroyer;
using CalamityOverhaul.Content.RemakeItems.ModifyBag;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer
{
    internal class DestroyerHeadAI : CWRNPCOverride, ICWRLoader
    {
        public override int TargetID => NPCID.TheDestroyer;
        private const int maxFindMode = 60000;
        private ref float ByDashX => ref ai[0];
        private ref float ByDashY => ref ai[1];
        private ref float Time => ref ai[2];
        private ref float ByMasterStageIndex => ref ai[3];
        private const int AttackAIsMaxSlot = 12;
        private float[] AttackAIs = new float[AttackAIsMaxSlot];
        private List<NPC> Bodys = new List<NPC>();
        internal static bool BossRush => BossRushEvent.BossRushActive || CWRWorld.MachineRebellion;
        internal static bool MasterMode => Main.masterMode || BossRush;
        internal static bool Death => CalamityWorld.death || BossRush;
        private int frame;
        private int glowFrame;
        private bool openMouth;
        private int dontOpenMouthTime;
        private Player player;
        [VaultLoaden(CWRConstant.NPC + "BTD/BTD_Head")]
        internal static Asset<Texture2D> HeadIcon = null;
        [VaultLoaden(CWRConstant.NPC + "BTD/Head")]
        internal static Asset<Texture2D> Head = null;
        [VaultLoaden(CWRConstant.NPC + "BTD/Head_Glow")]
        internal static Asset<Texture2D> Head_Glow = null;
        internal static int iconIndex;
        internal static int iconIndex_Void;
        internal const int StretchTime = 300;
        internal Vector2 DashVeloctiy {
            get => new(ByDashX, ByDashY);
            set {
                ByDashX = value.X;
                ByDashY = value.Y;
            }
        }
        void ICWRLoader.LoadData() {
            CWRMod.Instance.AddBossHeadTexture(CWRConstant.NPC + "BTD/BTD_Head", -1);
            iconIndex = ModContent.GetModBossHeadSlot(CWRConstant.NPC + "BTD/BTD_Head");
            CWRMod.Instance.AddBossHeadTexture(CWRConstant.Placeholder, -1);
            iconIndex_Void = ModContent.GetModBossHeadSlot(CWRConstant.Placeholder);
        }

        public override void SetProperty() {
            if (CWRWorld.MachineRebellion) {
                npc.life = npc.lifeMax *= 32;
                npc.defDefense = npc.defense = 40;
                npc.defDamage = npc.damage *= 3;
            }
        }

        public override bool? CanCWROverride() {
            if (CWRWorld.MachineRebellion) {
                return true;
            }
            return null;
        }

        public override bool CheckActive() => false;

        public override void BossHeadSlot(ref int index) {
            if (!HeadPrimeAI.DontReform()) {
                index = iconIndex_Void;
            }
        }

        public override void BossHeadRotation(ref float rotation) => rotation = npc.rotation + MathHelper.Pi;

        public override void ModifyNPCLoot(NPC thisNPC, NPCLoot npcLoot) {
            IItemDropRuleCondition condition = new DropInDeathMode();
            LeadingConditionRule rule = new LeadingConditionRule(condition);
            rule.Add(ModContent.ItemType<DestroyersBlade>(), 4);
            rule.Add(ModContent.ItemType<StaffoftheDestroyer>(), 4);
            rule.Add(ModContent.ItemType<Observer>(), 4);
            rule.Add(ModContent.ItemType<ForgedLash>(), 4);
            npcLoot.Add(rule);
        }

        internal void FindTarget() {
            if (npc.target < 0 || npc.target >= 255) {
                npc.FindClosestPlayer();
                player = Main.player[npc.target];
            }
            if (!player.Alives() || player.Distance(npc.Center) > maxFindMode) {
                npc.FindClosestPlayer();
                player = Main.player[npc.target];
                if (!player.Alives() || player.Distance(npc.Center) > maxFindMode) {
                    ByMasterStageIndex = 99;
                    NetAISend();
                }
            }
        }

        internal void NetWorkAI() {
            if (!VaultUtils.isServer) {
                return;
            }
            if (npc.netSpam > 8) {
                npc.netSpam = 0;
            }
            npc.netUpdate = true;
            NetAISend();
        }

        internal static void ForcedNetUpdating(NPC npc) {
            if (!VaultUtils.isServer || !npc.active || Main.GameUpdateCount % 80 != 0) {
                return;
            }

            foreach (var findPlayer in Main.ActivePlayers) {
                if (findPlayer.Distance(npc.position) < 1440) {
                    continue;
                }

                npc.SendNPCbasicData(findPlayer.whoAmI);
            }
        }

        public override bool AI() {
            Time++;
            npc.timeLeft = 1800;
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
            Attack();
            if (CWRWorld.MachineRebellion && !MachineRebellionAI()) {
                ForcedNetUpdating(npc);
                return false;
            }
            if (!OrigAI()) {
                ForcedNetUpdating(npc);
                return false;
            }
            return true;
        }

        //攻击函数直接不在客户端上运行，节省了同步数据的开销
        internal void Attack() {
            if (VaultUtils.isClient || Time < StretchTime) {
                return;
            }

            int idleTime = 600;
            if (MasterMode) {
                idleTime -= 60;
            }
            if (Death) {
                idleTime -= 90;
            }
            if (BossRush) {
                idleTime -= 120;
            }

            // 收集体节
            if (++AttackAIs[0] > idleTime) {
                Bodys.Clear();
                foreach (var body in Main.ActiveNPCs) {
                    if (body.type != NPCID.TheDestroyerBody) {
                        continue;
                    }
                    if (body.realLife != npc.whoAmI) {
                        continue;
                    }
                    Bodys.Add(body);
                }
                AttackAIs[1] = Bodys.Count - 1; // 体节索引
                AttackAIs[0] = 0; // 重置计时器
                AttackAIs[4] = 0; // 初始化模式计数器
                AttackAIs[5] = 0; // 初始化波次计数器
            }

            // 控制节奏的延迟
            if (AttackAIs[3] == 0) {
                AttackAIs[3] = 4; // 默认发射间隔
            }

            // 节奏模式逻辑
            if (AttackAIs[1] > 0 && ++AttackAIs[2] > AttackAIs[3]) {
                // 模式选择（每完成一个完整周期切换模式）
                if (AttackAIs[4] == 0) {
                    AttackAIs[4] = Main.rand.Next(1, 4); // 随机选择模式（1-3）
                    AttackAIs[5] = 0; // 重置波次计数器
                }

                int pattern = (int)AttackAIs[4];
                NPC thisBody = npc;
                Vector2 shootVelocity;

                if (pattern == 1) {
                    // 模式1：波浪式发射（从两端向中间）
                    int halfCount = Bodys.Count / 2;
                    int index = (int)(AttackAIs[5] < halfCount ? AttackAIs[5] : Bodys.Count - 1 - AttackAIs[5]);
                    if (index >= 0 && index < Bodys.Count) {
                        thisBody = Bodys[index];
                        shootVelocity = thisBody.Center.To(player.Center).UnitVector();
                        Projectile.NewProjectile(npc.GetSource_FromAI(), thisBody.Center,
                            shootVelocity, ModContent.ProjectileType<SpawnLaserEffect>(),
                            0, 0f, Main.myPlayer, AttackAIs[5] % 3, thisBody.whoAmI, player.whoAmI);
                    }
                    AttackAIs[5]++;
                    if (AttackAIs[5] >= Bodys.Count) {
                        AttackAIs[4] = 0; // 模式结束，重置
                        AttackAIs[1] = Bodys.Count - 1;
                    }
                }
                else if (pattern == 2) {
                    // 模式2：分组发射（每3个体节同时发射）
                    int groupSize = 3;
                    int startIndex = (int)AttackAIs[5] * groupSize;
                    for (int i = startIndex; i < startIndex + groupSize && i < Bodys.Count; i++) {
                        thisBody = Bodys[i];
                        shootVelocity = thisBody.Center.To(player.Center).UnitVector();
                        Projectile.NewProjectile(npc.GetSource_FromAI(), thisBody.Center,
                            shootVelocity, ModContent.ProjectileType<SpawnLaserEffect>(),
                            0, 0f, Main.myPlayer, i % 3, thisBody.whoAmI, player.whoAmI);
                    }
                    AttackAIs[5]++;
                    if (startIndex + groupSize >= Bodys.Count) {
                        AttackAIs[4] = 0; // 模式结束，重置
                        AttackAIs[1] = Bodys.Count - 1;
                    }
                }
                else if (pattern == 3) {
                    // 模式3：随机双发射（随机选择两个体节同时发射）
                    int index1 = Main.rand.Next(Bodys.Count);
                    int index2 = Main.rand.Next(Bodys.Count);
                    while (index2 == index1 && Bodys.Count > 1) {
                        index2 = Main.rand.Next(Bodys.Count); // 确保两个索引不同
                    }
                    thisBody = Bodys[index1];
                    shootVelocity = thisBody.Center.To(player.Center).UnitVector();
                    Projectile.NewProjectile(npc.GetSource_FromAI(), thisBody.Center,
                        shootVelocity, ModContent.ProjectileType<SpawnLaserEffect>(),
                        0, 0f, Main.myPlayer, index1 % 3, thisBody.whoAmI, player.whoAmI);
                    if (index2 < Bodys.Count) {
                        thisBody = Bodys[index2];
                        shootVelocity = thisBody.Center.To(player.Center).UnitVector();
                        Projectile.NewProjectile(npc.GetSource_FromAI(), thisBody.Center,
                            shootVelocity, ModContent.ProjectileType<SpawnLaserEffect>(),
                            0, 0f, Main.myPlayer, index2 % 3, thisBody.whoAmI, player.whoAmI);
                    }
                    AttackAIs[5]++;
                    if (AttackAIs[5] >= 3) { // 限制波次
                        AttackAIs[4] = 0; // 模式结束，重置
                        AttackAIs[1] = Bodys.Count - 1;
                    }
                }

                AttackAIs[2] = 0; // 重置发射计时器
                AttackAIs[3] = Main.rand.Next(3, 6); // 动态调整发射间隔，增加节奏感
                AttackAIs[1]--; // 减少体节计数
            }
        }

        internal bool OrigAI() {
            if (ByMasterStageIndex == 0) {
                if (!HeadPrimeAI.DontReform() && !VaultUtils.isClient) {
                    NPC.NewNPCDirect(npc.FromObjectGetParent(), npc.Center
                        , ModContent.NPCType<DestroyerDrawHeadIconNPC>(), 0, npc.whoAmI);
                }
                ByMasterStageIndex = 1;
            }

            FindTarget();
            HandleMouth();

            if (NPC.IsMechQueenUp && !CWRWorld.MachineRebellion) {
                Time = StretchTime + 60;
            }

            //这里判定一个时间进行冲刺，用于展开体节，实际冲刺的时间需要比预定的展开时间长一些
            if (Time < StretchTime + 60 && Time > 10) {
                if (DashVeloctiy == Vector2.Zero || npc.position.X > Main.maxTilesX * 16 - 50
                    || npc.position.X < 50 || npc.position.Y > Main.maxTilesY * 16 - 50 || npc.position.Y < 50) {
                    DashVeloctiy = npc.Center.To(player.Center).UnitVector();
                    NetWorkAI();
                }
                npc.velocity = DashVeloctiy * 32;
                return false;
            }

            return true;
        }

        private bool MachineRebellionAI() {
            if (ByMasterStageIndex == 99) {
                npc.velocity = new Vector2(0, 86);
                if (++ai[6] > 280) {
                    npc.life = 0;
                    npc.HitEffect();
                    npc.active = false;
                    npc.netUpdate = true;
                }
                return false;
            }

            if (Time > StretchTime && ByMasterStageIndex > 0 && ByMasterStageIndex != 99) {
                NPC skeletronPrime = CWRUtils.FindNPCFromeType(NPCID.SkeletronPrime);
                if (skeletronPrime != null && (skeletronPrime.life / (float)skeletronPrime.lifeMax) < 0.6f) {
                    ByMasterStageIndex = 99;//骷髅王低于这个血量时脱战
                    VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text6"), new(155, 215, 115));
                    return false;
                }
            }

            // 初始化
            if (ByMasterStageIndex == 0) {
                ByMasterStageIndex = 1;
                npc.ai[0] = 1;//将这个设置为否则他妈的其他地方就会再生成一次身体
                if (!HeadPrimeAI.DontReform() && !VaultUtils.isClient) {
                    NPC.NewNPCDirect(npc.FromObjectGetParent(), npc.Center
                        , ModContent.NPCType<DestroyerDrawHeadIconNPC>(), 0, npc.whoAmI);
                }
                SpawnBody();
                npc.netUpdate = true;
            }

            FindTarget();
            HandleMouth();

            if (npc.position.X > Main.maxTilesX * 16 - 50 || npc.position.X < 50
                || npc.position.Y > Main.maxTilesY * 16 - 50 || npc.position.Y < 50) {
                DashVeloctiy = npc.Center.To(player.Center).UnitVector();
                ai[7] = 0;
                ai[5] = 2;
                NetWorkAI();
            }

            //这里判定一个时间进行冲刺，用于展开体节，实际冲刺的时间需要比预定的展开时间长一些
            if (Time < StretchTime + 60 && Time > 10) {
                if (DashVeloctiy == Vector2.Zero) {
                    DashVeloctiy = npc.Center.To(player.Center).UnitVector();
                    NetWorkAI();
                }
                npc.velocity = DashVeloctiy * (32 + npc.Distance(player.Center) / 1000);
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                return false;
            }

            if (--ai[7] > 0) {
                npc.VanillaAI();
                if (npc.Distance(player.Center) > maxFindMode / 2) {//如果发现跑远了就里面切换阶段向玩家冲刺回来
                    DashVeloctiy = npc.Center.To(player.Center).UnitVector();
                    ai[7] = 0;
                    ai[5] = 2;
                    NetWorkAI();
                }
            }
            else {
                if (ai[5] == 0) {
                    if (++ai[4] > 120) {
                        ai[4] = 0;
                        ai[5] = 1;
                        NetWorkAI();
                    }
                }

                if (ai[5] == 1) {
                    DashVeloctiy = npc.Center.To(player.Center).UnitVector();
                    ai[5] = 2;
                    NetWorkAI();
                }

                if (ai[5] == 2) {
                    npc.velocity = DashVeloctiy * 44;
                    if (npc.Distance(player.Center) > maxFindMode / 2) {
                        DashVeloctiy = npc.Center.To(player.Center).UnitVector();
                        NetWorkAI();
                    }
                    if (++ai[6] > 180) {
                        ai[7] = 300;
                        ai[6] = 0;
                        ai[5] = 0;
                        ai[4] = 0;
                        NetWorkAI();
                    }
                }
            }

            return false;
        }

        private void SpawnBody() {
            // 生成毁灭者身体的多个部分
            if (VaultUtils.isClient) {
                return;
            }

            int index = npc.whoAmI;
            int oldIndex = npc.whoAmI;
            for (int i = 0; i < 88; i++) {
                oldIndex = index;
                index = NPC.NewNPC(npc.FromObjectGetParent(), (int)npc.Center.X, (int)npc.Center.Y
                    , i == 87 ? NPCID.TheDestroyerTail : NPCID.TheDestroyerBody
                    , 0, ai0: oldIndex, ai1: index, ai2: 0, ai3: npc.whoAmI);
                Main.npc[index].realLife = npc.whoAmI;
                Main.npc[index].netUpdate = true;
            }
        }

        private void HandleMouth() {
            VaultUtils.ClockFrame(ref glowFrame, 5, 3);

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
            Rectangle rectangle = value.GetRectangle(frame, 4);
            Rectangle glowRectangle = value.GetRectangle(glowFrame, 4);

            spriteBatch.Draw(value, npc.Center - Main.screenPosition
                , rectangle, drawColor, npc.rotation + MathHelper.Pi, rectangle.Size() / 2, npc.scale, SpriteEffects.None, 0);

            if (Time >= StretchTime) {
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
