using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;

namespace CalamityOverhaul.Content.Tiles.BloodAltars
{
    /// <summary>
    /// 献祭仪式的客户端演出：节拍表、粒子派发、着色器参数与屏幕反馈都在这里，
    /// 只读 TP 的阶段与阶段计时，自己不改任何玩法状态。<br/>
    /// 各端独立推进，因此不依赖包到达的精确帧；血柱的生长/侵蚀跨阶段边界，
    /// 由自带的 columnAge 承担而不是 PhaseTimer
    /// </summary>
    internal sealed class BloodAltarRite
    {
        //---- 节拍常量 ----
        /// <summary>供奉期每几帧抛一团血浆</summary>
        private const int OfferingGoutInterval = 5;
        /// <summary>沸腾期每几帧从池面抛一团</summary>
        private const int BoilGoutInterval = 4;
        /// <summary>沸腾末段完全定住的帧数，喷发前的预备拍</summary>
        private const int BoilHoldFrames = 3;
        /// <summary>血柱窜到顶所用帧数</summary>
        private const int ColumnRiseFrames = 14;
        /// <summary>血柱自根部被啃掉的起始帧与时长</summary>
        private const int ColumnDrainStart = 22;
        private const int ColumnDrainFrames = 46;
        /// <summary>常驻期碗沿多久滴一次</summary>
        private const int DripInterval = 40;

        private const float FillOffering = 0.55f;
        private const float FillErupt = 0.78f;
        private const float FillResident = 0.42f;
        private const float ColumnLengthPx = 560f;
        private const float ColumnWidthPx = 44f;
        /// <summary>粒子生成的距离闸：屏外的祭坛不该占粒子预算</summary>
        private const float ParticleRange = 2400f;
        private const float ScreenFxRange = 1500f;

        //---- 着色器参数 ----
        public float Fill { get; private set; }
        public float Boil { get; private set; }
        public float Rise { get; private set; }
        public float Drain { get; private set; }
        public float Sigil { get; private set; }
        public float SigilOpen { get; private set; }
        public float Flash { get; private set; }
        public float PulseWave { get; private set; }
        public float FxTime { get; private set; }
        public float Seed { get; }
        public float ColumnLength => ColumnLengthPx;
        public float ColumnWidth => ColumnWidthPx;
        public Vector3 LightColor { get; private set; }

        private readonly Vector4[] ripples = new Vector4[4];
        private int rippleCursor;
        private int columnAge = -1;
        private float beatPhase;
        private float pulsePhase;
        private Vector2 poolTopLeft;
        private float poolSurfaceY;

        public BloodAltarRite() {
            Seed = Main.rand.NextFloat(0f, 40f);
            beatPhase = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public Vector4 GetRipple(int index) => ripples[index];

        /// <summary>把一圈涟漪推回池面。世界坐标会换算成池面 quad 的 uv</summary>
        public void PushRipple(Vector2 worldPos, float strength) {
            if (BloodAltarFx.PoolWidth <= 0f) {
                return;
            }
            float u = (worldPos.X - poolTopLeft.X) / BloodAltarFx.PoolWidth;
            float v = (worldPos.Y - poolTopLeft.Y) / BloodAltarFx.PoolHeight;
            ripples[rippleCursor] = new Vector4(MathHelper.Clamp(u, 0f, 1f)
                , MathHelper.Clamp(v, 0f, 1f), 0f, MathHelper.Clamp(strength, 0.1f, 1f) * 0.028f);
            rippleCursor = (rippleCursor + 1) % ripples.Length;
        }

        // ============================== 每帧 ==============================

        public void Tick(BloodAltarTP tp) {
            Vector2 bowl = tp.BowlCenter;
            poolTopLeft = bowl + new Vector2(-BloodAltarFx.PoolWidth * 0.5f, -3f);
            poolSurfaceY = poolTopLeft.Y + BloodAltarFx.PoolHeight * (1f - MathF.Max(Fill, 0.05f));

            bool frozen = IsAnticipationHold(tp);
            if (!frozen) {
                FxTime += 1f / 60f;
                pulsePhase += 0.021f;
                beatPhase += 0.038f;
            }

            AdvanceRipples(frozen);
            AdvanceColumn(tp);
            DriveTargets(tp);
            PulseWave = 0.5f + 0.5f * MathF.Sin(pulsePhase);
            Flash = MathF.Max(0f, Flash - 0.075f);
            DriveLight(tp);

            if (InParticleRange(bowl)) {
                SpawnPhaseParticles(tp, bowl);
            }
            if (tp.Phase == BloodAltarPhase.Boil && OnLocalScreen(bowl)) {
                Main.LocalPlayer.CWR()?.GetScreenShake(1.2f + Boil * 1.1f);
            }
        }

        /// <summary>沸腾末段的定格：这三帧连噪声时间都停住，喷发才有落点</summary>
        private bool IsAnticipationHold(BloodAltarTP tp)
            => tp.Phase == BloodAltarPhase.Boil && tp.PhaseTimer > BloodAltarTP.BoilFrames - BoilHoldFrames;

        private void AdvanceRipples(bool frozen) {
            if (frozen) {
                return;
            }
            for (int i = 0; i < ripples.Length; i++) {
                if (ripples[i].W <= 0f) {
                    continue;
                }
                ripples[i].Z += 1f / 22f;
                if (ripples[i].Z >= 1f) {
                    ripples[i] = Vector4.Zero;
                }
            }
        }

        private void AdvanceColumn(BloodAltarTP tp) {
            if (tp.Phase == BloodAltarPhase.Erupt && columnAge < 0) {
                columnAge = 0;
            }
            if (columnAge < 0) {
                Rise = 0f;
                Drain = 0f;
                return;
            }

            columnAge++;
            Rise = MathF.Pow(MathHelper.Clamp(columnAge / (float)ColumnRiseFrames, 0f, 1f), 0.55f);
            Drain = MathHelper.Clamp((columnAge - ColumnDrainStart) / (float)ColumnDrainFrames, 0f, 1f);
            if (Drain >= 1f || tp.Phase is BloodAltarPhase.Idle or BloodAltarPhase.Recede) {
                columnAge = -1;
            }
        }

        private void DriveTargets(BloodAltarTP tp) {
            float fillTarget;
            float boilTarget;
            float sigilTarget;

            switch (tp.Phase) {
                case BloodAltarPhase.Offering: {
                    float t = MathHelper.Clamp(tp.PhaseTimer / (float)BloodAltarTP.OfferingFrames, 0f, 1f);
                    fillTarget = FillOffering * Smooth01(t);
                    boilTarget = 0.12f * t;
                    sigilTarget = 0f;
                    break;
                }
                case BloodAltarPhase.Boil: {
                    float t = MathHelper.Clamp(tp.PhaseTimer / (float)BloodAltarTP.BoilFrames, 0f, 1f);
                    fillTarget = MathHelper.Lerp(FillOffering, FillErupt, Smooth01(t));
                    boilTarget = Smooth01(t);
                    sigilTarget = 0f;
                    break;
                }
                case BloodAltarPhase.Erupt: {
                    float t = MathHelper.Clamp(tp.PhaseTimer / (float)BloodAltarTP.EruptFrames, 0f, 1f);
                    //血被抽上柱子，池面随之回落
                    fillTarget = MathHelper.Lerp(FillErupt, FillResident, Smooth01(t));
                    boilTarget = MathHelper.Lerp(1f, 0.30f, t);
                    sigilTarget = Smooth01(MathHelper.Clamp((tp.PhaseTimer - BloodAltarTP.MoonRiseFrame) / 20f, 0f, 1f));
                    break;
                }
                case BloodAltarPhase.Active:
                    fillTarget = FillResident + PulseWave * 0.03f;
                    boilTarget = 0.18f;
                    sigilTarget = 1f;
                    break;
                case BloodAltarPhase.Recede: {
                    float t = MathHelper.Clamp(tp.PhaseTimer / (float)BloodAltarTP.RecedeFrames, 0f, 1f);
                    fillTarget = FillResident * (1f - Smooth01(t));
                    boilTarget = 0.10f * (1f - t);
                    sigilTarget = 1f - Smooth01(t);
                    break;
                }
                default:
                    fillTarget = 0f;
                    boilTarget = 0f;
                    sigilTarget = 0f;
                    break;
            }

            Fill = Approach(Fill, fillTarget, 0.10f);
            Boil = Approach(Boil, boilTarget, 0.09f);
            Sigil = Approach(Sigil, sigilTarget, 0.05f);
            //血线沿圆周流开比整体淡入慢一截，读成"正在写"而不是"整圈亮起"
            SigilOpen = Approach(SigilOpen, sigilTarget > 0.02f ? 1f : 0f, 0.016f);
        }

        private void DriveLight(BloodAltarTP tp) {
            float power = tp.Phase switch {
                BloodAltarPhase.Offering => 0.5f + Fill * 1.1f,
                BloodAltarPhase.Boil => 0.9f + Boil * 1.0f,
                BloodAltarPhase.Erupt => 1.5f + Rise * 0.9f,
                BloodAltarPhase.Active => 1.4f + PulseWave * 0.9f,
                BloodAltarPhase.Recede => 0.9f * (1f - tp.PhaseTimer / (float)BloodAltarTP.RecedeFrames),
                _ => 0.32f,
            };
            LightColor = Color.DarkRed.ToVector3() * (power + Flash * 1.6f);
        }

        // ============================ 阶段入场 ============================

        public void OnPhaseEnter(BloodAltarTP tp) {
            Vector2 bowl = tp.BowlCenter;
            switch (tp.Phase) {
                case BloodAltarPhase.Offering:
                    SoundEngine.PlaySound(CWRSound.MechanicalFullBloodFlow with {
                        Pitch = -0.40f,
                        Volume = 0.55f,
                        MaxInstances = 2,
                    }, bowl);
                    if (InParticleRange(bowl)) {
                        BurstOrbShells(OfferingSource(tp));
                        PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(bowl, Vector2.Zero
                            , BloodAltarFx.ColWet, 0.5f)?.Configure(0.24f, 1.15f, 30);
                    }
                    break;

                case BloodAltarPhase.Boil:
                    SoundEngine.PlaySound(CWRSound.Accumulator with {
                        Pitch = -0.50f,
                        Volume = 0.42f,
                        MaxInstances = 2,
                    }, bowl);
                    break;

                case BloodAltarPhase.Erupt:
                    SoundEngine.PlaySound(CWRSound.OutburstRelease with { Pitch = -0.25f, Volume = 0.70f }, bowl);
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                        Pitch = -0.62f,
                        Volume = 0.45f,
                        MaxInstances = 3,
                    }, bowl);
                    if (OnLocalScreen(bowl)) {
                        Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                            bowl, -Vector2.UnitY, 6f, 6f, 22, 900f, "CWRBloodAltarErupt"));
                    }
                    if (InParticleRange(bowl)) {
                        //柱根炸开的一圈：血从石头缝里被顶出来
                        for (int i = 0; i < 22; i++) {
                            float ang = MathHelper.TwoPi * i / 22f;
                            Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(2.4f, 6.5f);
                            vel.Y = vel.Y * 0.45f - Main.rand.NextFloat(1.2f, 3.6f);
                            PRTLoader.NewParticle<PRT_HeartcarverDroplet>(bowl, vel
                                , Main.rand.NextBool(3) ? BloodAltarFx.ColDeep : BloodAltarFx.ColWet
                                , Main.rand.NextFloat(1.0f, 1.7f))?.Configure(Main.rand.Next(26, 44), 0.30f);
                        }
                        PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(bowl, Vector2.Zero
                            , BloodAltarFx.ColDeep, 0.85f)?.Configure(0.35f, 2.3f, 26);
                    }
                    break;

                case BloodAltarPhase.Active:
                    break;

                case BloodAltarPhase.Recede:
                    SoundEngine.PlaySound(CWRSound.Peuncharge, bowl);
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Pitch = -0.72f,
                        Volume = 0.42f,
                        MaxInstances = 2,
                    }, bowl);
                    if (InParticleRange(bowl)) {
                        //向池心塌陷：向内向下的液滴，配干血烟
                        for (int i = 0; i < 16; i++) {
                            Vector2 from = bowl + new Vector2(Main.rand.NextFloat(-26f, 26f), Main.rand.NextFloat(-8f, 4f));
                            Vector2 vel = from.To(bowl).UnitVector() * Main.rand.NextFloat(1.4f, 3.4f);
                            PRTLoader.NewParticle<PRT_HeartcarverDroplet>(from, vel, BloodAltarFx.ColDeep
                                , Main.rand.NextFloat(0.8f, 1.3f))?.Configure(Main.rand.Next(16, 26), 0.22f);
                        }
                        for (int i = 0; i < 4; i++) {
                            PRTLoader.NewParticle<PRT_CrimsonSmoke>(bowl + Main.rand.NextVector2Circular(20f, 8f)
                                , new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.2f, 0.7f))
                                , Color.White, Main.rand.NextFloat(0.12f, 0.20f))
                                ?.Configure(Main.rand.Next(40, 62), BloodAltarFx.ColDeep, BloodAltarFx.ColDry, 0.010f);
                        }
                    }
                    break;
            }
        }

        /// <summary>血珠不足时的一记闷响，与阶段无关</summary>
        public static void PlayRejectBeat(Vector2 bowl) {
            SoundEngine.PlaySound(SoundID.NPCDeath13 with { Pitch = -0.70f, Volume = 0.50f }, bowl);
            SoundEngine.PlaySound(CWRSound.HitTheFlesh_2 with { Pitch = -0.35f, Volume = 0.45f }, bowl);
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 10; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-2.2f, 2.2f), -Main.rand.NextFloat(1.0f, 3.2f));
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(bowl, vel, BloodAltarFx.ColDeep
                    , Main.rand.NextFloat(0.7f, 1.2f))?.Configure(Main.rand.Next(16, 26), 0.30f);
            }
        }

        // ============================ 逐帧粒子 ============================

        private void SpawnPhaseParticles(BloodAltarTP tp, Vector2 bowl) {
            switch (tp.Phase) {
                case BloodAltarPhase.Offering:
                    if (tp.PhaseTimer % OfferingGoutInterval == 0
                        && tp.PhaseTimer < BloodAltarTP.OfferingFrames - 6) {
                        LobOffering(OfferingSource(tp), bowl);
                    }
                    break;

                case BloodAltarPhase.Boil:
                    if (IsAnticipationHold(tp)) {
                        break;
                    }
                    if (tp.PhaseTimer % BoilGoutInterval == 0) {
                        TossFromPool(bowl);
                    }
                    if (tp.PhaseTimer % 7 == 0) {
                        PRTLoader.NewParticle<PRT_CrimsonSmoke>(bowl + Main.rand.NextVector2Circular(18f, 6f)
                            , new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.3f, 0.9f))
                            , Color.White, Main.rand.NextFloat(0.09f, 0.16f))
                            ?.Configure(Main.rand.Next(30, 48), BloodAltarFx.ColDeep, BloodAltarFx.ColDry, 0.012f);
                    }
                    break;

                case BloodAltarPhase.Erupt:
                    if (tp.PhaseTimer == BloodAltarTP.MoonRiseFrame) {
                        MoonRiseBeat(tp, bowl);
                    }
                    ShedFromColumnTip(bowl);
                    break;

                case BloodAltarPhase.Active:
                    ShedFromColumnTip(bowl);
                    if (tp.AliveTime % DripInterval == 0) {
                        DripFromRim(bowl);
                    }
                    if (tp.AliveTime % 26 == 0) {
                        PRTLoader.NewParticle<PRT_CrimsonSmoke>(bowl + Main.rand.NextVector2Circular(16f, 5f)
                            , new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.2f, 0.6f))
                            , Color.White, Main.rand.NextFloat(0.07f, 0.13f))
                            ?.Configure(Main.rand.Next(34, 52), BloodAltarFx.ColDeep, BloodAltarFx.ColDry, 0.009f);
                    }
                    break;
            }
        }

        /// <summary>定血月的那一帧：屏幕血闪 + 吼声 + 柱顶大喷</summary>
        private void MoonRiseBeat(BloodAltarTP tp, Vector2 bowl) {
            Flash = 1f;
            SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.35f, Volume = 0.80f }, bowl);
            SoundEngine.PlaySound(CWRSound.MechanicalFullBloodFlow with {
                Pitch = -0.85f,
                Volume = 0.50f,
                MaxInstances = 2,
            }, bowl);

            if (OnLocalScreen(bowl)) {
                //BloodAltarRender.Trigger(bowl);
                Main.LocalPlayer.CWR()?.GetScreenShake(7f);
                VaultUtils.Text(BloodAltarTP.ApproachingText.Value, Color.DarkRed);
            }

            Vector2 tip = bowl - Vector2.UnitY * (ColumnLengthPx * Rise);
            FishBloodyManowarVFX.DropletSpray(tip, -Vector2.UnitY, 26, 4f, 13f, 0.85f, 0.34f);
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(tip, Vector2.Zero
                , BloodAltarFx.ColWet, 0.9f)?.Configure(0.30f, 2.6f, 28);
        }

        /// <summary>血浆从供奉者手里被抛进碗中，走真弹道而不是直线平移</summary>
        private void LobOffering(Vector2 from, Vector2 bowl) {
            Vector2 target = new(bowl.X + Main.rand.NextFloat(-14f, 14f), poolSurfaceY);
            const float gravity = 0.34f;
            float flight = MathHelper.Clamp(Vector2.Distance(from, target) / 9f, 14f, 34f);
            Vector2 vel = (target - from) / flight - new Vector2(0f, 0.5f * gravity * flight);

            PRTLoader.NewParticle<PRT_BloodGout>(from, vel
                , Main.rand.NextBool(3) ? BloodAltarFx.ColDeep : BloodAltarFx.ColWet
                , Main.rand.NextFloat(0.95f, 1.45f))
                ?.Configure((int)flight + 8, gravity, poolSurfaceY, bowl.X
                    , BloodAltarFx.PoolWidth * 0.5f, this);

            SoundEngine.PlaySound((Main.rand.NextBool() ? CWRSound.HitTheFlesh_1 : CWRSound.HitTheFlesh_2) with {
                Pitch = Main.rand.NextFloat(-0.55f, -0.25f),
                Volume = 0.28f,
                MaxInstances = 3,
            }, from);
        }

        /// <summary>沸腾：池面自己顶出一团，落回时把涟漪推回去</summary>
        private void TossFromPool(Vector2 bowl) {
            Vector2 from = new(bowl.X + Main.rand.NextFloat(-18f, 18f), poolSurfaceY - 1f);
            Vector2 vel = new(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(2.6f, 5.4f) * (0.5f + Boil));
            PRTLoader.NewParticle<PRT_BloodGout>(from, vel, BloodAltarFx.ColWet
                , Main.rand.NextFloat(0.7f, 1.15f))
                ?.Configure(40, 0.34f, poolSurfaceY, bowl.X, BloodAltarFx.PoolWidth * 0.5f, this);
        }

        /// <summary>柱顶断裂处掉下来的液滴：柱子活着就一直掉</summary>
        private void ShedFromColumnTip(Vector2 bowl) {
            if (columnAge < 0 || Rise <= 0.2f || !Main.rand.NextBool(2)) {
                return;
            }
            float along = Main.rand.NextFloat(MathF.Max(Drain, 0.55f), 1f);
            Vector2 pos = bowl - Vector2.UnitY * (ColumnLengthPx * Rise * along)
                + new Vector2(Main.rand.NextFloat(-12f, 12f), 0f);
            Vector2 vel = new(Main.rand.NextFloat(-1.6f, 1.6f), Main.rand.NextFloat(-2.4f, 1.2f));
            PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel
                , Main.rand.NextBool(3) ? BloodAltarFx.ColDry : BloodAltarFx.ColDeep
                , Main.rand.NextFloat(0.7f, 1.25f))?.Configure(Main.rand.Next(26, 42), 0.32f);
        }

        /// <summary>碗沿挂不住的一滴：飞出去、贴到地上、再慢慢渗</summary>
        private void DripFromRim(Vector2 bowl) {
            float side = Main.rand.NextBool() ? 1f : -1f;
            Vector2 from = bowl + new Vector2(side * BloodAltarFx.PoolWidth * 0.46f, 4f);
            PRTLoader.NewParticle<PRT_CrimsonBloodStain>(from
                , new Vector2(side * Main.rand.NextFloat(0.1f, 0.5f), Main.rand.NextFloat(0.6f, 1.3f))
                , BloodAltarFx.ColDeep, Main.rand.NextFloat(1.1f, 1.7f))
                ?.Configure(Main.rand.Next(40, 62), 0.34f, 0.99f, stuckLifetime: Main.rand.Next(56, 80));
        }

        private void BurstOrbShells(Vector2 from) {
            for (int i = 0; i < 9; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.6f, 4.6f);
                vel.Y -= Main.rand.NextFloat(0.4f, 1.6f);
                PRTLoader.NewParticle<PRT_BloodOrbShell>(from + Main.rand.NextVector2Circular(6f, 6f), vel
                    , BloodAltarFx.ColWet, Main.rand.NextFloat(0.7f, 1.2f))
                    ?.Configure(Main.rand.Next(26, 42));
            }
        }

        private static Vector2 OfferingSource(BloodAltarTP tp) {
            int who = tp.summonerPlayer;
            if (who >= 0 && who < Main.maxPlayers && Main.player[who].active && !Main.player[who].dead) {
                return Main.player[who].Center;
            }
            return tp.BowlCenter - Vector2.UnitY * 48f;
        }

        // ============================== 绘制 ==============================

        /// <summary>地表层：血纹环压在地面上，画在祭坛本体之后但在血面之前</summary>
        public void DrawUnderAltar(SpriteBatch spriteBatch, BloodAltarTP tp) {
            if (Main.dedServ) {
                return;
            }
            BloodAltarFx.DrawSigil(spriteBatch, this, tp.CenterInWorld + new Vector2(0f, tp.Size.Y * 0.5f - 4f));
        }

        /// <summary>碗内血面</summary>
        public void DrawPool(SpriteBatch spriteBatch, BloodAltarTP tp) {
            if (Main.dedServ) {
                return;
            }
            BloodAltarFx.DrawPool(spriteBatch, this, tp.BowlCenter);
        }

        /// <summary>最上层：血柱、牵引血丝、悬浮供奉物</summary>
        public void DrawOverAltar(SpriteBatch spriteBatch, BloodAltarTP tp) {
            if (Main.dedServ) {
                return;
            }

            Vector2 bowl = tp.BowlCenter;
            BloodAltarFx.DrawGeyser(spriteBatch, this, bowl);
            DrawIntakeThreads(spriteBatch, tp, bowl);

            if (tp.Phase == BloodAltarPhase.Offering) {
                Vector2 from = OfferingSource(tp);
                float alpha = MathF.Min(1f, tp.PhaseTimer / 8f)
                    * (1f - Smooth01(MathHelper.Clamp((tp.PhaseTimer - 24) / 11f, 0f, 1f)));
                FishBloodyManowarVFX.DrawBloodThread(spriteBatch, from, bowl, 1f, alpha * 0.9f, Seed);
            }

            if (tp.Phase == BloodAltarPhase.Active) {
                DrawOffering(spriteBatch, bowl - Vector2.UnitY * 54f, 1f);
            }
            else if (tp.Phase == BloodAltarPhase.Idle && tp.HoverGlow) {
                //悬停预览：告诉玩家这座祭坛吃什么
                DrawOffering(spriteBatch, bowl - Vector2.UnitY * 34f, 0.62f);
            }
        }

        /// <summary>血月期间被拖过来的血珠：牵一条粘丝，沿途掉渣</summary>
        private void DrawIntakeThreads(SpriteBatch spriteBatch, BloodAltarTP tp, Vector2 bowl) {
            if (tp.Phase != BloodAltarPhase.Active) {
                return;
            }

            int drawn = 0;
            foreach (Item orb in Main.ActiveItems) {
                if (orb.type != CWRID.Item_BloodOrb) {
                    continue;
                }
                float dist = Vector2.Distance(orb.Center, bowl);
                if (dist > 640f || dist < 24f) {
                    continue;
                }

                float alpha = MathHelper.Clamp(1f - dist / 640f, 0.15f, 0.9f);
                FishBloodyManowarVFX.DrawBloodThread(spriteBatch, orb.Center, bowl, 0.7f, alpha, Seed + orb.whoAmI);
                if (Main.rand.NextBool(14)) {
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(orb.Center
                        , new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(0.2f, 1.1f))
                        , BloodAltarFx.ColDeep, Main.rand.NextFloat(0.5f, 0.9f))
                        ?.Configure(Main.rand.Next(18, 28), 0.26f);
                }
                if (++drawn >= 6) {
                    return;
                }
            }
        }

        /// <summary>
        /// 悬浮供奉物：血珠贴图当本体（可读性靠它），外面裹一层湿血壳，
        /// 下缘挂丝滴落，缩放走心跳式的不对称脉冲而不是正弦上下浮动
        /// </summary>
        private void DrawOffering(SpriteBatch spriteBatch, Vector2 center, float presence) {
            float beat = MathF.Pow(MathF.Max(0f, MathF.Sin(beatPhase)), 5f);
            float sx = (1f + beat * 0.17f) * presence;
            float sy = (1f - beat * 0.11f) * presence;
            Vector2 pos = center + new Vector2(0f, MathF.Sin(pulsePhase * 1.7f) * 2.5f) - Main.screenPosition;

            Texture2D shell = CWRAsset.Extra_98?.Value;
            if (shell != null) {
                Vector2 origin = shell.Size() * 0.5f;
                //湿血壳分两层：外层薄而大、内层浓而小，靠层间频率差撑出体积
                spriteBatch.Draw(shell, pos, null, BloodAltarFx.ColDeep * (0.55f * presence), 0f, origin
                    , new Vector2(0.42f * sx, 0.46f * sy), SpriteEffects.None, 0f);
                spriteBatch.Draw(shell, pos, null, BloodAltarFx.ColWet * (0.75f * presence), 0f, origin
                    , new Vector2(0.26f * sx, 0.30f * sy), SpriteEffects.None, 0f);
            }

            int type = CWRID.Item_BloodOrb;
            if (type > 0) {
                Main.instance.LoadItem(type);
                Texture2D tex = TextureAssets.Item[type].Value;
                spriteBatch.Draw(tex, pos, null, Color.White * presence, 0f, tex.Size() * 0.5f
                    , new Vector2(sx, sy), SpriteEffects.None, 0f);
            }

            //挂在下缘的一根丝，血是会往下淌的
            if (shell != null && presence > 0.9f) {
                float hang = 6f + beat * 5f;
                FishBloodyManowarVFX.DrawBloodThread(spriteBatch
                    , center + new Vector2(0f, 8f)
                    , center + new Vector2(MathF.Sin(pulsePhase * 0.9f) * 3f, 8f + hang)
                    , 0.4f, 0.55f, Seed + 3.1f);
            }
        }

        // ============================== 工具 ==============================

        private static float Smooth01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        private static float Approach(float current, float target, float rate)
            => current + (target - current) * rate;

        private static bool InParticleRange(Vector2 pos)
            => Main.LocalPlayer?.active == true
                && Vector2.DistanceSquared(Main.LocalPlayer.Center, pos) < ParticleRange * ParticleRange;

        private static bool OnLocalScreen(Vector2 pos)
            => Main.LocalPlayer?.active == true
                && Vector2.DistanceSquared(Main.LocalPlayer.Center, pos) < ScreenFxRange * ScreenFxRange;
    }
}
