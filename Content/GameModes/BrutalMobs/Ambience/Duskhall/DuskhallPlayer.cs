using CalamityOverhaul.Content.Scenarios.Dungeonworld;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Duskhall
{
    /// <summary>
    /// 「被凝视」逐玩家凝视计量与幽瞳惊吓演出。<br/>
    /// 无伤害、无网络包：状态在 ModPlayer 上逐玩家持有，只有本机玩家的实例被推进与绘制，
    /// 联机时每个客户端各自被自己的幽瞳凝视（幻觉不共享，队友不可见属设计内）。<br/>
    /// 累积：同一小区域（锚半径 <see cref="AnchorRadius"/>）滞留时凝视值上涨，
    /// 档位只调累积速度（<see cref="GazeFullTicksByTier"/>）；换区大幅衰减重锚，
    /// 离开地牢快速归零，Boss 在场暂停累积。<br/>
    /// 渐进反馈：低语渐清晰（声床音量/音调随值上抬，见 DuskhallAmbience）→
    /// 烛火色调转蓝（烛具冷焰加密）→ 0.8 处一次远处呻吟预兆 →
    /// 满值幽瞳虚影于视野边缘浮现（约 78 帧潜伏凝望，瞳孔追人）→
    /// 直冲脸前消散（惊吓拍：音效+粒子+极轻屏震，零伤害）。<br/>
    /// 与 Mournfog 怨聚（鬼火实体物理围拢）的差异：本机制是计量驱动的视野边缘虚影，
    /// 无实体无碰撞，靠"潜伏被瞥见"与"冲脸消散"完成惊吓
    /// </summary>
    internal class DuskhallPlayer : ModPlayer
    {
        /// <summary>凝视值 0~1</summary>
        internal float Gaze;

        internal enum EyePhase : byte
        {
            None,
            /// <summary>潜伏：视野边缘淡入凝望</summary>
            Lurk,
            /// <summary>冲脸：加速直扑面门</summary>
            Rush,
            /// <summary>消散：爆点余辉</summary>
            Burst,
        }

        internal EyePhase Phase;
        internal int PhaseTimer;
        internal Vector2 EyePos;
        internal Vector2 EyeVel;
        internal float EyeAlpha;
        /// <summary>瞳孔盯人偏移（像素级，绘制端直接加）</summary>
        internal Vector2 PupilLook;

        private Vector2 anchorPos;
        private bool anchored;
        private int refractory;
        private bool preHinted;

        /// <summary>满凝视所需滞留帧数，档位只调速度（75s / 60s / 46s）</summary>
        private static readonly int[] GazeFullTicksByTier = [4500, 3600, 2760];
        /// <summary>视作"同一小区域"的锚半径（像素，约 22 格）</summary>
        private const float AnchorRadius = 360f;
        /// <summary>惊吓后的不应期（15s 内不再累积）</summary>
        private const int RefractoryTicks = 900;
        /// <summary>潜伏凝望时长（先看见再被扑，远超 45 帧可读底线）</summary>
        private const int LurkTicks = 78;
        /// <summary>冲脸兜底帧数（正常按距离提前结算）</summary>
        private const int RushMaxTicks = 26;
        /// <summary>爆点余辉帧数</summary>
        private const int BurstTicks = 18;

        public override void PostUpdate() {
            //纯本机演出：只有所有者客户端推进（服务端 myPlayer=255 恒跳过）
            if (Main.dedServ || Player.whoAmI != Main.myPlayer || Main.gamePaused) {
                return;
            }

            bool inScene = Player.ZoneDungeon && GameModeSystem.BrutalActive && !Dungeonworld.Active;
            int tier = Math.Clamp(GameModeSystem.EffectiveTier, 1, 3);
            float gain = 1f / GazeFullTicksByTier[tier - 1];

            UpdateEye(inScene);

            if (!inScene) {
                //离开地牢：快速衰减重置
                Gaze = Math.Max(0f, Gaze - gain * 4f);
                anchored = false;
            }
            else if (CWRWorld.HasBoss) {
                //Boss 在场：暂停累积，缓慢回落
                Gaze = Math.Max(0f, Gaze - gain * 0.5f);
            }
            else if (refractory > 0) {
                refractory--;
            }
            else if (Phase == EyePhase.None) {
                if (!anchored) {
                    anchored = true;
                    anchorPos = Player.Center;
                }
                if (Player.Center.Distance(anchorPos) > AnchorRadius) {
                    //挪窝：重锚并大幅衰减（移动到新区域即视作摆脱注视）
                    anchorPos = Player.Center;
                    Gaze *= 0.45f;
                }
                else {
                    Gaze += gain;
                }

                if (Gaze >= 1f) {
                    Gaze = 1f;
                    TriggerEye();
                }
                else if (Gaze >= 0.8f && !preHinted) {
                    PreHint();
                }
            }

            if (Gaze < 0.6f) {
                preHinted = false;
            }
        }

        public override void UpdateDead() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            //死亡即断演出，计量快速回落
            CancelEye();
            Gaze = Math.Max(0f, Gaze - 0.01f);
            anchored = false;
        }

        private void CancelEye() {
            Phase = EyePhase.None;
            PhaseTimer = 0;
            EyeAlpha = 0f;
            EyeVel = Vector2.Zero;
        }

        //==================== 幽瞳状态机 ====================

        private void UpdateEye(bool inScene) {
            if (Phase == EyePhase.None) {
                return;
            }
            //中途离开地牢：不走爆点，直接速淡
            if (!inScene && Phase != EyePhase.Burst) {
                EyeAlpha -= 0.08f;
                if (EyeAlpha <= 0f) {
                    CancelEye();
                }
                return;
            }

            switch (Phase) {
                case EyePhase.Lurk:
                    PhaseTimer++;
                    EyeAlpha = Math.Min(0.62f, EyeAlpha + 0.024f);
                    //瞳孔盯人 + 轻微悬浮漂移
                    PupilLook = (Player.MountedCenter - EyePos).SafeNormalize(Vector2.UnitY) * 3.4f;
                    EyePos += new Vector2(
                        MathF.Sin(PhaseTimer * 0.11f) * 0.18f,
                        MathF.Sin(PhaseTimer * 0.07f + 1.7f) * 0.14f);
                    if (PhaseTimer % 6 == 0) {
                        Dust mist = Dust.NewDustPerfect(EyePos + Main.rand.NextVector2Circular(14f, 10f),
                            DustID.Shadowflame, new Vector2(0f, -Main.rand.NextFloat(0.2f, 0.5f)),
                            180, default, Main.rand.NextFloat(0.6f, 0.9f));
                        mist.noGravity = true;
                    }
                    if (PhaseTimer >= LurkTicks) {
                        Phase = EyePhase.Rush;
                        PhaseTimer = 0;
                        SoundEngine.PlaySound(SoundID.NPCHit36 with {
                            Volume = 0.5f, Pitch = -0.3f, MaxInstances = 2
                        }, EyePos);
                    }
                    break;

                case EyePhase.Rush: {
                    PhaseTimer++;
                    //逐帧追瞄面门：加速度曲线冲刺，保证真的贴到脸前
                    Vector2 target = Player.MountedCenter + new Vector2(0f, -6f);
                    float speed = 4f + MathF.Pow(PhaseTimer / 12f, 2f) * 42f;
                    EyeVel = (target - EyePos).SafeNormalize(Vector2.UnitY) * speed;
                    EyePos += EyeVel;
                    EyeAlpha = Math.Min(0.95f, EyeAlpha + 0.08f);
                    PupilLook = Vector2.Zero;
                    if (EyePos.Distance(target) < 30f || PhaseTimer >= RushMaxTicks) {
                        DoBurst();
                    }
                    break;
                }

                case EyePhase.Burst:
                    PhaseTimer++;
                    EyeAlpha = Math.Max(0f, EyeAlpha - 1f / BurstTicks);
                    if (PhaseTimer >= BurstTicks) {
                        CancelEye();
                    }
                    break;
            }
        }

        /// <summary>满值触发：视野边缘（优先身后半侧）挑一处非实心空位浮现</summary>
        private void TriggerEye() {
            float backAng = Player.direction > 0 ? MathHelper.Pi : 0f;
            Vector2 chosen = Player.Center + backAng.ToRotationVector2() * 460f;
            for (int i = 0; i < 8; i++) {
                //前 4 次试身后半侧，后 4 次放开到任意方位
                float ang = backAng + Main.rand.NextFloat(-1.05f, 1.05f) + (i >= 4 ? MathHelper.Pi : 0f);
                Vector2 pos = Player.Center + ang.ToRotationVector2() * Main.rand.NextFloat(430f, 520f);
                Point tp = pos.ToTileCoordinates();
                if (!WorldGen.InWorld(tp.X, tp.Y, 10) || WorldGen.SolidTile(tp.X, tp.Y)) {
                    continue;
                }
                chosen = pos;
                break;
            }

            Phase = EyePhase.Lurk;
            PhaseTimer = 0;
            EyePos = chosen;
            EyeAlpha = 0f;
            EyeVel = Vector2.Zero;
            SoundEngine.PlaySound(SoundID.ZombieMoan with {
                Volume = 0.3f, Pitch = -0.35f, MaxInstances = 2
            }, chosen);
        }

        /// <summary>0.8 预兆：一声更近的呻吟 + 边缘幽影碎屑，提示"再不走就要被盯上了"</summary>
        private void PreHint() {
            preHinted = true;
            Vector2 pos = Player.Center + Main.rand.NextVector2Unit() * 460f;
            SoundEngine.PlaySound(SoundID.ZombieMoan with {
                Volume = 0.2f, Pitch = -0.2f, MaxInstances = 2
            }, pos);
            for (int i = 0; i < 4; i++) {
                Dust shade = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(18f, 12f),
                    DustID.Shadowflame, Main.rand.NextVector2Circular(0.6f, 0.4f),
                    170, default, Main.rand.NextFloat(0.6f, 0.9f));
                shade.noGravity = true;
            }
        }

        /// <summary>惊吓拍：脸前爆散（零伤害）。粒子 16、极轻屏震、双层音效，随后进入不应期</summary>
        private void DoBurst() {
            Phase = EyePhase.Burst;
            PhaseTimer = 0;
            for (int i = 0; i < 10; i++) {
                Dust shard = Dust.NewDustPerfect(EyePos, DustID.IceTorch,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6.5f),
                    90, default, Main.rand.NextFloat(1f, 1.4f));
                shard.noGravity = true;
            }
            for (int i = 0; i < 6; i++) {
                Dust shade = Dust.NewDustPerfect(EyePos, DustID.Shadowflame,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f),
                    150, default, Main.rand.NextFloat(0.8f, 1.1f));
                shade.noGravity = true;
            }
            Player.CWR().GetScreenShake(3f);
            SoundEngine.PlaySound(SoundID.NPCDeath39 with {
                Volume = 0.5f, Pitch = -0.15f, MaxInstances = 2
            }, EyePos);
            SoundEngine.PlaySound(SoundID.ZombieMoan with {
                Volume = 0.38f, Pitch = 0.5f, MaxInstances = 2
            }, EyePos);
            Gaze = 0f;
            refractory = RefractoryTicks;
        }
    }
}
