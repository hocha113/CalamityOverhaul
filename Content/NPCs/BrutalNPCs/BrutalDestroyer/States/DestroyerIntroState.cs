using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States
{
    /// <summary>龙车开场，预兆→破土→交叉俯冲→尖刺亮相</summary>
    [InnoVault.StateMachines.VaultState((int)DestroyerStateIndex.Intro, typeof(DestroyerStateContext))]
    internal class DestroyerIntroState : DestroyerStateBase
    {
        public override string StateName => "Intro";
        public override DestroyerStateIndex StateIndex => DestroyerStateIndex.Intro;
        /// <summary>开场自带地下/高空走位，回归瞬移阀不介入</summary>
        public override bool AllowFarSnap => false;

        #region 节奏常量
        private const int OmenTime = 45;
        private const int Pass1End = 96;
        private const int TelegraphTime = 42;
        private const int DiveTime = 50;
        private const int AimedPassLength = TelegraphTime + DiveTime;   //92
        private const int AimedPassCount = 2;
        private const int AimedEnd = Pass1End + AimedPassCount * AimedPassLength;   //280
        private const int DeployTime = 34;
        private const int IntroEnd = AimedEnd + DeployTime;            //314
        private const float Pass1Speed = 78f;
        private const float AimedSpeed = 74f;
        #endregion

        /// <summary>尖刺波相位0头→1尾，-1未展2全展</summary>
        internal static float DeployWavePhase = 2f;

        private Vector2 breachPoint;
        private Vector2 dir1;
        private Vector2 aimedDir;
        private Vector2 aimedLineCenter;
        private bool breach1Fired;
        private bool aimedBoomFired;
        private int currentAimedPass = -1;

        public DestroyerIntroState() {
        }

        private int Side(DestroyerStateContext context) => (int)context.Npc.ai[3] % 2 == 0 ? 1 : -1;

        public override void OnEnter(DestroyerStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            DeployWavePhase = -1f;
            breach1Fired = false;
            currentAimedPass = -1;

            context.Npc.damage = 0;

            //服务端首趟方位ai[3]
            if (!VaultUtils.isClient) {
                context.Npc.ai[3] = Main.rand.Next(2);
                context.Npc.netUpdate = true;
            }
        }

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            int side = Side(context);

            Timer++;

            //幕一 地面预兆
            if (Timer <= OmenTime) {
                UpdateOmen(context, side);
                return null;
            }

            //破土贯入帧，生节
            if (Timer == OmenTime + 1) {
                npc.Center = breachPoint - dir1 * 2200f;
                npc.velocity = dir1 * Pass1Speed;
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                npc.netUpdate = true;
                if (!VaultUtils.isClient) {
                    DestroyerHeadAI.SpawnBodySegments(npc);
                    DestroyerHeatWakeProj.EnsureForHead(npc);
                }
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.1f, Volume = 1.25f }, player.Center);
            }

            //幕二 偏移首趟
            if (Timer <= Pass1End) {
                UpdatePass1(context);
                return null;
            }

            //幕三 交叉俯冲
            if (Timer <= AimedEnd) {
                UpdateAimedPass(context, Timer - Pass1End - 1, side);
                return null;
            }

            //亮相尖刺展开
            if (Timer < IntroEnd) {
                UpdateDeploy(context, side);
                return null;
            }

            DeployWavePhase = 2f;
            return new DestroyerPatrolState();
        }

        #region 幕一 预兆

        private void UpdateOmen(DestroyerStateContext context, int side) {
            NPC npc = context.Npc;
            Player player = context.Target;

            npc.velocity = Vector2.Zero;
            npc.damage = 0;

            if (Timer == 1) {
                //头移地下待命
                npc.Center = player.Center + new Vector2(side * 420f, 1700f);
                npc.netUpdate = true;
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.7f, Pitch = -0.65f }, player.Center);
            }

            //首趟偏玩家~260px对角向上
            if (Timer == 2) {
                breachPoint = DestroyerMotionFX.FindGroundBelow(player.Center + new Vector2(side * 260f, 0f));
                dir1 = (-Vector2.UnitY).RotatedBy(side * 0.62f);
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), breachPoint - dir1 * 2400f, dir1,
                        ModContent.ProjectileType<DestroyerStrikeTelegraph>(), 0, 0f, Main.myPlayer,
                        -1, -1, DestroyerStrikeTelegraph.PackParams(0, OmenTime - 2));
                }
            }

            //地面隆隆，t³爬升
            float t = Timer / (float)OmenTime;
            float ramp = t * t * t;
            if (!VaultUtils.isServer) {
                int dustCount = 1 + (int)(ramp * 5f);
                for (int i = 0; i < dustCount; i++) {
                    Dust dust = Dust.NewDustDirect(breachPoint + new Vector2(Main.rand.NextFloat(-70f, 70f), -8f),
                        4, 4, DustID.Dirt, 0, 0, 120, default, Main.rand.NextFloat(1.2f, 2.2f));
                    dust.noGravity = false;
                    dust.velocity = new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(2f, 5f + ramp * 5f));
                }
                if (Timer % 12 == 0) {
                    DestroyerMotionFX.CameraPunch(breachPoint, 1f + ramp * 3f, 14, "DestroyerIntroRumble");
                }
                if (Timer % 14 == 0) {
                    SoundEngine.PlaySound(SoundID.WormDig with {
                        Volume = 0.45f + ramp * 0.55f,
                        Pitch = -0.6f + ramp * 0.45f,
                        MaxInstances = 3
                    }, breachPoint);
                }
                Lighting.AddLight(breachPoint, DestroyerMotionFX.HotOrange.ToVector3() * ramp * 0.8f);
            }
        }

        #endregion

        #region 幕二 破土第一趟

        private void UpdatePass1(DestroyerStateContext context) {
            NPC npc = context.Npc;

            npc.damage = npc.defDamage;
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
            context.OrbitalVisual = 2;
            context.JawCommand = 1;

            //破土冲击+碎屑+震屏
            if (!breach1Fired && Vector2.Dot(npc.Center - breachPoint, dir1) > 0f) {
                breach1Fired = true;
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), breachPoint, Vector2.Zero,
                        ModContent.ProjectileType<DestroyerShockwave>(), 0, 0f, Main.myPlayer, 1);
                }
                DestroyerMotionFX.SpawnImpactBlast(breachPoint, 1.25f);
                DestroyerMotionFX.CameraPunch(breachPoint, 9f, 18, "DestroyerIntroBreach", dir1);
            }

            //出屏提前下一拍
            if (breach1Fired && Vector2.Dot(npc.Center - breachPoint, dir1) > 2600f && Timer < Pass1End) {
                Timer = Pass1End;
            }
        }

        #endregion

        #region 幕三 交叉瞄准俯冲

        private void UpdateAimedPass(DestroyerStateContext context, int aimedTimer, int side) {
            NPC npc = context.Npc;
            Player player = context.Target;

            int passIndex = Math.Min(aimedTimer / AimedPassLength, AimedPassCount - 1);
            int t = aimedTimer - passIndex * AimedPassLength;

            //新趟左右对角成X
            if (passIndex != currentAimedPass) {
                currentAimedPass = passIndex;
                aimedBoomFired = false;

                float angle = (passIndex % 2 == 0 ? -side : side) * (0.58f - passIndex * 0.06f);
                aimedDir = Vector2.UnitY.RotatedBy(angle);
                aimedLineCenter = player.Center + player.velocity * 18f;

                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), aimedLineCenter - aimedDir * 2400f, aimedDir,
                        ModContent.ProjectileType<DestroyerStrikeTelegraph>(), 0, 0f, Main.myPlayer,
                        -1, -1, DestroyerStrikeTelegraph.PackParams(0, TelegraphTime));
                }
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.2f, Volume = 0.85f, MaxInstances = 3 }, player.Center);
            }

            //预警缓行，末12f咬合
            if (t < TelegraphTime) {
                npc.damage = 0;
                npc.velocity *= 0.985f;
                context.JawCommand = t > TelegraphTime - 12 ? 2 : 1;
                DestroyerChargeWave.Push(npc.whoAmI, 1f - t / (float)TelegraphTime, 0.25f, 0.7f);
                return;
            }

            //俯冲释放帧
            if (t == TelegraphTime) {
                npc.Center = aimedLineCenter - aimedDir * 2300f;
                npc.velocity = aimedDir * AimedSpeed;
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                npc.netUpdate = true;
                //ForceRoar，防开幕余音吞
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.35f, Volume = 1.1f }, player.Center);
                if (!VaultUtils.isClient) {
                    DestroyerHeatWakeProj.EnsureForHead(npc);
                }
            }

            //俯冲中
            npc.damage = npc.defDamage;
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
            context.OrbitalVisual = 2;
            context.JawCommand = 1;

            if (!aimedBoomFired && npc.Distance(aimedLineCenter) < 340f) {
                aimedBoomFired = true;
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), aimedLineCenter, Vector2.Zero,
                        ModContent.ProjectileType<DestroyerShockwave>(), 0, 0f, Main.myPlayer, 1);
                }
                DestroyerMotionFX.CameraPunch(aimedLineCenter, 7f, 16, "DestroyerIntroPass", aimedDir);
            }

            //越场够远下一趟
            int passEndTimer = Pass1End + 1 + passIndex * AimedPassLength + AimedPassLength - 1;
            if (aimedBoomFired && Vector2.Dot(npc.Center - aimedLineCenter, aimedDir) > 1200f && Timer < passEndTimer) {
                Timer = passEndTimer;
            }
        }

        #endregion

        #region 亮相 回场+尖刺波

        private void UpdateDeploy(DestroyerStateContext context, int side) {
            NPC npc = context.Npc;
            Player player = context.Target;

            npc.damage = 0;
            context.JawCommand = 0;

            //前10f阶梯刹，回身侧亮相
            if (Timer <= AimedEnd + 10) {
                float spd = npc.velocity.Length();
                float brake = spd > 40f ? 0.92f : spd > 25f ? 0.94f : 0.965f;
                npc.velocity = npc.velocity.RotatedBy(side * 0.022f) * brake;
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
            }
            else {
                context.SkipDefaultMovement = false;
                context.SlitherStrength = 0.6f;
                SetMovement(context, player.Center + new Vector2(side * 480f, -360f), 36f, 1.1f);
                context.AccelRate = 0.09f;
            }

            if (Timer == AimedEnd + 1) {
                context.RefreshBodySegments();
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.35f, Volume = 0.8f, MaxInstances = 3 }, npc.Center);
            }

            //尖刺波头→尾换贴图
            float phase = MathHelper.Clamp((Timer - AimedEnd) / (float)DeployTime, 0f, 1f);
            DeployWavePhase = phase * 1.15f;
            DestroyerChargeWave.Push(npc.whoAmI, MathHelper.Clamp(DeployWavePhase, 0f, 1f), 0.1f, 1f);

            //逐节咔哒+火花(客户端)
            if (!VaultUtils.isServer && Timer % 3 == 0 && context.BodySegments.Count > 0) {
                int idx = (int)(MathHelper.Clamp(DeployWavePhase, 0f, 1f) * (context.BodySegments.Count - 1));
                NPC segment = context.BodySegments[idx];
                if (segment.Alives() && DestroyerMotionFX.OnScreen(segment.Center)) {
                    SoundEngine.PlaySound(SoundID.Unlock with {
                        Volume = 0.55f,
                        Pitch = -0.1f + phase * 0.45f,
                        MaxInstances = 6
                    }, segment.Center);
                    DestroyerMotionFX.SpawnSegmentSpeedSparks(segment, 0.8f);
                }
            }
        }

        #endregion

        public override void OnExit(DestroyerStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            context.OrbitalVisual = 0;
            context.AccelRate = 0.055f;
            DeployWavePhase = 2f;
            context.Npc.damage = context.Npc.defDamage;
        }
    }
}
