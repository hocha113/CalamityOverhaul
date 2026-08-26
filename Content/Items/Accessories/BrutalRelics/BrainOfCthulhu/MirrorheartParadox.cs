using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.BrainOfCthulhu
{
    /// <summary>
    /// 镜心悖论：克苏鲁之脑残酷遗物。
    /// 镜像之心游曳在玩家对侧，以半伤复现玩家射出的弹幕；
    /// 受击瞬间与镜心换位闪避，两端爆发心跳脉冲环并困惑敌人，镜心随后碎裂重聚
    /// </summary>
    internal class MirrorheartParadox : BaseBrutalRelic
    {
        public override void SetDefaults() {
            base.SetDefaults();
            //同期(克脑档位)掉落物约4倍价
            Item.value = Item.buyPrice(0, 20, 0, 0);
        }

        //镜心是功能实体(换位落点+复现出膛口)，不吃可见性开关
        public override void UpdateAccessory(Player player, bool hideVisual)
            => player.GetModPlayer<MirrorheartPlayer>().Equipped = true;
    }

    /// <summary>
    /// 镜心状态机：装备旗、镜位推演、复现队列、换位闪避与碎裂计时。
    /// 位置为各端本地推演(本人端用光标锚，远端用朝向/速度锚，仅表现差异)；
    /// 换位在受击者本人端裁决，演出经 <see cref="MirrorheartNet"/> 广播
    /// </summary>
    internal class MirrorheartPlayer : ModPlayer
    {
        /// <summary>镜心悬停距离(px)</summary>
        public const float MirrorDist = 110f;
        /// <summary>碎裂重聚时长(帧)，期间无复现无闪避</summary>
        public const int ShatterRebuildTime = 360;
        /// <summary>换位演出时长(帧)</summary>
        public const int SwapFxTime = 34;
        /// <summary>复现伤害倍率(设计卡定值)</summary>
        public const float EchoDamageMul = 0.5f;
        /// <summary>复现延迟(帧)，镜心慢半拍出手</summary>
        public const int EchoDelay = 7;
        /// <summary>脉冲环伤害</summary>
        public const int PulseDamage = 165;
        /// <summary>换位后无敌帧</summary>
        public const int SwapImmuneTime = 75;
        private const int EchoPerFrameCap = 4;
        private const int EchoQueueCap = 24;
        internal const string EchoSourceContext = "MirrorheartEcho";
        private const string PulseSourceContext = "MirrorheartPulse";

        /// <summary>待复现的弹幕快照，出手时从镜心当前位置射出</summary>
        private struct PendingEcho
        {
            public int Type;
            public Vector2 Velocity;
            public float Ai0, Ai1, Ai2;
            public int Damage;
            public float Knockback;
            public int Timer;
        }

        /// <summary>本帧装备生效，物品钩子逐帧点亮</summary>
        public bool Equipped;
        /// <summary>镜心碎裂剩余帧，>0 期间复现与闪避同时下线</summary>
        public int ShatterTimer;
        /// <summary>镜心当前位置(各端本地推演)</summary>
        public Vector2 MirrorPos;
        /// <summary>镜心实体化程度 0~1，重聚渐显</summary>
        public float CloneMaterialize;
        /// <summary>换位演出剩余帧(本人裁决或远端事件点亮)</summary>
        public int SwapFxTimer;
        /// <summary>换位演出：腾出的位置(镜心碎裂处)</summary>
        public Vector2 SwapPosA;
        /// <summary>换位演出：落点(玩家新位置)</summary>
        public Vector2 SwapPosB;

        private bool mirrorPosValid;
        private readonly List<PendingEcho> pendingEchoes = new(EchoQueueCap);
        private long lastEchoSoundTick;

        public override void ResetEffects() => Equipped = false;

        public override void PreUpdate() => TickTimers();

        public override void UpdateDead() {
            TickTimers();
            pendingEchoes.Clear();
        }

        private void TickTimers() {
            if (SwapFxTimer > 0) {
                SwapFxTimer--;
                UpdateSwapScreenPulse();
            }

            if (ShatterTimer > 0) {
                ShatterTimer--;
                CloneMaterialize = 0f;
                if (ShatterTimer == 0) {
                    //重聚瞬间：镜位重置到锚点，播一记轻响
                    mirrorPosValid = false;
                    if (!VaultUtils.isServer && Equipped && BrainMotion.OnScreen(Player.Center)) {
                        BrainMotion.BloodMistBurst(Player.Center, 0.55f, 3, 4f);
                        BrainMotion.FleshSquish(Player.Center, 0.5f, 0.1f);
                        BrainHeartbeat.Thump(0.4f);
                    }
                }
            }
            else if (Equipped && CloneMaterialize < 1f) {
                CloneMaterialize = Math.Min(CloneMaterialize + 0.045f, 1f);
            }
        }

        /// <summary>换位期间的心跳屏效：低频红圈脉冲，克脑活跃时只借拍不抢中心</summary>
        private void UpdateSwapScreenPulse() {
            if (VaultUtils.isServer) {
                return;
            }
            float k = SwapFxTimer / (float)SwapFxTime;
            float dist = Main.LocalPlayer.Distance(SwapPosB);
            float proximity = MathHelper.Clamp(1f - dist / 2600f, 0f, 1f);
            if (proximity <= 0.02f) {
                return;
            }
            if (!BrainHeartbeat.ActiveThisFrame) {
                BrainHeartbeat.Push(SwapPosB, 0.55f * k * proximity, 0.10f * k * proximity, 0f);
            }
        }

        public override void PostUpdate() {
            if (VaultUtils.isServer) {
                //服务端不推演镜位(纯表现)，只保证队列不积压
                pendingEchoes.Clear();
                return;
            }

            if (!Equipped || Player.dead) {
                mirrorPosValid = false;
                pendingEchoes.Clear();
                return;
            }

            UpdateMirrorPosition();
            FirePendingEchoes();

            //镜心常驻低频冷血雾，材质=镜化脑髓(与假体碎裂同料)
            if (ShatterTimer <= 0 && CloneMaterialize > 0.9f
                && Main.GameUpdateCount % 12 == 0 && BrainMotion.OnScreen(MirrorPos)) {
                var mist = PRTLoader.NewParticle<PRT_BrainBloodMist>(
                    MirrorPos + Main.rand.NextVector2Circular(20f, 16f),
                    Main.rand.NextVector2Circular(0.5f, 0.5f) - Vector2.UnitY * 0.3f,
                    Color.Lerp(BrainMotion.MirrorCold, BrainMotion.BloodDark, Main.rand.NextFloat(0.5f)) * 0.6f,
                    Main.rand.NextFloat(0.3f, 0.5f));
                mist?.Configure(Main.rand.Next(20, 34));
            }
        }

        /// <summary>
        /// 镜位推演：悬停在玩家对侧(本人端=光标反向，远端=速度/朝向反向)。
        /// 远端与本人端的镜位可有微差，复现出膛与换位落点均以本人端广播为准
        /// </summary>
        private void UpdateMirrorPosition() {
            if (ShatterTimer > 0) {
                return;
            }

            Vector2 aim;
            if (Player.whoAmI == Main.myPlayer) {
                aim = (Main.MouseWorld - Player.Center).SafeNormalize(Vector2.UnitX * Player.direction);
            }
            else if (Player.velocity.LengthSquared() > 9f) {
                aim = Player.velocity.SafeNormalize(Vector2.UnitX * Player.direction);
            }
            else {
                aim = Vector2.UnitX * Player.direction;
            }

            Vector2 target = Player.Center - aim * MirrorDist;
            target.Y += (float)Math.Sin(Main.GameUpdateCount * 0.043f + Player.whoAmI * 1.7f) * 10f;

            if (!mirrorPosValid || Vector2.Distance(MirrorPos, target) > 500f) {
                MirrorPos = target;
                mirrorPosValid = true;
                return;
            }
            MirrorPos = Vector2.Lerp(MirrorPos, target, 0.16f);
        }

        /// <summary>登记一枚待复现弹幕(仅所有者端调用)</summary>
        internal void QueueEcho(Projectile projectile) {
            if (pendingEchoes.Count >= EchoQueueCap) {
                return;
            }
            pendingEchoes.Add(new PendingEcho {
                Type = projectile.type,
                Velocity = projectile.velocity,
                Ai0 = projectile.ai[0],
                Ai1 = projectile.ai[1],
                Ai2 = projectile.ai[2],
                Damage = Math.Max(1, (int)(projectile.damage * EchoDamageMul)),
                Knockback = projectile.knockBack * 0.75f,
                Timer = EchoDelay
            });
        }

        /// <summary>镜心慢半拍复现：到期弹从镜心当前位置出膛，带回坐微动与冷色出手花</summary>
        private void FirePendingEchoes() {
            if (pendingEchoes.Count == 0) {
                return;
            }
            if (Player.whoAmI != Main.myPlayer || ShatterTimer > 0 || !mirrorPosValid) {
                if (ShatterTimer > 0) {
                    pendingEchoes.Clear();
                }
                return;
            }

            int fired = 0;
            for (int i = 0; i < pendingEchoes.Count; i++) {
                PendingEcho echo = pendingEchoes[i];
                echo.Timer--;
                pendingEchoes[i] = echo;
                if (echo.Timer > 0 || fired >= EchoPerFrameCap) {
                    continue;
                }

                Projectile.NewProjectile(Player.GetSource_Misc(EchoSourceContext),
                    MirrorPos, echo.Velocity, echo.Type, echo.Damage, echo.Knockback,
                    Player.whoAmI, echo.Ai0, echo.Ai1, echo.Ai2);
                pendingEchoes.RemoveAt(i);
                i--;
                fired++;

                //出手回坐+冷色花，读作镜心开火而非凭空冒弹
                Vector2 dir = echo.Velocity.SafeNormalize(Vector2.Zero);
                MirrorPos -= dir * 7f;
                if (BrainMotion.OnScreen(MirrorPos)) {
                    for (int k = 0; k < 3; k++) {
                        PRTLoader.NewParticle<PRT_Spark>(MirrorPos + dir * 14f,
                            dir.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(2f, 5f),
                            Color.Lerp(BrainMotion.MirrorCold, Color.White, Main.rand.NextFloat(0.4f)),
                            Main.rand.NextFloat(0.5f, 0.9f))?.Configure(true, Main.rand.Next(8, 14));
                    }
                    if (Main.GameUpdateCount - lastEchoSoundTick > 8) {
                        lastEchoSoundTick = Main.GameUpdateCount;
                        BrainMotion.FleshSquish(MirrorPos, 0.24f, 0.35f);
                    }
                }
            }
        }

        /// <summary>
        /// 受击瞬间换位闪避：镜心在位即免除本次伤害，与镜心互换位置，
        /// 两端各爆一圈心跳脉冲，镜心碎裂进入重聚期。
        /// 裁决仅在受击者本人端(服务端或他端发起的伤害不吃闪避，按原样结算)
        /// </summary>
        public override bool FreeDodge(Player.HurtInfo info) {
            if (!Equipped || ShatterTimer > 0 || info.Damage <= 0) {
                return false;
            }
            if (Player.whoAmI != Main.myPlayer || !mirrorPosValid) {
                return false;
            }

            Vector2 posA = Player.Center;
            Vector2 posB = MirrorPos;
            //镜位卡在实体块里就原地闪避，只免伤不挪人
            if (Collision.SolidCollision(posB - Player.Size * 0.5f, Player.width, Player.height)) {
                posB = posA;
            }

            if (posB != posA) {
                Player.RemoveAllGrapplingHooks();
                Player.Center = posB;
                Player.fallStart = (int)(Player.position.Y / 16f);
                Player.fallStart2 = Player.fallStart;
            }
            Player.GivePlayerImmuneState(SwapImmuneTime, true);

            //镜心接管玩家腾出的位置后随即碎裂
            MirrorPos = posA;
            ShatterTimer = ShatterRebuildTime;
            CloneMaterialize = 0f;
            pendingEchoes.Clear();

            //两端脉冲环：落点为主拍(ai1=1 承担心音)，旧位为副拍
            int ringType = ModContent.ProjectileType<MirrorheartPulse>();
            Projectile.NewProjectile(Player.GetSource_Misc(PulseSourceContext), posB, Vector2.Zero,
                ringType, PulseDamage, 9f, Player.whoAmI, 0f, 1f);
            if (posB != posA) {
                Projectile.NewProjectile(Player.GetSource_Misc(PulseSourceContext), posA, Vector2.Zero,
                    ringType, PulseDamage, 9f, Player.whoAmI, 0f, 0f);
            }

            StartSwapFx(posA, posB);
            MirrorheartNet.SendSwap(Player.whoAmI, posA, posB);
            return true;
        }

        /// <summary>点亮换位演出(本端立即演，远端经网络事件到达后演)</summary>
        internal void StartSwapFx(Vector2 posA, Vector2 posB) {
            SwapFxTimer = SwapFxTime;
            SwapPosA = posA;
            SwapPosB = posB;
            ShatterTimer = Math.Max(ShatterTimer, ShatterRebuildTime);
            CloneMaterialize = 0f;

            if (VaultUtils.isServer) {
                return;
            }
            //双侧裂隙撕开+镜心碎裂+心跳重拍
            BrainMotion.TeleportBurst(posA, 1.0f, false);
            BrainMotion.TeleportBurst(posB, 1.2f, true);
            BrainMotion.MirrorShatter(posA, 1.25f);
            BrainHeartbeat.Thump(1.25f, 0.88f);
            BrainHeartbeat.PlayThumpSound(posB, 1f, 0.1f);
        }
    }

    /// <summary>
    /// 换位演出广播信道：受击者本人端上报(位置A/B)，服务端校验来源后转发，
    /// 各端把事件落到该玩家的 <see cref="MirrorheartPlayer"/> 上播演出。
    /// 位置本体走原版玩家同步，此包只载演出，丢包只丢观感不丢状态
    /// </summary>
    internal class MirrorheartNet : CWRNetChannel
    {
        internal static void SendSwap(int playerIndex, Vector2 posA, Vector2 posB) {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }
            ModPacket packet = CWRNetWork.GetPacket<MirrorheartNet>();
            packet.Write((byte)playerIndex);
            packet.WriteVector2(posA);
            packet.WriteVector2(posB);
            packet.Send();
        }

        public override void Receive(BinaryReader reader, int whoAmI) {
            //先读净载荷再校验，防流错位
            int playerIndex = reader.ReadByte();
            Vector2 posA = reader.ReadVector2();
            Vector2 posB = reader.ReadVector2();
            if (playerIndex < 0 || playerIndex >= Main.maxPlayers) {
                return;
            }

            if (VaultUtils.isServer) {
                //只接受本人上报，转发给其余客户端
                if (playerIndex != whoAmI) {
                    return;
                }
                ModPacket packet = CWRNetWork.GetPacket<MirrorheartNet>();
                packet.Write((byte)playerIndex);
                packet.WriteVector2(posA);
                packet.WriteVector2(posB);
                packet.Send(-1, whoAmI);
                return;
            }

            if (playerIndex == Main.myPlayer) {
                return;
            }
            Player player = Main.player[playerIndex];
            if (player == null || !player.active) {
                return;
            }
            player.GetModPlayer<MirrorheartPlayer>().StartSwapFx(posA, posB);
        }
    }
}
