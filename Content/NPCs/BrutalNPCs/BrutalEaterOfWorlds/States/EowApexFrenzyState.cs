using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.States
{
    /// <summary>
    /// 低血大招·群猎终章：四分裂全体入地→轮转鼓点式脚下喷发×6→
    /// 地底汇拢死寂→合体巨喷+酸液新星终结
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EowStateIndex.ApexFrenzy, typeof(EowStateContext))]
    internal class EowApexFrenzyState : EowStateBase
    {
        public override string StateName => "ApexFrenzy";
        public override EowStateIndex StateIndex => EowStateIndex.ApexFrenzy;
        public override bool AllowFarSnap => false;

        #region 节奏常量
        private const int RipFrame = 30;
        private const int RipEnd = 42;
        private const int BurrowEnd = 104;
        private const int DrumStart = 104;
        private const int DrumCadence = 40;
        private const int DrumCount = 6;
        private const int DrumEnd = DrumStart + DrumCadence * DrumCount;  //344
        private const int ConvergeEnd = DrumEnd + 52;                     //396
        private const int FinaleFrame = ConvergeEnd + 8;                  //404
        private const int TotalTime = FinaleFrame + 66;                   //470
        private const float EruptSpeed = 47f;
        #endregion

        private float groundY;
        private bool ripFxFired;
        private bool finaleBreachFired;
        private bool convergeOmenPlaced;
        /// <summary>各鼓点已放预兆标记</summary>
        private int drumOmenPlacedIndex = -1;
        /// <summary>各组入土尘爆标记</summary>
        private readonly bool[] burrowFired = new bool[EowSplitLayout.MaxGroups];

        public EowApexFrenzyState() {
        }

        public override void OnEnter(EowStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = false;
            ripFxFired = false;
            finaleBreachFired = false;
            convergeOmenPlaced = false;
            drumOmenPlacedIndex = -1;
            Array.Clear(burrowFired, 0, burrowFired.Length);

            NPC npc = context.Npc;
            groundY = EowMotionFX.FindGroundBelow(context.Target.Alives()
                ? context.Target.Center : npc.Center).Y;

            EowMotionFX.PlayRoar(npc.Center, -0.3f, 1.25f);
            //大招广播(各客户端本地打印)
            if (!VaultUtils.isServer) {
                VaultUtils.Text(EowHeadAI.EowApex_Text.Value, EowMotionFX.AcidGreen);
            }
        }

        public override IEowState OnUpdate(EowStateContext context) {
            NPC npc = context.Npc;

            Tick();
            context.MiasmaLevel = MathHelper.Clamp(Timer / 60f, 0f, 1f);

            //幕一 四分撕裂
            if (Timer <= RipEnd) {
                UpdateRip(context);
                return null;
            }

            //幕二 全体入地
            if (Timer <= BurrowEnd) {
                UpdateBurrow(context);
                return null;
            }

            //幕三 鼓点轮转喷发
            if (Timer <= DrumEnd) {
                UpdateDrumRoll(context);
                return null;
            }

            //幕四 地底汇拢(死寂蓄势)
            if (Timer <= ConvergeEnd) {
                UpdateConverge(context);
                return null;
            }

            //幕五 合体巨喷终结
            if (Timer <= TotalTime) {
                UpdateFinale(context);
                return null;
            }

            return new EowWeaveState();
        }

        #region 幕一 四分撕裂
        private void UpdateRip(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            float squeezeT = MathHelper.Clamp(Timer / (float)RipFrame, 0f, 1f);
            context.Compression = MathHelper.Lerp(1f, 0.58f, squeezeT * squeezeT);
            context.PulseKind = 4;
            context.PulsePhase = 4f;
            SetMovement(context, player.Center + new Vector2(0f, -420f), MathHelper.Lerp(14f, 5f, squeezeT), 1.2f);
            npc.damage = 0;

            if (Timer == RipFrame && !VaultUtils.isClient) {
                context.SplitGroups = 4;
            }

            if (Timer >= RipFrame && !ripFxFired) {
                ripFxFired = true;
                EowMotionFX.PlayRoar(npc.Center, 0.1f, 1.2f);
                EowMotionFX.CameraPunch(npc.Center, 7f, 16, "EowApexRip");
                FireBoundaryRipFX(context, 4);
            }

            if (Timer > RipFrame) {
                context.SplitProgress = MathHelper.Clamp((Timer - RipFrame) / (float)(RipEnd - RipFrame), 0f, 1f);
            }
        }

        private void FireBoundaryRipFX(EowStateContext context, int groups) {
            if (VaultUtils.isServer || context.Segments.Count == 0) {
                return;
            }
            int totalSegs = context.Segments.Count;
            for (int g = 1; g < groups; g++) {
                int b = EowSplitLayout.LeaderOrdinal(totalSegs, groups, g);
                if (b <= 0 || b >= totalSegs) {
                    continue;
                }
                NPC leader = context.Segments[b];
                if (leader.Alives()) {
                    EowMotionFX.SpawnRipBurst(leader.Center, leader.rotation.ToRotationVector2(), 1.5f);
                }
            }
        }
        #endregion

        #region 幕二 全体入地
        /// <summary>各组入地散点X偏移</summary>
        private static readonly float[] BurrowOffsets = [-700f, 700f, -260f, 260f];

        private void UpdateBurrow(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            npc.damage = 0;
            groundY = EowMotionFX.FindGroundBelow(player.Center).Y;
            context.SplitProgress = 1f;

            //头(组0)扎向左外侧
            Vector2 headDive = new Vector2(player.Center.X + BurrowOffsets[0], groundY + 460f);
            SetMovement(context, headDive, 34f, 1.6f);
            context.AccelRate = 0.11f;

            //其余组各自扎点
            for (int g = 1; g < 4; g++) {
                context.GroupTargets[g] = new Vector2(player.Center.X + BurrowOffsets[g], groundY + 460f);
                context.GroupSpeeds[g] = 34f;
                context.GroupTurns[g] = 1.6f;
            }

            //入土尘爆(各组过地表线时一次)
            TryBurrowBurst(context, 0, npc);
            for (int g = 1; g < 4; g++) {
                int lead = LeaderIndexOf(context, g);
                if (lead >= 0) {
                    TryBurrowBurst(context, g, context.Segments[lead]);
                }
            }
        }

        private void TryBurrowBurst(EowStateContext context, int group, NPC worm) {
            if (burrowFired[group] || worm == null || !worm.active) {
                return;
            }
            if (worm.Center.Y > groundY + 20f) {
                burrowFired[group] = true;
                EowMotionFX.SpawnDirtBurst(new Vector2(worm.Center.X, groundY), 1.15f);
            }
        }

        private int LeaderIndexOf(EowStateContext context, int group) {
            int totalSegs = context.TotalSegments;
            if (totalSegs <= 0 || context.SplitGroups <= 1) {
                return -1;
            }
            int lead = EowSplitLayout.LeaderOrdinal(totalSegs, context.SplitGroups, group);
            if (lead < 0 || lead >= context.Segments.Count || !context.Segments[lead].Alives()) {
                return -1;
            }
            return lead;
        }
        #endregion

        #region 幕三 鼓点轮转喷发
        /// <summary>鼓点出场组序(头压轴不上，留给终章)</summary>
        private static readonly int[] DrumOrder = [1, 2, 3, 1, 3, 2];

        private void UpdateDrumRoll(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            npc.damage = 0;
            groundY = EowMotionFX.FindGroundBelow(player.Center).Y;

            //头保持玩家脚下浅层游弋(地表尘迹威压)
            SetMovement(context, new Vector2(player.Center.X, groundY + 300f), 26f, 1.5f);
            context.SlitherStrength = 0.5f;
            if (!VaultUtils.isServer && Timer % 3 == 0) {
                Vector2 surface = new Vector2(npc.Center.X, groundY);
                if (EowMotionFX.OnScreen(surface)) {
                    Dust dust = Dust.NewDustDirect(surface + new Vector2(Main.rand.NextFloat(-30f, 30f), -6f),
                        4, 4, DustID.Dirt, 0, 0, 110, default, Main.rand.NextFloat(1.1f, 1.7f));
                    dust.velocity = new Vector2(0f, -Main.rand.NextFloat(2f, 4f));
                }
            }

            int drumTimer = Timer - DrumStart;
            int drumIndex = drumTimer / DrumCadence;
            int inDrum = drumTimer % DrumCadence;
            if (drumIndex >= DrumCount) {
                return;
            }
            int group = DrumOrder[drumIndex];

            //鼓点前奏：预兆(服务端放置一次)
            if (inDrum == 0 && drumOmenPlacedIndex != drumIndex) {
                drumOmenPlacedIndex = drumIndex;
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(),
                        new Vector2(player.Center.X + player.velocity.X * 10f, groundY), Vector2.Zero,
                        ModContent.ProjectileType<EowBreachOmen>(), 0, 0f, Main.myPlayer, 20f, 0f);
                }
            }

            int lead = LeaderIndexOf(context, group);
            if (lead < 0) {
                return;
            }
            NPC leader = context.Segments[lead];

            //蓄势：地下就位
            if (inDrum < 20) {
                context.GroupTargets[group] = new Vector2(player.Center.X, groundY + 420f);
                context.GroupSpeeds[group] = 40f;
                context.GroupTurns[group] = 1.9f;
                return;
            }

            //喷发帧
            if (inDrum == 20) {
                if (!VaultUtils.isClient) {
                    leader.Center = new Vector2(player.Center.X + player.velocity.X * 8f, groundY + 560f);
                    leader.netUpdate = true;
                }
                context.GroupDirectVelocity[group] = -Vector2.UnitY * EruptSpeed;
                EowMotionFX.SpawnBreachBlast(new Vector2(leader.Center.X, groundY), 1.35f, -Vector2.UnitY);
                EowMotionFX.CameraPunch(new Vector2(leader.Center.X, groundY), 6f, 12, "EowDrum", -Vector2.UnitY);
                //酸液小扇(服务端)
                if (!VaultUtils.isClient) {
                    for (int i = 0; i < 3; i++) {
                        float spread = MathHelper.Lerp(-0.4f, 0.4f, i / 2f);
                        Vector2 vel = (-Vector2.UnitY).RotatedBy(spread) * Main.rand.NextFloat(8f, 10.5f);
                        Projectile.NewProjectile(npc.GetSource_FromAI(),
                            new Vector2(leader.Center.X, groundY - 8f), vel,
                            ModContent.ProjectileType<EowAcidGlob>(),
                            EowSpitBarrageState.SpitDamage(npc), 0f, Main.myPlayer, 2f);
                    }
                }
                return;
            }

            //弧线回落再入地
            if (inDrum > 20) {
                Vector2 vel = leader.velocity + new Vector2(0f, 1.7f);
                if (vel.Length() > EruptSpeed) {
                    vel = vel.SafeNormalize(Vector2.UnitY) * EruptSpeed;
                }
                context.GroupDirectVelocity[group] = vel;
            }
        }
        #endregion

        #region 幕四 地底汇拢死寂
        private void UpdateConverge(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            npc.damage = 0;
            groundY = EowMotionFX.FindGroundBelow(player.Center).Y;

            //全员向玩家脚下深处汇拢+回链
            context.MergeHoming = true;
            SetMovement(context, new Vector2(player.Center.X, groundY + 600f), 32f, 1.7f);
            context.SplitProgress = MathHelper.Clamp(1f - (Timer - DrumEnd) / 34f, 0f, 1f);

            //巨型预兆盘+死寂(声画收干，只留越来越大的预兆)
            if (!convergeOmenPlaced && !VaultUtils.isClient) {
                convergeOmenPlaced = true;
                Projectile.NewProjectile(npc.GetSource_FromAI(),
                    new Vector2(player.Center.X, groundY), Vector2.Zero,
                    ModContent.ProjectileType<EowBreachOmen>(), 0, 0f, Main.myPlayer,
                    ConvergeEnd - DrumEnd + 6, 1f);
            }

            //回链完成即合体
            bool docked = npc.TryGetOverride<EowHeadAI>(out var headOverride) && headOverride.AllLeadersDocked();
            if ((docked && Timer > DrumEnd + 26) || Timer >= ConvergeEnd - 2) {
                if (!VaultUtils.isClient) {
                    context.SplitGroups = 0;
                }
            }
        }
        #endregion

        #region 幕五 合体巨喷终结
        private void UpdateFinale(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            context.SkipDefaultMovement = true;

            //终喷帧
            if (Timer == FinaleFrame) {
                npc.Center = new Vector2(player.Center.X, groundY + 760f);
                npc.velocity = -Vector2.UnitY * (EruptSpeed + 19f);
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                npc.netUpdate = true;
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = -0.25f, Volume = 1.3f }, player.Center);
            }

            npc.damage = npc.velocity.Length() > 18f ? npc.defDamage : 0;

            //破土终爆：巨尘爆+全向酸液新星
            if (!finaleBreachFired && Timer > FinaleFrame && npc.Center.Y < groundY) {
                finaleBreachFired = true;
                Vector2 breachPoint = new Vector2(npc.Center.X, groundY);
                EowMotionFX.SpawnBreachBlast(breachPoint, 2.4f, -Vector2.UnitY);
                EowMotionFX.CameraPunch(breachPoint, 12f, 26, "EowApexFinale", -Vector2.UnitY);
                EowMotionFX.PlayRoar(npc.Center, 0.4f, 1.3f);
                if (!VaultUtils.isClient) {
                    const int nova = 10;
                    for (int i = 0; i < nova; i++) {
                        float spread = MathHelper.Lerp(-1.25f, 1.25f, i / (float)(nova - 1));
                        Vector2 vel = (-Vector2.UnitY).RotatedBy(spread) * Main.rand.NextFloat(8.5f, 13f);
                        Projectile.NewProjectile(npc.GetSource_FromAI(), breachPoint - new Vector2(0, 12f), vel,
                            ModContent.ProjectileType<EowAcidGlob>(),
                            (int)(EowSpitBarrageState.SpitDamage(npc) * 1.1f), 0f, Main.myPlayer, 2f);
                    }
                }
            }

            //冲天后拱弧回落
            if (finaleBreachFired && Timer > FinaleFrame + 22) {
                npc.velocity.Y += 1.6f;
                npc.velocity.X += Math.Sign(npc.velocity.X == 0 ? 1f : npc.velocity.X) * 0.24f;
                float cap = EruptSpeed + 19f;
                if (npc.velocity.Length() > cap) {
                    npc.velocity = npc.velocity.SafeNormalize(Vector2.UnitY) * cap;
                }
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
            }
        }
        #endregion

        public override void OnExit(EowStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            context.AccelRate = 0.07f;
            context.Npc.damage = context.Npc.defDamage;
            if (!VaultUtils.isClient) {
                context.SplitGroups = 0;
            }
            context.SplitProgress = 0f;
            context.MergeHoming = false;
        }
    }
}
