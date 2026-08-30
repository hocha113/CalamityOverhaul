using CalamityOverhaul.Content.Items.Magic.Everdeeps;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Kinematics;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.States
{
    /// <summary>
    /// 跃空大跳（P1+）：弓身压地蓄势 → 猛然腾空 → 顶点短悬（锁落点、亮预警）→
    /// 头先行砸向地面 → 落地重锤：原地掀起巨型水龙卷（约 2000px）+ 两侧千像素巨浪 +
    /// 冲天水球弧雨。下砸接触伤=速度门；落点预警 10f+；浪与龙卷横向让位即安全。
    /// Counter=演出阶段：0 蓄力 1 升空 2 顶点 3 下砸 4 落地余韵
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)SeaShrimpStateIndex.SkyLeap, typeof(SeaShrimpStateContext))]
    internal class SeaShrimpSkyLeapState : SeaShrimpStateBase
    {
        public override string StateName => "SkyLeap";
        public override SeaShrimpStateIndex StateIndex => SeaShrimpStateIndex.SkyLeap;

        /// <summary>蓄力帧数</summary>
        private const int CrouchFrames = 26;
        /// <summary>顶点悬停帧数（落点预警窗）</summary>
        private const int ApexFrames = 10;
        /// <summary>落地余韵帧数</summary>
        private const int SettleFrames = 18;
        /// <summary>硬超时：任何异常都收得回（公平阀）</summary>
        private const int HardTimeout = 240;
        /// <summary>下砸接触伤速度门 px/f</summary>
        private const float SlamSpeedGate = 20f;

        private Vector2 landPoint;
        /// <summary>当前阶段起始帧</summary>
        private int phaseStart;

        public override ISeaShrimpState OnUpdate(SeaShrimpStateContext ctx) {
            NPC npc = ctx.Npc;
            ShrimpLocomotion loco = ctx.Owner.Locomotion;
            int t = (int)Timer;
            Timer++;

            //硬超时兜底：落点扫描异常/地形怪也收得回
            if (t >= HardTimeout) {
                loco.AbortBallistic();
                return EndAttack(ctx, 55);
            }

            switch ((int)Counter) {
                case 0: {
                    //蓄力下压：弓身压地，末帧猛缩（迟滞语法——静到突然）
                    HoldInPlace(ctx);
                    float w = MathHelper.Clamp(t / (float)CrouchFrames, 0f, 1f);
                    float snap = MathF.Pow(w, 8f);
                    ctx.SpineCurl = 0.28f * w + 0.32f * snap;
                    ctx.TailFlare = 0.15f;
                    ctx.WaveGain = 0.3f;
                    ctx.CrystalGlow = MathF.Max(ctx.CrystalGlow, w * 0.8f);
                    if (t == 2 && !Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.6f, Pitch = -0.55f, MaxInstances = 2 }, npc.Center);
                    }

                    if (t >= CrouchFrames) {
                        //起跳帧：一帧点火腾空 + 起跳点贴地环
                        float dirX = MathF.Sign(ctx.Target.Center.X - npc.Center.X);
                        if (dirX == 0f) {
                            dirX = 1f;
                        }
                        loco.LaunchBallistic(new Vector2(dirX * 8f, -SeaShrimpDirector.LeapUpSpeed),
                            16, 0.82f, BallisticHeading.Frozen);
                        Counter = 1;
                        phaseStart = t;
                        if (!Main.dedServ) {
                            SoundEngine.PlaySound(SoundID.Item102 with { Volume = 0.8f, Pitch = 0.1f, MaxInstances = 2 }, npc.Center);
                            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.9f, Pitch = 0.2f, MaxInstances = 2 }, npc.Center);
                            ShakeNearby(npc.Center, 3.5f);
                            float groundY = FindGroundY(npc.Center);
                            ctx.AddRing(new Vector2(npc.Center.X, groundY - 6f), 250f, 22, 0.4f);
                            EverdeepVFX.SplashBurst(new Vector2(npc.Center.X, groundY - 10f),
                                -Vector2.UnitY * 11f, 1.2f);
                        }
                    }
                    return null;
                }
                case 1: {
                    //升空段：身体舒展，速度残影
                    float unroll = MathHelper.Clamp(npc.velocity.Length() / SeaShrimpDirector.LeapUpSpeed, 0f, 1f);
                    ctx.SpineCurl = -0.25f * unroll;
                    ctx.TailFlare = 0.8f;
                    ctx.WaveGain = 0.2f;
                    ctx.AfterimageStrength = MathF.Max(ctx.AfterimageStrength, unroll * 0.7f);

                    if (loco.BallisticDone) {
                        //顶点：锁落点（玩家 X 预测 + 垂扫找地），锁点即承诺。
                        //扫描起点压到 boss 顶点高度以下——玩家在高台上方时也向下砸，不向上"砸"
                        Counter = 2;
                        phaseStart = t;
                        float landX = ctx.Target.Center.X + ctx.Target.velocity.X * 14f;
                        float scanY = MathF.Max(ctx.Target.Center.Y - 120f, npc.Center.Y + 60f);
                        float landY = FindGroundY(new Vector2(landX, scanY));
                        landPoint = new Vector2(landX, landY);
                    }
                    return null;
                }
                case 2: {
                    //顶点悬停：蓄势下压 + 落点预警（要砸了）
                    float w = MathHelper.Clamp((t - phaseStart) / (float)ApexFrames, 0f, 1f);
                    ctx.SpineCurl = 0.4f * w;
                    ctx.TailFlare = 0.3f;
                    ctx.CrystalGlow = MathF.Max(ctx.CrystalGlow, 0.7f);

                    //落点预警：从落点上方垂下的滚动虚线 + 身轴缓转向砸线
                    Vector2 toLand = (landPoint - npc.Center).SafeNormalize(Vector2.UnitY);
                    HoldFacing(ctx, toLand.ToRotation(), 0.12f);
                    ctx.AddTelegraph(landPoint - new Vector2(0f, 520f), Vector2.UnitY, 520f,
                        0.35f + 0.35f * w, 0.85f);

                    if (t - phaseStart >= ApexFrames) {
                        //下砸点火：头先行砸向落点
                        Counter = 3;
                        phaseStart = t;
                        loco.LaunchBallistic(toLand * SeaShrimpDirector.LeapSlamSpeed,
                            60, 0.9f, BallisticHeading.HeadFirst);
                        if (!Main.dedServ) {
                            SoundEngine.PlaySound(SoundID.Item102 with { Volume = 0.75f, Pitch = -0.35f, MaxInstances = 2 }, npc.Center);
                        }
                    }
                    return null;
                }
                case 3: {
                    //下砸段：接触伤窗=速度门，预警线保持到触地
                    float speed = npc.velocity.Length();
                    ctx.SpineCurl = -0.15f;
                    ctx.TailFlare = 0.9f;
                    ctx.WaveGain = 0.2f;
                    ctx.AfterimageStrength = MathF.Max(ctx.AfterimageStrength, 0.9f);
                    if (speed > SlamSpeedGate) {
                        npc.damage = npc.defDamage;
                    }
                    ctx.AddTelegraph(landPoint - new Vector2(0f, 520f), Vector2.UnitY, 520f, 0.6f, 0.9f);

                    bool touchdown = npc.Center.Y >= landPoint.Y - SeaShrimpDirector.RideHeight;
                    if (touchdown || loco.BallisticDone) {
                        //落地重锤：硬停 + 大环 + 两侧巨浪
                        loco.AbortBallistic();
                        Counter = 4;
                        phaseStart = t;
                        Vector2 impact = new(npc.Center.X, landPoint.Y);
                        if (!Main.dedServ) {
                            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.95f, Pitch = -0.35f, MaxInstances = 2 }, impact);
                            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 1f, Pitch = -0.2f, MaxInstances = 2 }, impact);
                            SoundEngine.PlaySound(SoundID.Item21 with { Volume = 0.7f, Pitch = -0.45f, MaxInstances = 2 }, impact);
                            ShakeNearby(npc.Center, 8f, 1600f);
                            ctx.AddRing(impact - new Vector2(0f, 8f), 380f, 26, 0.4f);
                            EverdeepVFX.SplashBurst(impact, -Vector2.UnitY * 14f, 1.4f);
                            EverdeepVFX.SplashBurst(impact, new Vector2(6f, -8f), 1f);
                            EverdeepVFX.SplashBurst(impact, new Vector2(-6f, -8f), 1f);
                        }
                        if (!VaultUtils.isClient) {
                            //两侧千像素巨浪：从龙卷两旁向外行进（速度恒定可读，横向让位即安全）
                            int waveDamage = SeaShrimpDirector.ScaleProjectileDamage(npc, SeaShrimpDirector.WaveCrestDamage);
                            for (int side = -1; side <= 1; side += 2) {
                                Projectile.NewProjectile(npc.GetSource_FromAI(),
                                    impact + new Vector2(side * 160f, 0f), Vector2.Zero,
                                    ModContent.ProjectileType<SeaShrimpWaveCrest>(), waveDamage, 2f, Main.myPlayer,
                                    side, SeaShrimpDirector.WaveCrestHeight);
                            }
                            //落点原地巨型水龙卷：起身即预告，60% 前无伤
                            int vortexDamage = SeaShrimpDirector.ScaleProjectileDamage(npc, SeaShrimpDirector.VortexDamage);
                            Projectile.NewProjectile(npc.GetSource_FromAI(), impact, Vector2.Zero,
                                ModContent.ProjectileType<SeaShrimpLeapVortex>(), vortexDamage, 2f, Main.myPlayer,
                                SeaShrimpDirector.LeapVortexHeight);
                            //冲天水球喷泉：扇形上抛，后段自坠成弧雨
                            int boltDamage = SeaShrimpDirector.ScaleProjectileDamage(npc, SeaShrimpDirector.WaterBoltDamage);
                            for (int i = 0; i < SeaShrimpDirector.LeapBoltCount; i++) {
                                float spread = MathHelper.Lerp(-1.15f, 1.15f, i / (SeaShrimpDirector.LeapBoltCount - 1f))
                                    + Main.rand.NextFloat(-0.06f, 0.06f);
                                Vector2 vel = (-Vector2.UnitY).RotatedBy(spread) * Main.rand.NextFloat(9f, 16f);
                                Projectile.NewProjectile(npc.GetSource_FromAI(), impact - Vector2.UnitY * 30f, vel,
                                    ModContent.ProjectileType<SeaShrimpWaterBolt>(), boltDamage, 1f, Main.myPlayer);
                            }
                        }
                    }
                    return null;
                }
                default: {
                    //落地余韵：脊柱压缩回弹（次级动作），随后收招
                    HoldInPlace(ctx);
                    float r = MathHelper.Clamp((t - phaseStart) / (float)SettleFrames, 0f, 1f);
                    ctx.SpineCurl = MathHelper.Lerp(0.55f, -0.08f, r * r * (3f - 2f * r));
                    ctx.TailFlare = 0.35f;
                    if (t - phaseStart >= SettleFrames) {
                        return EndAttack(ctx, 55);
                    }
                    return null;
                }
            }
        }

        /// <summary>被全局转移打断（蜕壳/死亡/离场）时清掉弹道残速——演出状态不被惯性顶飞</summary>
        public override void OnExit(SeaShrimpStateContext ctx) {
            ctx.Owner.Locomotion.AbortBallistic();
        }
    }
}
