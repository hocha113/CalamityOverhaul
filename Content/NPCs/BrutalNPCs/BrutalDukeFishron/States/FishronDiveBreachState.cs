using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.States
{
    /// <summary>
    /// 潜浪跃袭：尾梢上翘的小跳预备→扎进海里→贴着水面下掠行，
    /// 沸腾的水线替他行走→破水腾空，整周翻滚甩出鲨鱼龙→再入水或收势。
    /// 破水点在起跳前 <see cref="BreachLockFrames"/> 帧冻结（沸腾聚点即承诺），
    /// 翻滚整数周数，出旋时身体自然对回航向
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FishronStateIndex.DiveBreach, typeof(FishronStateContext))]
    internal class FishronDiveBreachState : FishronStateBase
    {
        public override string StateName => "DiveBreach";
        public override FishronStateIndex StateIndex => FishronStateIndex.DiveBreach;
        public override bool AllowFarSnap => false;

        #region 节奏常量
        /// <summary>反向预备：入水前尾梢上翘的小跳</summary>
        private const int HopTime = 14;
        /// <summary>入水寻位上限，超时直接收势</summary>
        private const int DiveTimeout = 60;
        /// <summary>水下掠行帧数</summary>
        private const int UnderTime = 46;
        /// <summary>破水点冻结提前量：沸腾水线聚拢的公平承诺</summary>
        private const int BreachLockFrames = 14;
        /// <summary>空中翻滚上限</summary>
        private const int AirTime = 54;
        /// <summary>收势帧数</summary>
        private const int ExitTime = 26;
        private const float DiveSpeed = 34f;
        private const float BreachSpeed = 32f;
        private const float AirGravity = 0.6f;
        /// <summary>翻滚整周数：整数保证出旋对回航向</summary>
        private const int RollTurns = 2;
        #endregion

        //相位 0跳 1扎 2潜 3滚 4收
        private int phase;
        private int phaseStart;
        private int loopsDone;
        private float surfaceY;
        private float breachX;
        private bool breachLocked;
        private bool sharkTossed;
        private int spinSign;
        private float airBaseRot;

        public FishronDiveBreachState() {
        }

        private static int MaxLoops(FishronStateContext ctx) => ctx.Phase >= 2 ? 2 : 1;

        /// <summary>选招门：玩家脚下 1100px 内有真水面才允许潜浪</summary>
        internal static bool WaterReachable(FishronStateContext context) {
            if (!context.Target.Alives()) {
                return false;
            }
            Vector2 surface = FishronMotionFX.FindSurfaceBelow(context.Target.Center - new Vector2(0, 40f), out bool isWater);
            return isWater && surface.Y - context.Target.Center.Y < 1100f;
        }

        public override void OnEnter(FishronStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            phase = 0;
            phaseStart = 0;
            loopsDone = 0;
            breachLocked = false;
            sharkTossed = false;
            spinSign = 0;
            //鳍面拍水的起手声
            SoundEngine.PlaySound(SoundID.NPCHit14 with { Pitch = -0.3f, Volume = 0.8f, MaxInstances = 3 }, context.Npc.Center);
        }

        public override IFishronState OnUpdate(FishronStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;
            int t = (int)Timer - phaseStart;

            switch (phase) {
                case 0:
                    return UpdateHop(context, npc, player, t);
                case 1:
                    return UpdateDiveIn(context, npc, player, t);
                case 2:
                    return UpdateUnderwater(context, npc, player, t);
                case 3:
                    return UpdateAirRoll(context, npc, player, t);
                default:
                    return UpdateExit(context, npc, player, t);
            }
        }

        private void EnterPhase(int next) {
            phase = next;
            phaseStart = (int)Timer;
        }

        /// <summary>相位0：反向预备，先离水面小跳拉开，pow 末段猛压</summary>
        private IFishronState UpdateHop(FishronStateContext context, NPC npc, Player player, int t) {
            //各端确定性解析入水面：本体与玩家之间偏玩家侧
            if (t == 1) {
                Vector2 probe = Vector2.Lerp(npc.Center, player.Center, 0.65f);
                Vector2 surface = FishronMotionFX.FindSurfaceBelow(probe - new Vector2(0, 60f), out bool isWater);
                if (!isWater) {
                    surface = FishronMotionFX.FindSurfaceBelow(player.Center - new Vector2(0, 40f), out isWater);
                }
                if (!isWater) {
                    //无水兜底：不硬演，直接退回悬停
                    EnterPhase(4);
                    return null;
                }
                surfaceY = surface.Y;
            }

            float progress = Math.Min(t / (float)HopTime, 1f);
            //上翘小跳：先抬头离水，末两帧猛然压头
            float lift = 1f - (float)Math.Pow(progress, 6);
            npc.velocity = Vector2.Lerp(npc.velocity, new Vector2(npc.velocity.X * 0.4f, -5.5f * lift), 0.3f);
            FaceBody(npc, player.Center, 0.14f);
            context.FrameCommand = 1;

            if (t >= HopTime) {
                EnterPhase(1);
                //一帧写满入水冲量
                Vector2 entry = new(MathHelper.Lerp(npc.Center.X, player.Center.X, 0.4f), surfaceY);
                Vector2 dir = (entry - npc.Center).SafeNormalize(Vector2.UnitY);
                //保证足够的下潜分量
                if (dir.Y < 0.45f) {
                    dir = new Vector2(dir.X, 0.45f).SafeNormalize(Vector2.UnitY);
                }
                npc.velocity = dir * DiveSpeed;
                npc.netUpdate = true;
                SoundEngine.PlaySound(SoundID.Zombie20 with { Volume = 0.8f, Pitch = -0.1f, MaxInstances = 3 }, npc.Center);
            }
            return null;
        }

        /// <summary>相位1：直线扎水，越面即溅</summary>
        private IFishronState UpdateDiveIn(FishronStateContext context, NPC npc, Player player, int t) {
            AimBodyAlongVelocity(npc);
            context.FrameCommand = 2;

            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                FishronMotionFX.SpawnSprayCone(npc.Center, -npc.velocity.SafeNormalize(Vector2.UnitY), 1, 2f, 6f, 0.5f, 0.8f);
            }

            if (npc.Center.Y > surfaceY + 30f) {
                FishronMotionFX.SpawnSplashBurst(new Vector2(npc.Center.X, surfaceY), 1.7f);
                FishronMotionFX.CameraPunch(npc.Center, 5f, 12, "FishronDiveIn", Vector2.UnitY);
                EnterPhase(2);
                breachLocked = false;
                npc.velocity *= 0.55f;
                npc.netUpdate = true;
            }
            else if (t > DiveTimeout) {
                EnterPhase(4);
            }
            return null;
        }

        /// <summary>相位2：水下掠行，沸腾水线替他行走；末段沸点冻结即破水承诺</summary>
        private IFishronState UpdateUnderwater(FishronStateContext context, NPC npc, Player player, int t) {
            //贴面下巡航：目标玩家正下方水下 130px
            float targetY = surfaceY + 130f;
            float chaseX = breachLocked ? breachX : player.Center.X;
            Vector2 goal = new(chaseX, targetY);
            Vector2 desired = (goal - npc.Center).SafeNormalize(Vector2.Zero)
                * MathHelper.Lerp(16f, 30f, Math.Min(t / 18f, 1f));
            npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.16f);
            AimBodyAlongVelocity(npc);
            context.FrameCommand = 2;

            //破水点冻结：预告即承诺，此后沸腾只在此聚拢
            if (!breachLocked && t >= UnderTime - BreachLockFrames) {
                breachLocked = true;
                breachX = player.Center.X + player.velocity.X * 16f;
            }

            //沸腾水线：跟随的低沸走线 + 冻结后在破水点越聚越猛
            if (!VaultUtils.isServer) {
                if (t % 2 == 0) {
                    float boilX = breachLocked ? breachX : npc.Center.X;
                    float gather = breachLocked ? 1f + (t - (UnderTime - BreachLockFrames)) / (float)BreachLockFrames : 0.5f;
                    Vector2 boil = new(boilX + Main.rand.NextFloat(-70f, 70f) * (breachLocked ? 0.5f : 1.4f), surfaceY);
                    FishronMotionFX.SpawnSprayCone(boil, -Vector2.UnitY, 1 + (int)gather, 1.5f, 3f + gather * 3f, 0.35f, 0.8f);
                    InnoVault.PRT.PRTLoader.NewParticle<PRT_FishronFoam>(
                        boil + new Vector2(0, -4f), -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1f),
                        FishronMotionFX.FoamWhite * (0.3f + gather * 0.25f),
                        Main.rand.NextFloat(0.7f, 1.2f))?.Configure(Main.rand.Next(16, 28), 0.01f);
                }
                if (t % 16 == 0) {
                    SoundEngine.PlaySound(SoundID.Drown with { Volume = 0.4f, Pitch = -0.5f, MaxInstances = 3 }, new Vector2(npc.Center.X, surfaceY));
                }
            }

            if (t >= UnderTime) {
                //破水帧：从冻结沸点跃出
                EnterPhase(3);
                sharkTossed = false;
                npc.Center = new Vector2(breachX, surfaceY + 40f);

                //出水角：朝玩家上方越顶，横向分量随距离
                Vector2 apex = player.Center + new Vector2(Math.Sign(player.Center.X - breachX) * 120f, -300f);
                Vector2 dir = (apex - npc.Center).SafeNormalize(-Vector2.UnitY);
                if (dir.Y > -0.62f) {
                    dir = new Vector2(dir.X, -0.62f).SafeNormalize(-Vector2.UnitY);
                }
                npc.velocity = dir * BreachSpeed;
                spinSign = Math.Sign(npc.velocity.X);
                if (spinSign == 0) {
                    spinSign = 1;
                }
                //翻滚期朝向冻结，旋转全交给滚
                npc.direction = spinSign;
                npc.spriteDirection = -spinSign;
                airBaseRot = npc.velocity.ToRotation() + (npc.spriteDirection == 1 ? MathHelper.Pi : 0f);
                npc.netUpdate = true;

                FishronMotionFX.SpawnSplashBurst(new Vector2(breachX, surfaceY), 2.3f);
                FishronMotionFX.CameraPunch(npc.Center, 8f, 16, "FishronBreach", -Vector2.UnitY);
                SoundEngine.PlaySound(SoundID.Zombie20 with { Volume = 1.05f, Pitch = 0.25f, MaxInstances = 3 }, npc.Center);
            }
            return null;
        }

        /// <summary>相位3：弹道腾空+整周翻滚，顶点甩鲨</summary>
        private IFishronState UpdateAirRoll(FishronStateContext context, NPC npc, Player player, int t) {
            //弹道重力
            npc.velocity.Y += AirGravity;
            npc.velocity.X *= 0.995f;
            context.FrameCommand = 2;

            //翻滚：整数周 smoothstep，出旋时角度回落到航向（慢-快-慢，中段最急）
            float airT = MathHelper.Clamp(t / (float)AirTime, 0f, 1f);
            float ease = airT * airT * (3f - 2f * airT);
            float flightRot = npc.velocity.ToRotation() + (npc.spriteDirection == 1 ? MathHelper.Pi : 0f);
            //基准角随弹道缓转，翻滚叠加其上
            airBaseRot = airBaseRot.AngleTowards(flightRot, 0.05f);
            npc.rotation = airBaseRot + spinSign * RollTurns * MathHelper.TwoPi * ease;

            //顶点甩鲨：升转降的一帧（服务端裁决，帧判据各端一致）；末相数量与出膛速度翻倍
            if (!sharkTossed && npc.velocity.Y >= 0f) {
                sharkTossed = true;
                SoundEngine.PlaySound(SoundID.Zombie9 with { Volume = 0.85f, Pitch = 0.1f, MaxInstances = 3 }, npc.Center);
                if (!VaultUtils.isClient) {
                    bool lastStand = context.Phase >= 3;
                    int count = lastStand ? 6 : 2;
                    float speed = lastStand ? 34f : 17f;
                    //六条收窄扇距，威胁面拓宽但中路仍是主刀
                    float spreadStep = lastStand ? 0.22f : 0.34f;
                    for (int i = 0; i < count; i++) {
                        float spread = (i - (count - 1) * 0.5f) * spreadStep;
                        Vector2 aim = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY).RotatedBy(spread);
                        FishronSharkronStrafeState.TryLaunchSharkron(npc, npc.Center, aim, speed);
                    }
                }
            }

            //再入水或滚尽
            bool splashedBack = npc.Center.Y > surfaceY + 50f && t > 8;
            if (splashedBack || t >= AirTime) {
                loopsDone++;
                if (splashedBack) {
                    FishronMotionFX.SpawnSplashBurst(new Vector2(npc.Center.X, surfaceY), 1.6f);
                }
                if (loopsDone < MaxLoops(context) && splashedBack) {
                    //再潜一轮：直接接水下掠行
                    EnterPhase(2);
                    breachLocked = false;
                    npc.velocity *= 0.5f;
                    npc.netUpdate = true;
                }
                else {
                    EnterPhase(4);
                }
            }
            return null;
        }

        /// <summary>相位4：贴浪拉起收势，交还悬停</summary>
        private IFishronState UpdateExit(FishronStateContext context, NPC npc, Player player, int t) {
            context.SkipDefaultMovement = false;
            SetMovement(context, player.Center + new Vector2(Math.Sign(npc.Center.X - player.Center.X) * 360f, -220f), 11f, 0.5f);
            if (t == 1 && !VaultUtils.isServer) {
                FishronMotionFX.SpawnBrakeSpray(npc);
            }
            if (t >= ExitTime) {
                return new FishronHoverState();
            }
            return null;
        }

        public override void OnExit(FishronStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
        }
    }
}
