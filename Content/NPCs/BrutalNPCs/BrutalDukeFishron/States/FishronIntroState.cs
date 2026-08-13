using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.States
{
    /// <summary>开场：海面钓出的爆发。涌动→死寂→破水→悬停宣战</summary>
    [InnoVault.StateMachines.VaultState((int)FishronStateIndex.Intro, typeof(FishronStateContext))]
    internal class FishronIntroState : FishronStateBase
    {
        public override string StateName => "Intro";
        public override FishronStateIndex StateIndex => FishronStateIndex.Intro;
        public override bool AllowFarSnap => false;

        #region 节奏常量
        private const int ChurnEnd = 70;      //幕一 海面涌动
        private const int SilenceEnd = 84;    //幕二 死寂
        private const int BreachFrame = 85;   //破水帧
        private const int RiseEnd = 150;      //幕三 升空急停
        private const int RoarEnd = 205;      //幕四 咆哮宣战
        private const float BreachSpeed = 36f;
        #endregion

        private Vector2 surfacePoint;
        private bool surfaceIsWater;
        private bool surfaceResolved;

        public FishronIntroState() {
        }

        public override void OnEnter(FishronStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            surfaceResolved = false;
            NPC npc = context.Npc;
            npc.alpha = 255;
            npc.damage = 0;
            npc.dontTakeDamage = true;
        }

        public override IFishronState OnUpdate(FishronStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;

            //风暴随开场从晴到阴
            float introRamp = MathHelper.Clamp(Timer / (float)RoarEnd, 0f, 1f);
            context.StormBoost = -context.PhaseStormGrade * (1f - introRamp);

            //首帧解析海面点并把本体压到水下待命
            if (!surfaceResolved) {
                surfaceResolved = true;
                surfacePoint = FishronMotionFX.FindSurfaceBelow(npc.Center - new Vector2(0, 120f), out surfaceIsWater);
                npc.Center = surfacePoint + new Vector2(0, 640f);
                npc.velocity = Vector2.Zero;
                npc.netUpdate = true;
            }

            //幕一：海面涌动，t³ 爬升
            if (Timer <= ChurnEnd) {
                npc.damage = 0;
                npc.dontTakeDamage = true;
                npc.velocity = Vector2.Zero;
                //主控每帧衰减 alpha，水下待命须持续压满
                npc.alpha = 255;

                float t = Timer / (float)ChurnEnd;
                float ramp = t * t * t;
                if (!VaultUtils.isServer) {
                    //搅动的泡沫与上涌水珠
                    if (Timer % 4 == 0) {
                        FishronMotionFX.SpawnSprayCone(
                            surfacePoint + new Vector2(Main.rand.NextFloat(-90f, 90f), 0f),
                            -Vector2.UnitY, 1 + (int)(ramp * 3f), 1.5f, 3.5f + ramp * 4f, 0.4f, 0.8f);
                    }
                    if (Timer % 9 == 0) {
                        InnoVault.PRT.PRTLoader.NewParticle<PRT_FishronFoam>(
                            surfacePoint + new Vector2(Main.rand.NextFloat(-110f, 110f), -6f),
                            -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.1f),
                            FishronMotionFX.FoamWhite * (0.35f + ramp * 0.4f),
                            Main.rand.NextFloat(0.8f, 1.3f))?.Configure(Main.rand.Next(30, 50), Main.rand.NextFloat(-0.02f, 0.02f));
                    }
                    if (Timer % 14 == 0) {
                        FishronMotionFX.CameraPunch(surfacePoint, 1f + ramp * 2.5f, 12, "FishronIntroRumble");
                        SoundEngine.PlaySound(SoundID.Drown with {
                            Volume = 0.3f + ramp * 0.5f,
                            Pitch = -0.7f + ramp * 0.3f,
                            MaxInstances = 3
                        }, surfacePoint);
                    }
                    Lighting.AddLight(surfacePoint, FishronMotionFX.SeaGreen.ToVector3() * ramp * 0.7f);
                }
                return null;
            }

            //幕二：死寂——粒子全停，只剩一声线断
            if (Timer <= SilenceEnd) {
                npc.damage = 0;
                npc.dontTakeDamage = true;
                npc.alpha = 255;
                if (Timer == ChurnEnd + 2 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.6f, Pitch = 0.4f }, surfacePoint);
                }
                return null;
            }

            //破水帧：从海面炸出
            if (Timer == BreachFrame) {
                Vector2 tilt = player.Alives()
                    ? new Vector2(Math.Sign(player.Center.X - surfacePoint.X) * 0.22f, -1f)
                    : -Vector2.UnitY;
                npc.Center = surfacePoint + new Vector2(0, 40f);
                npc.velocity = tilt.SafeNormalize(-Vector2.UnitY) * BreachSpeed;
                npc.alpha = 0;
                npc.netUpdate = true;

                FishronMotionFX.SpawnSplashBurst(surfacePoint, surfaceIsWater ? 2.1f : 1.5f);
                FishronMotionFX.CameraPunch(surfacePoint, 9f, 18, "FishronIntroBreach", -Vector2.UnitY);
                SoundEngine.PlaySound(SoundID.Zombie20 with { Volume = 1.2f, Pitch = -0.2f }, surfacePoint);
            }

            //幕三：冲天减速，急停悬于海面上方
            if (Timer <= RiseEnd) {
                npc.dontTakeDamage = false;
                npc.damage = 0;
                AimBodyAlongVelocity(npc);
                context.FrameCommand = 2;

                //上冲后半程指数刹车
                if (Timer > BreachFrame + 22) {
                    npc.velocity *= 0.88f;
                    if (player.Alives()) {
                        FaceBody(npc, player.Center, 0.1f);
                    }
                }
                //升空拖水尾
                if (!VaultUtils.isServer && Timer % 2 == 0 && npc.velocity.Length() > 6f) {
                    FishronMotionFX.SpawnSprayCone(npc.Center + Main.rand.NextVector2Circular(30f, 30f),
                        -npc.velocity.SafeNormalize(Vector2.UnitY), 2, 2f, 6f, 0.5f);
                }
                return null;
            }

            //幕四：悬停咆哮，宣战
            if (Timer <= RoarEnd) {
                npc.damage = 0;
                npc.velocity *= 0.9f;
                if (player.Alives()) {
                    FaceBody(npc, player.Center, 0.12f);
                }

                if (Timer == RiseEnd + 14) {
                    SoundEngine.PlaySound(SoundID.Zombie20 with { Volume = 1.1f, Pitch = 0.1f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.75f, Pitch = -0.4f, MaxInstances = 3 }, npc.Center);
                    FishronStormSky.PushFlash(0.55f, npc.Center);
                    if (!VaultUtils.isServer) {
                        InnoVault.PRT.PRTLoader.NewParticle<CalamityOverhaul.Content.PRTTypes.PRT_DWave>(
                            npc.Center, Vector2.Zero, FishronMotionFX.SeaGreen, 0.3f)?
                            .Configure(new Vector2(1f, 1f), 0f, 1.6f, 22);
                    }
                }
                if (Timer > RiseEnd + 10 && Timer < RiseEnd + 40) {
                    context.FrameCommand = 1;
                }
                //鳍尖滴水
                if (!VaultUtils.isServer && Timer % 6 == 0) {
                    FishronMotionFX.SpawnSprayCone(npc.Center + Main.rand.NextVector2Circular(46f, 30f),
                        Vector2.UnitY, 1, 0.5f, 1.5f, 0.2f, 0.7f);
                }
                return null;
            }

            return new FishronHoverState();
        }

        public override void OnExit(FishronStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            context.Npc.dontTakeDamage = false;
            context.Npc.damage = context.Npc.defDamage;
        }
    }
}
