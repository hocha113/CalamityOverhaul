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
    /// <summary>钻地伏击：俯角入土→潜行(地表尘迹)→40帧预警→垂直破土→拱弧再入地</summary>
    /// <para>普通2次喷发、激怒3次；喷发点预警开始时锁定</para>
    [InnoVault.StateMachines.VaultState((int)DestroyerStateIndex.BurrowAmbush, typeof(DestroyerStateContext))]
    internal class DestroyerBurrowAmbushState : DestroyerStateBase
    {
        public override string StateName => "BurrowAmbush";
        public override DestroyerStateIndex StateIndex => DestroyerStateIndex.BurrowAmbush;
        /// <summary>钻地伏击自带地下走位，回归瞬移阀不介入</summary>
        public override bool AllowFarSnap => false;

        #region 节奏常量
        private const int WarnTime = 40;
        private const int DiveInMaxTime = 90;
        private const int StalkMaxTime = 110;
        private const int EruptMaxTime = 120;
        private const int ExitTime = 70;
        #endregion

        private enum Phase
        {
            DiveIn = 0,
            Stalk = 1,
            Warn = 2,
            Erupt = 3,
            ExitArc = 4,
        }

        private int EruptionCount(DestroyerStateContext ctx) => ctx.IsEnraged ? 3 : 2;
        private float EruptSpeed(DestroyerStateContext ctx) => 70f + (ctx.IsDeathMode ? 6f : 0f);
        private float StalkSpeed(DestroyerStateContext ctx) => 42f + (ctx.IsDeathMode ? 5f : 0f);

        private Phase phase;
        private int eruptionsDone;
        private float groundY;
        private float warnX;
        private bool entryFired;
        private bool breachFired;
        private bool reentryFired;
        private bool exitBurstFired;

        public DestroyerBurrowAmbushState() {
        }

        public override void OnEnter(DestroyerStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = false;
            phase = Phase.DiveIn;
            eruptionsDone = 0;
            entryFired = false;
            exitBurstFired = false;

            //客户端中途加入恢复状态时 Target 可能尚未赋值，回退用头部位置取地表
            Vector2 anchor = context.Target.Alives() ? context.Target.Center : context.Npc.Center;
            groundY = DestroyerMotionFX.FindGroundBelow(anchor).Y;
            SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.45f, Volume = 1f }, context.Npc.Center);
        }

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            Timer++;
            switch (phase) {
                case Phase.DiveIn:
                    UpdateDiveIn(context);
                    break;
                case Phase.Stalk:
                    UpdateStalk(context);
                    break;
                case Phase.Warn:
                    UpdateWarn(context);
                    break;
                case Phase.Erupt:
                    UpdateErupt(context);
                    break;
                case Phase.ExitArc:
                    if (UpdateExitArc(context)) {
                        return new DestroyerPatrolState();
                    }
                    break;
            }
            return null;
        }

        private void SwitchPhase(Phase next) {
            phase = next;
            Timer = 0;
        }

        #region 入土

        private void UpdateDiveIn(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            context.SkipDefaultMovement = false;
            context.OrbitalVisual = 1;
            npc.damage = 0;

            //俯角扎向玩家侧下方的地底
            int side = Math.Sign(npc.Center.X - player.Center.X);
            if (side == 0) {
                side = 1;
            }
            SetMovement(context, new Vector2(player.Center.X + side * 140f, groundY + 1100f),
                MathHelper.Lerp(34f, 52f, Math.Min(Timer / 40f, 1f)), 1.0f);
            context.AccelRate = 0.085f;

            //入土瞬间：尘爆 + 钻地声
            if (!entryFired && npc.Center.Y > groundY + 40f) {
                entryFired = true;
                SpawnGroundBurst(npc.Center.X, 1f);
                DestroyerMotionFX.CameraPunch(new Vector2(npc.Center.X, groundY), 5f, 12, "DestroyerBurrowIn", Vector2.UnitY);
                SoundEngine.PlaySound(SoundID.WormDig with { Volume = 1f, Pitch = -0.3f }, npc.Center);
            }

            //完全入土或超时 → 潜行
            if (npc.Center.Y > groundY + 650f || Timer > DiveInMaxTime) {
                SwitchPhase(Phase.Stalk);
            }
        }

        #endregion

        #region 地下潜行

        private void UpdateStalk(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            context.SkipDefaultMovement = false;
            context.OrbitalVisual = 1;
            context.SlitherStrength = 0.5f;
            npc.damage = 0;

            SetMovement(context, new Vector2(player.Center.X, groundY + 400f), StalkSpeed(context), 1.2f);

            //地表尘迹：沿头部X喷土+低鸣(可读性阀，可追踪地下轨迹)
            if (!VaultUtils.isServer) {
                Vector2 surface = DestroyerMotionFX.FindGroundBelow(new Vector2(npc.Center.X, groundY - 600f));
                if (Timer % 2 == 0 && DestroyerMotionFX.OnScreen(surface)) {
                    Dust dust = Dust.NewDustDirect(surface + new Vector2(Main.rand.NextFloat(-40f, 40f), -6f),
                        4, 4, DustID.Dirt, 0, 0, 110, default, Main.rand.NextFloat(1.1f, 1.9f));
                    dust.velocity = new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(2f, 5f));
                }
                if (Timer % 14 == 0) {
                    SoundEngine.PlaySound(SoundID.WormDigQuiet with { Volume = 0.65f, Pitch = -0.35f }, surface);
                }
                if (Timer % 18 == 0) {
                    DestroyerMotionFX.CameraPunch(surface, 1.6f, 12, "DestroyerStalkRumble");
                }
            }

            //就位（与玩家水平对齐）或超时 → 喷发预警
            if ((Timer > 24 && Math.Abs(npc.Center.X - player.Center.X) < 100f) || Timer > StalkMaxTime) {
                //喷发点在预警开始时锁定（公平阀：40帧反应窗口）
                warnX = player.Center.X + player.velocity.X * 12f;
                groundY = DestroyerMotionFX.FindGroundBelow(new Vector2(warnX, player.Center.Y)).Y;
                breachFired = false;
                reentryFired = false;
                SwitchPhase(Phase.Warn);
            }
        }

        #endregion

        #region 喷发预警

        private void UpdateWarn(DestroyerStateContext context) {
            NPC npc = context.Npc;

            context.SkipDefaultMovement = false;
            context.OrbitalVisual = 1;
            context.JawCommand = Timer > WarnTime - 12 ? 2 : 1;
            npc.damage = 0;

            //地下保持在锁定点正下方蓄势
            SetMovement(context, new Vector2(warnX, groundY + 460f), 30f, 1.4f);

            Vector2 warnPoint = new Vector2(warnX, groundY);
            float t = Timer / (float)WarnTime;
            float ramp = t * t * t;

            if (Timer == 1) {
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.45f, Volume = 1f }, warnPoint);
            }

            //汇聚尘 + t³ 震动爬升 + 地光
            if (!VaultUtils.isServer) {
                int dustCount = 1 + (int)(t * 4f);
                for (int i = 0; i < dustCount; i++) {
                    Vector2 dustPos = warnPoint + new Vector2(Main.rand.NextFloat(-110f, 110f), Main.rand.NextFloat(-12f, 4f));
                    Dust dust = Dust.NewDustDirect(dustPos, 4, 4, DustID.Dirt, 0, 0, 100, default, Main.rand.NextFloat(1.2f, 2f));
                    dust.noGravity = true;
                    dust.velocity = (warnPoint - dustPos).SafeNormalize(Vector2.Zero) * (2f + ramp * 4f)
                        - Vector2.UnitY * Main.rand.NextFloat(1f, 3f);
                }
                if (Timer % 8 == 0) {
                    DestroyerMotionFX.CameraPunch(warnPoint, 1.5f + ramp * 4f, 10, "DestroyerEruptWarn");
                }
                Lighting.AddLight(warnPoint, DestroyerMotionFX.HotOrange.ToVector3() * (0.4f + ramp));
            }
            DestroyerChargeWave.Push(npc.whoAmI, 1f - t, 0.25f, 0.5f + 0.5f * t);

            if (Timer >= WarnTime) {
                SwitchPhase(Phase.Erupt);
            }
        }

        #endregion

        #region 破土直射

        private void UpdateErupt(DestroyerStateContext context) {
            NPC npc = context.Npc;

            context.SkipDefaultMovement = true;
            context.OrbitalVisual = 2;
            context.JawCommand = 1;

            //喷发帧：一帧设定垂直全速
            if (Timer == 1) {
                npc.Center = new Vector2(warnX, groundY + 880f);
                npc.velocity = -Vector2.UnitY * EruptSpeed(context);
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                npc.netUpdate = true;
                //ForceRoar：避免被入土咆哮的余音按IgnoreNew上限吞掉
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.4f, Volume = 1.1f }, new Vector2(warnX, groundY));
                if (!VaultUtils.isClient) {
                    DestroyerHeatWakeProj.EnsureForHead(npc);
                }
            }

            //速度门控接触伤害
            npc.damage = npc.velocity.Length() > 24f ? npc.defDamage : 0;

            //破土瞬间：冲击环 + 碎屑喷泉 + 垂直定向震屏
            if (!breachFired && npc.Center.Y < groundY) {
                breachFired = true;
                Vector2 breachPoint = new Vector2(warnX, groundY);
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), breachPoint, Vector2.Zero,
                        ModContent.ProjectileType<DestroyerShockwave>(), 0, 0f, Main.myPlayer, 1);
                }
                DestroyerMotionFX.SpawnImpactBlast(breachPoint, 1.15f);
                DestroyerMotionFX.CameraPunch(breachPoint, 8f, 16, "DestroyerErupt", -Vector2.UnitY);
            }

            //越顶拱弧：破土后重力式弯落 + 横向漂移回地
            if (breachFired && Timer > 26) {
                npc.velocity.Y += 1.7f;
                npc.velocity.X += Math.Sign(npc.velocity.X == 0 ? 1f : npc.velocity.X) * 0.25f;
                if (npc.velocity.Length() > EruptSpeed(context)) {
                    npc.velocity = npc.velocity.SafeNormalize(Vector2.UnitY) * EruptSpeed(context);
                }
            }
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

            //再入地：小尘爆，决定继续伏击还是收场
            bool reentered = breachFired && npc.velocity.Y > 0f && npc.Center.Y > groundY + 60f;
            if (reentered && !reentryFired) {
                reentryFired = true;
                SpawnGroundBurst(npc.Center.X, 0.7f);
                SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.9f, Pitch = -0.25f }, npc.Center);
            }

            if ((reentryFired && npc.Center.Y > groundY + 500f) || Timer > EruptMaxTime) {
                eruptionsDone++;
                if (eruptionsDone >= EruptionCount(context)) {
                    SwitchPhase(Phase.ExitArc);
                }
                else {
                    SwitchPhase(Phase.Stalk);
                }
            }
        }

        #endregion

        #region 收场

        private bool UpdateExitArc(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            context.SkipDefaultMovement = false;
            context.OrbitalVisual = 0;
            context.SlitherStrength = 0.6f;
            npc.damage = 0;

            int side = Math.Sign(npc.Center.X - player.Center.X);
            if (side == 0) {
                side = 1;
            }
            SetMovement(context, player.Center + new Vector2(side * 520f, -380f), 30f, 1.0f);

            //首次穿出地表的瞬间小尘爆
            if (!exitBurstFired && npc.Center.Y < groundY - 60f) {
                exitBurstFired = true;
                SpawnGroundBurst(npc.Center.X, 0.8f);
                SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.85f, Pitch = -0.1f }, npc.Center);
            }

            return Timer > ExitTime;
        }

        #endregion

        /// <summary>
        /// 地表尘爆（入土/出土通用，客户端粒子）
        /// </summary>
        private void SpawnGroundBurst(float worldX, float power) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 point = new Vector2(worldX, groundY);
            if (!DestroyerMotionFX.OnScreen(point)) {
                return;
            }
            int count = (int)(18 * power);
            for (int i = 0; i < count; i++) {
                Dust dust = Dust.NewDustDirect(point + new Vector2(Main.rand.NextFloat(-60f, 60f), -10f),
                    6, 6, DustID.Dirt, 0, 0, 90, default, Main.rand.NextFloat(1.4f, 2.4f));
                dust.velocity = new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(3f, 9f) * power);
            }
        }

        public override void OnExit(DestroyerStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            context.OrbitalVisual = 0;
            context.AccelRate = 0.055f;
            context.Npc.damage = context.Npc.defDamage;
        }
    }
}
