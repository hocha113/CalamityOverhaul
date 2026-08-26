using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Aetherglim.Projectiles;
using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Aetherglim
{
    /// <summary>
    /// 微光地带的逐玩家状态：
    /// 「引力泡」泡内低重力漂浮（每个端对自己模拟的玩家确定性施加，入泡拍只在本机玩家上演）；
    /// 「深引」微光湖水下久待后的周期性向下引力脉冲（预告 48 帧：水中光斑下流+低鸣 →
    /// 温和下拽数秒 → 平息休整），全部状态机只走本机玩家——玩家运动本就是本机权威。
    /// 深引力度是"提醒你别恋战"级别：气量不足不开新脉冲、脉冲中气量告急提前平息、钩爪随时能拉走。
    /// 另持权威端私产：引力泡的逐玩家生成冷却（服务端决策用，客户端不得读它驱动画面）
    /// </summary>
    internal class AetherglimPlayer : ModPlayer
    {
        //==== 引力泡（泡内体感）====
        /// <summary>泡内重力系数（温和漂浮，跳跃与羽落感）</summary>
        private const float BubbleGravityMul = 0.12f;
        /// <summary>泡内下落速度上限系数</summary>
        private const float BubbleFallMul = 0.32f;
        /// <summary>泡体漂移对人的携带加速度上限（可能把你飘离平台的那只手）</summary>
        private const float BubbleCarryAccel = 0.03f;
        /// <summary>泡心上浮软力（低于泡心时向上托）</summary>
        private const float BubbleBuoyancy = 0.045f;

        //==== 深引（水下向下脉冲）====
        /// <summary>首次脉冲需要的水下累计帧，档位只调频率</summary>
        private static readonly int[] PulseDwellByTier = [560, 470, 380];
        /// <summary>两次脉冲之间的休整帧，档位只调频率</summary>
        private static readonly int[] PulseRestByTier = [780, 660, 540];
        /// <summary>预告帧（公平契约 ≥45，视觉+听觉双通道）</summary>
        private const int PulseTelegraphFrames = 48;
        /// <summary>拖拽持续帧</summary>
        private const int PulseActiveFrames = 150;
        /// <summary>每帧向下加速度（对比游泳上浮加速度，总能挣脱）</summary>
        private const float PulseAccel = 0.055f;
        /// <summary>脉冲附加下沉速度封顶</summary>
        private const float PulseMaxDown = 2.6f;

        /// <summary>深引相位</summary>
        private enum PullPhase : byte
        {
            Idle,
            Telegraph,
            Active,
            Rest,
        }

        /// <summary>权威端私产：引力泡生成冷却（<see cref="AetherglimAmbience"/> 读写）</summary>
        internal int BubbleSpawnTimer;

        private bool insideBubbleLastFrame;
        private int submergeTicks;
        private PullPhase pullPhase;
        private int phaseTick;
        private SlotId humSlot;
        /// <summary>低鸣当前应有的响度（脉冲进程驱动，回调逐帧读）</summary>
        private float humLevel;

        public override void PostUpdateMiscEffects() {
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                insideBubbleLastFrame = false;
                ResetPull();
                return;
            }
            UpdateBubbleFloat();
            UpdateDeepPull(tier);
        }

        public override void UpdateDead() {
            insideBubbleLastFrame = false;
            submergeTicks = 0;
            ResetPull();
        }

        private void ResetPull() {
            pullPhase = PullPhase.Idle;
            phaseTick = 0;
            humLevel = 0f;
        }

        //==================== 引力泡：泡内低重力 ====================

        private void UpdateBubbleFloat() {
            Projectile bubble = FindContainingBubble();
            bool inside = bubble != null;

            //Boss 在场/城镇安宁：位移扰动暂停，泡退化为纯视觉
            if (inside && (CWRWorld.HasBoss || AetherglimFX.NearTownNPC(Player.Center))) {
                insideBubbleLastFrame = false;
                return;
            }

            if (inside) {
                //低重力漂浮：确定性地作用在每个端各自模拟的这名玩家身上
                Player.gravity *= BubbleGravityMul;
                Player.maxFallSpeed *= BubbleFallMul;
                //泡体携带：向泡的漂移速度缓慢靠拢，可能把你飘离平台
                float carry = MathHelper.Clamp(bubble.velocity.X - Player.velocity.X,
                    -BubbleCarryAccel, BubbleCarryAccel);
                Player.velocity.X += carry;
                //低于泡心时轻托上浮
                if (Player.Center.Y > bubble.Center.Y + bubble.ai[0] * 0.25f) {
                    Player.velocity.Y -= BubbleBuoyancy;
                }
                //泡内升降不积累坠落高度：纯物理扰动不许转化成摔落伤害
                Player.fallStart = (int)(Player.position.Y / 16f);

                if (!insideBubbleLastFrame && Player.whoAmI == Main.myPlayer) {
                    //入泡拍：一点上浮冲量+失重轻响，只在本机玩家上演
                    if (Player.velocity.Y > 0f) {
                        Player.velocity.Y *= 0.35f;
                    }
                    Player.velocity.Y -= 1.4f;
                    SoundEngine.PlaySound(SoundID.ShimmerWeak1 with { Volume = 0.55f, Pitch = 0.25f }, Player.Center);
                }
            }
            insideBubbleLastFrame = inside;
        }

        private Projectile FindContainingBubble() {
            int bubbleType = ModContent.ProjectileType<AetherglimGravityBubbleProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == bubbleType && AetherglimGravityBubbleProj.Contains(proj, Player.Center)) {
                    return proj;
                }
            }
            return null;
        }

        //==================== 深引：水下向下引力脉冲 ====================

        private void UpdateDeepPull(int tier) {
            //玩家运动是本机权威，深引状态机只在本机玩家上走
            if (Player.whoAmI != Main.myPlayer || Main.dedServ) {
                return;
            }

            bool submerged = Player.shimmerWet && !Player.shimmering && !Player.dead;
            if (!submerged) {
                //离水快速泄压：余量清空，进行中的相位直接平息
                submergeTicks = Math.Max(0, submergeTicks - 4);
                if (pullPhase is PullPhase.Telegraph or PullPhase.Active) {
                    ResetPull();
                }
                UpdateHum(0f);
                return;
            }

            submergeTicks++;

            switch (pullPhase) {
                case PullPhase.Idle:
                    //气量不足不开新脉冲：深引是提醒，不是溺水执行器
                    bool breathSafe = Player.breath > Player.breathMax * 2 / 5;
                    if (submergeTicks >= PulseDwellByTier[tier - 1] && breathSafe
                        && AetherglimFX.MechanicsAllowed(Player.Center)) {
                        pullPhase = PullPhase.Telegraph;
                        phaseTick = 0;
                        //听觉通道开场：一记沉钟垫底，低鸣循环随后浮上来
                        SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.5f, Pitch = -0.72f, MaxInstances = 2 }, Player.Center);
                    }
                    UpdateHum(0f);
                    break;

                case PullPhase.Telegraph:
                    phaseTick++;
                    //视觉通道：水中光斑向下流动，向"下"的方向性就是预告内容
                    SpawnDownwardMotes(1f);
                    UpdateHum(0.32f * (phaseTick / (float)PulseTelegraphFrames));
                    if (CWRWorld.HasBoss) {
                        ResetPull();
                        break;
                    }
                    if (phaseTick >= PulseTelegraphFrames) {
                        pullPhase = PullPhase.Active;
                        phaseTick = 0;
                        SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.62f, Pitch = -0.5f, MaxInstances = 2 }, Player.Center);
                    }
                    break;

                case PullPhase.Active:
                    phaseTick++;
                    //温和下拽：只加不设，钩爪/游泳/跳跃随时能赢
                    if (Player.velocity.Y < PulseMaxDown && Player.grapCount == 0) {
                        Player.velocity.Y += PulseAccel;
                    }
                    SpawnDownwardMotes(1.6f);
                    UpdateHum(0.4f * (1f - phaseTick / (float)PulseActiveFrames * 0.4f));
                    //气量告急提前平息：绝不把人按在水里
                    bool mercy = Player.breath < Player.breathMax / 4;
                    if (phaseTick >= PulseActiveFrames || mercy || CWRWorld.HasBoss) {
                        pullPhase = PullPhase.Rest;
                        phaseTick = 0;
                        SoundEngine.PlaySound(SoundID.ShimmerWeak2 with { Volume = 0.45f, Pitch = -0.2f }, Player.Center);
                    }
                    break;

                case PullPhase.Rest:
                    phaseTick++;
                    UpdateHum(0f);
                    if (phaseTick >= PulseRestByTier[tier - 1]) {
                        pullPhase = PullPhase.Idle;
                        //休整结束重新蓄势：下一轮仍要完整预告
                        submergeTicks = PulseDwellByTier[tier - 1] / 2;
                    }
                    break;
            }
        }

        /// <summary>水中下流光斑（≤2 粒/帧，只围着本机玩家）</summary>
        private void SpawnDownwardMotes(float rate) {
            if (Main.rand.NextFloat() > rate * 0.55f) {
                return;
            }
            Vector2 pos = Player.Center + new Vector2(Main.rand.NextFloat(-70f, 70f), Main.rand.NextFloat(-60f, 20f));
            Dust dust = Dust.NewDustPerfect(pos, DustID.ShimmerSpark,
                new Vector2(0f, Main.rand.NextFloat(1.6f, 3.1f)), 110, default, Main.rand.NextFloat(0.9f, 1.3f));
            dust.noGravity = true;
        }

        /// <summary>低鸣循环槽：预告与脉冲期间在场，响度随相位走（镜像 OldNetAmbience 的槽管理）</summary>
        private void UpdateHum(float target) {
            humLevel = MathHelper.Lerp(humLevel, target, 0.12f);
            if (humLevel < 0.01f) {
                return;
            }
            if (!SoundEngine.TryGetActiveSound(humSlot, out _)) {
                humSlot = SoundEngine.PlaySound(
                    SoundID.DD2_EtherianPortalIdleLoop with { IsLooped = true, MaxInstances = 1 },
                    null, UpdateHumCallback);
            }
        }

        private bool UpdateHumCallback(ActiveSound sound) {
            if (Main.gameMenu || humLevel < 0.01f || Player.whoAmI != Main.myPlayer || !Player.active) {
                return false;
            }
            sound.Volume = humLevel;
            sound.Pitch = -0.82f;
            sound.Position = null;
            return true;
        }
    }
}
