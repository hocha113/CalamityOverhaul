using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.States
{
    /// <summary>
    /// 天穹尖刺瀑(二阶段)：车道预告落定(预告即承诺)→尖刺列错拍坠落→翼卫镜像扇协同。
    /// 皇后每波在高空掠影横穿一趟，读作"播撒"。缺口=每波固定跳过的车道(声明式)。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)QueenSlimeStateIndex.SkySpikeCascade, typeof(QueenSlimeStateContext))]
    internal class QueenSkySpikeCascadeState : QueenSlimeStateBase
    {
        public override string StateName => "SkySpikeCascade";
        public override QueenSlimeStateIndex StateIndex => QueenSlimeStateIndex.SkySpikeCascade;

        #region 节奏与公平常量
        private const int SetupTime = 26;
        private const int WavePeriod = 66;
        /// <summary>车道预告寿命，坠落在其熄灭帧开始</summary>
        private const int OmenLife = 34;
        private const float LaneSpacing = 112f;
        /// <summary>缺口声明：每 3 条车道空 1 条，空位随波次轮转(发射循环实际读取)</summary>
        private const int LaneGapEvery = 3;
        private const int HardTimeout = 700;
        #endregion

        private int WaveCount(QueenSlimeStateContext ctx) => ctx.IsDeathMode ? 4 : 3;
        private int LaneCount(QueenSlimeStateContext ctx) => ctx.IsDeathMode ? 11 : 9;

        /// <summary>本波锁定的车道X(服务端弹幕生成用；预告落定后不再改向)</summary>
        private float[] laneXs;
        private float laneSkyY;
        private int currentWave = -1;

        public QueenSkySpikeCascadeState() {
        }

        public override void OnEnter(QueenSlimeStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            DisableContactDamage(npc);
            npc.noGravity = true;
            npc.noTileCollide = true;
            currentWave = -1;
        }

        public override IQueenSlimeState OnUpdate(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;
            DisableContactDamage(npc);

            //就位段：掠上高位
            if (Timer <= SetupTime) {
                int side = npc.Center.X < player.Center.X ? -1 : 1;
                Vector2 anchor = player.Center + new Vector2(side * 320f, -440f);
                QueenMotion.SpringHover(npc, anchor, 0.024f, 0.12f, 30f);
                QueenMotion.FlightLean(npc);
                context.PoseCommand = 5;
                context.WingFlapBoost = 1.3f;
                FaceTarget(npc, player.Center);
                return null;
            }

            int cascadeT = (int)Timer - SetupTime;
            int wave = cascadeT / WavePeriod;
            int waveT = cascadeT % WavePeriod;

            //收势
            if (wave >= WaveCount(context)) {
                context.ResetChargeState();
                QueenMotion.FlitBrake(npc, 0.9f);
                context.PoseCommand = 5;
                if ((waveT >= 30 || Timer > HardTimeout) && !VaultUtils.isClient) {
                    return new QueenAerialBalletState();
                }
                return null;
            }

            //新波：锁车道+挂预告(服务端一次)
            if (wave != currentWave) {
                currentWave = wave;
                if (!VaultUtils.isClient) {
                    LockLanesAndOmen(context, wave);
                }
            }

            UpdateQueenStrafe(context, wave, waveT);

            //坠落帧：预告熄灭即降刺(服务端)
            if (waveT == OmenLife && !VaultUtils.isClient) {
                DropSpikeColumns(context, wave);
            }
            if (waveT == OmenLife) {
                SoundEngine.PlaySound(SoundID.Item155 with { Volume = 0.8f, Pitch = 0.3f + wave * 0.07f }, npc.Center);
            }

            //翼卫镜像扇(服务端)：坠落后一拍协同
            if (waveT == OmenLife + 5 && !VaultUtils.isClient) {
                foreach (var n in Main.ActiveNPCs) {
                    if (context.IsMyMinion(n, QueenMinionRole.WingedEscort)) {
                        QueenMotion.SpawnSpikeFan(n, n.Center, player.Center, 3, 0.26f, 9f,
                            QueenCrystalSpikeProj.SpikeDamage, n.whoAmI * 0.13f % 1f);
                    }
                }
            }

            return null;
        }

        /// <summary>皇后每波高空掠影一趟(蓄→一帧全速→硬刹)</summary>
        private void UpdateQueenStrafe(QueenSlimeStateContext context, int wave, int waveT) {
            NPC npc = context.Npc;
            Player player = context.Target;
            int strafeDir = wave % 2 == 0 ? 1 : -1;
            context.PoseCommand = 5;

            if (waveT < 8) {
                //波首悬定
                npc.velocity *= 0.85f;
                FaceTarget(npc, player.Center);
                context.WingFlapBoost = 0.9f;
            }
            else if (waveT < 14) {
                //蓄势后拉
                QueenMotion.FlitPullback(npc, new Vector2(strafeDir, 0f), (waveT - 8) / 6f, 2.2f);
                context.WingFlapBoost = 1.5f;
                context.SetChargeState(2, (waveT - 8) / 6f);
            }
            else if (waveT == 14) {
                //一帧全速横掠
                QueenMotion.FlitLaunch(npc, new Vector2(strafeDir, 0f), 27f);
                context.PushSquash(0.5f);
                context.AfterimageBoost = 1f;
                SoundEngine.PlaySound(SoundID.Item160 with { Volume = 0.6f, Pitch = 0.45f, MaxInstances = 3 }, npc.Center);
            }
            else if (waveT < 30) {
                //直线掠行(不转向，读得快)
                context.AfterimageBoost = Math.Max(context.AfterimageBoost, 0.85f);
                context.WingFlapBoost = 1.6f;
                QueenMotion.FlightLean(npc, 0.04f, 0.5f);
                context.PrismShimmer = Math.Max(context.PrismShimmer, 0.6f);
            }
            else {
                //硬刹+回稳到玩家上方
                QueenMotion.FlitBrake(npc, 0.78f);
                Vector2 hold = player.Center + new Vector2(npc.Center.X < player.Center.X ? -300f : 300f, -430f);
                QueenMotion.SpringHover(npc, hold, 0.012f, 0.1f, 14f);
                QueenMotion.FlightLean(npc);
                FaceTarget(npc, player.Center);
            }
        }

        /// <summary>锁车道并挂预告(服务端)：以玩家预测位为中心，空位随波轮转</summary>
        private void LockLanesAndOmen(QueenSlimeStateContext context, int wave) {
            NPC npc = context.Npc;
            Player player = context.Target;
            int count = LaneCount(context);
            laneXs = new float[count];
            laneSkyY = player.Center.Y - 520f;
            float centerX = player.Center.X + player.velocity.X * 28f;

            for (int i = 0; i < count; i++) {
                float x = centerX + (i - (count - 1) * 0.5f) * LaneSpacing;
                //缺口车道：本波空位(标记为NaN，坠落循环同读)
                if (i % LaneGapEvery == wave % LaneGapEvery) {
                    laneXs[i] = float.NaN;
                    continue;
                }
                laneXs[i] = x;
                Vector2 top = new Vector2(x, laneSkyY);
                Vector2 ground = QueenMotion.FindGroundBelow(top);
                float len = MathHelper.Clamp(ground.Y - laneSkyY, 300f, 1300f);
                QueenMotion.SpawnLaneOmen(npc, top, len, OmenLife);
            }
        }

        /// <summary>降刺(服务端)：每条已锁车道两枚错拍尖刺列</summary>
        private void DropSpikeColumns(QueenSlimeStateContext context, int wave) {
            NPC npc = context.Npc;
            if (laneXs == null) {
                return;
            }
            for (int i = 0; i < laneXs.Length; i++) {
                if (float.IsNaN(laneXs[i])) {
                    continue;
                }
                for (int k = 0; k < 2; k++) {
                    Vector2 spawn = new Vector2(laneXs[i], laneSkyY - k * 52f);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), spawn, new Vector2(0f, 0.4f),
                        Terraria.ModLoader.ModContent.ProjectileType<QueenCrystalSpikeProj>(),
                        QueenCrystalSpikeProj.SpikeDamage, 0f, Main.myPlayer,
                        (int)QueenCrystalSpikeProj.Mode.Rain, k * 9f, (wave * 0.19f + i * 0.07f) % 1f);
                }
            }
        }

        public override void OnExit(QueenSlimeStateContext context) {
            base.OnExit(context);
            DisableContactDamage(context.Npc);
        }
    }
}
