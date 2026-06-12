using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering;
using CalamityOverhaul.Content.Projectiles.Boss.Destroyer;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States
{
    /// <summary>
    /// 龙车俯冲开场（无出场无敌）：
    /// <br/>幕一 预兆——玩家侧方地面隆隆，尘柱与震动随 t³ 爬升，对角预警线淡入；
    /// <br/>幕二 破土贯入——头部自地下沿预警线一帧全速贯出，体节在此帧生成、
    ///    靠第一趟高速位移自然甩开展开（取代旧 StretchTime 展开期）；首趟轨迹偏移玩家（公平阀）；
    /// <br/>幕三 反向第二趟成X交叉，随后阶梯刹车收势，尖刺展开波从头扫到尾完成亮相 → 巡空。
    /// <br/>全程可被攻击、接触伤害仅在贯穿途中开启。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)DestroyerStateIndex.Intro, typeof(DestroyerStateContext))]
    internal class DestroyerIntroState : DestroyerStateBase
    {
        public override string StateName => "Intro";
        public override DestroyerStateIndex StateIndex => DestroyerStateIndex.Intro;

        #region 节奏常量
        private const int OmenTime = 45;
        private const int Pass1End = 96;
        private const int Tele2Time = 42;
        private const int Pass2Launch = Pass1End + Tele2Time;   //138
        private const int Pass2MaxEnd = Pass2Launch + 60;       //198
        private const int DeployStart = Pass2MaxEnd + 1;        //199
        private const int DeployTime = 32;
        private const int IntroEnd = DeployStart + DeployTime;  //231
        private const float Pass1Speed = 78f;
        private const float Pass2Speed = 74f;
        #endregion

        /// <summary>
        /// 尖刺展开波相位（0=头部→1=尾部，-1=尚未展开，2=全部展开）。
        /// 体节绘制据此决定无刺/带刺贴图：仅当头部处于Intro且波未扫过本节时画无刺
        /// </summary>
        internal static float DeployWavePhase = 2f;

        private Vector2 breachPoint;
        private Vector2 dir1;
        private Vector2 dir2;
        private Vector2 lineCenter2;
        private bool breach1Fired;
        private bool boom2Fired;

        public DestroyerIntroState() {
        }

        private int Side(DestroyerStateContext context) => (int)context.Npc.ai[3] % 2 == 0 ? 1 : -1;

        public override void OnEnter(DestroyerStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            DeployWavePhase = -1f;
            breach1Fired = false;
            boom2Fired = false;

            NPC npc = context.Npc;
            npc.damage = 0;

            //服务端决定首趟方位（经ai[3]同步）；地下待命位的安置在首帧OnUpdate执行（确保Target已就绪）
            if (!VaultUtils.isClient) {
                npc.ai[3] = Main.rand.Next(2);
                npc.netUpdate = true;
            }
        }

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            int side = Side(context);

            Timer++;

            //幕一：地面预兆
            if (Timer <= OmenTime) {
                UpdateOmen(context, side);
                return null;
            }

            //破土贯入帧：一帧设定全速 + 体节此刻生成
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

            //幕二：第一趟贯穿（偏移线，体节在高速位移中展开）
            if (Timer <= Pass1End) {
                UpdatePass1(context);
                return null;
            }

            //第二趟预警期：头部远处缓行，反对角预警线锁定
            if (Timer <= Pass2Launch) {
                UpdateTelegraph2(context, side);
                return null;
            }

            //第二趟释放帧
            if (Timer == Pass2Launch + 1) {
                npc.Center = lineCenter2 - dir2 * 2300f;
                npc.velocity = dir2 * Pass2Speed;
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                npc.netUpdate = true;
                if (!VaultUtils.isClient) {
                    DestroyerHeatWakeProj.EnsureForHead(npc);
                }
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.35f, Volume = 1.1f }, player.Center);
            }

            //幕三：第二趟贯穿（正常瞄准，X交叉）
            if (Timer <= Pass2MaxEnd) {
                UpdatePass2(context);
                return null;
            }

            //刹车 + 尖刺展开波亮相
            if (Timer < IntroEnd) {
                UpdateDeploy(context, side);
                return null;
            }

            DeployWavePhase = 2f;
            return new DestroyerPatrolState();
        }

        #region 幕一：预兆

        private void UpdateOmen(DestroyerStateContext context, int side) {
            NPC npc = context.Npc;
            Player player = context.Target;

            npc.velocity = Vector2.Zero;
            npc.damage = 0;

            if (Timer == 1) {
                //头部移到地下待命位（远离玩家武器射程，预兆期间不可见）
                npc.Center = player.Center + new Vector2(side * 420f, 1700f);
                npc.netUpdate = true;
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.7f, Pitch = -0.65f }, player.Center);
            }

            //确定第一趟贯穿线：破土点偏移玩家约260px（公平阀——开场不打脸），自地下对角向上
            if (Timer == 2) {
                breachPoint = DestroyerMotionFX.FindGroundBelow(player.Center + new Vector2(side * 260f, 0f));
                dir1 = (-Vector2.UnitY).RotatedBy(side * 0.62f);
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), breachPoint - dir1 * 2400f, dir1,
                        ModContent.ProjectileType<DestroyerStrikeTelegraph>(), 0, 0f, Main.myPlayer,
                        -1, -1, DestroyerStrikeTelegraph.PackParams(0, OmenTime - 2));
                }
            }

            //地面隆隆：尘柱密度/震动强度随 t³ 爬升（"slow start, hard end"）
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
                        Pitch = -0.6f + ramp * 0.45f
                    }, breachPoint);
                }
                Lighting.AddLight(breachPoint, DestroyerMotionFX.HotOrange.ToVector3() * ramp * 0.8f);
            }
        }

        #endregion

        #region 幕二/幕三：双趟贯穿

        private void UpdatePass1(DestroyerStateContext context) {
            NPC npc = context.Npc;

            npc.damage = npc.defDamage;
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
            context.OrbitalVisual = 2;
            context.JawCommand = 1;

            //破土瞬间：冲击环 + 碎屑喷泉 + 沿线定向震屏
            if (!breach1Fired && Vector2.Dot(npc.Center - breachPoint, dir1) > 0f) {
                breach1Fired = true;
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), breachPoint, Vector2.Zero,
                        ModContent.ProjectileType<DestroyerShockwave>(), 0, 0f, Main.myPlayer, 1);
                }
                DestroyerMotionFX.SpawnImpactBlast(breachPoint, 1.25f);
                DestroyerMotionFX.CameraPunch(breachPoint, 9f, 18, "DestroyerIntroBreach", dir1);
            }

            //贯穿出屏后提前进入下一拍（no dead waiting）
            if (breach1Fired && Vector2.Dot(npc.Center - breachPoint, dir1) > 2600f && Timer < Pass1End) {
                Timer = Pass1End;
            }
        }

        private void UpdateTelegraph2(DestroyerStateContext context, int side) {
            NPC npc = context.Npc;
            Player player = context.Target;

            npc.damage = 0;
            npc.velocity *= 0.985f;
            context.JawCommand = 1;

            //反对角预警线（与第一趟成X交叉），正常瞄准玩家
            if (Timer == Pass1End + 1) {
                dir2 = Vector2.UnitY.RotatedBy(-side * 0.58f);
                lineCenter2 = player.Center + player.velocity * 18f;
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), lineCenter2 - dir2 * 2400f, dir2,
                        ModContent.ProjectileType<DestroyerStrikeTelegraph>(), 0, 0f, Main.myPlayer,
                        -1, -1, DestroyerStrikeTelegraph.PackParams(0, Tele2Time));
                }
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.2f, Volume = 0.85f }, player.Center);
            }
        }

        private void UpdatePass2(DestroyerStateContext context) {
            NPC npc = context.Npc;

            npc.damage = npc.defDamage;
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
            context.OrbitalVisual = 2;
            context.JawCommand = 1;

            //贴近战场中心引爆音爆
            if (!boom2Fired && npc.Distance(lineCenter2) < 340f) {
                boom2Fired = true;
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), lineCenter2, Vector2.Zero,
                        ModContent.ProjectileType<DestroyerShockwave>(), 0, 0f, Main.myPlayer, 1);
                }
                DestroyerMotionFX.CameraPunch(lineCenter2, 7f, 16, "DestroyerIntroPass2", dir2);
            }

            //越过战场足够远立即收势（no dead waiting）
            if (boom2Fired && Vector2.Dot(npc.Center - lineCenter2, dir2) > 1200f && Timer < Pass2MaxEnd) {
                Timer = Pass2MaxEnd;
            }
        }

        #endregion

        #region 亮相：阶梯刹车 + 尖刺展开波

        private void UpdateDeploy(DestroyerStateContext context, int side) {
            NPC npc = context.Npc;

            npc.damage = 0;
            context.JawCommand = 0;

            //三层阶梯刹车 + 缓弧回卷，重型机体的长弧停驻
            float spd = npc.velocity.Length();
            float brake = spd > 40f ? 0.92f : spd > 25f ? 0.94f : 0.965f;
            npc.velocity = npc.velocity.RotatedBy(side * 0.022f) * brake;
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

            if (Timer == DeployStart) {
                context.RefreshBodySegments();
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.35f, Volume = 0.8f }, npc.Center);
            }

            //尖刺展开波：从头部扫向尾部，扫过的体节切换为带刺贴图
            float phase = MathHelper.Clamp((Timer - DeployStart) / (float)DeployTime, 0f, 1f);
            DeployWavePhase = phase * 1.15f;
            DestroyerChargeWave.Push(npc.whoAmI, MathHelper.Clamp(DeployWavePhase, 0f, 1f), 0.1f, 1f);

            //逐节金属咔哒 + 火花（客户端，跟随波峰所在体节）
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
            DeployWavePhase = 2f;
            context.Npc.damage = context.Npc.defDamage;
        }
    }
}
