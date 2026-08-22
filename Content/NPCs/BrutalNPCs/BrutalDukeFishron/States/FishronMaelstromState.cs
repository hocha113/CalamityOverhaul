using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.States
{
    /// <summary>
    /// 低血大招·灭世潮漩：聚拢整场风暴→雨声骤停的死寂→雷击己身引爆
    /// 间歇泉行进、海啸双向合拢、预判落雷、压轴巨冲，一整套过山车后力竭喘息
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FishronStateIndex.Maelstrom, typeof(FishronStateContext))]
    internal class FishronMaelstromState : FishronStateBase
    {
        public override string StateName => "Maelstrom";
        public override FishronStateIndex StateIndex => FishronStateIndex.Maelstrom;
        public override bool AllowFarSnap => false;

        #region 节奏常量
        private const int SpinUpEnd = 96;
        private const int SilenceEnd = 116;
        private const int TsunamiWave = 152;
        private const int BoltWaveStart = 205;
        private const int MegaTelegraph = 240;
        private const int MegaDash = 268;
        private const int ExhaleStart = 306;
        private const int TotalTime = 392;
        #endregion

        private bool megaLaunched;
        //压轴巨冲方向，预告锁定帧冻结
        private Vector2 megaDashDir;
        //服务端专用落雷排程（帧, 地面点）
        private readonly System.Collections.Generic.List<(int frame, Vector2 ground)> boltPlan = [];

        public FishronMaelstromState() {
        }

        public override void OnEnter(FishronStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            context.MaelstromUsed = true;
            megaLaunched = false;
            boltPlan.Clear();

            //清掉气泡，把在场龙卷灌成满级，风暴归拢到他身上
            if (!VaultUtils.isClient) {
                foreach (var n in Main.ActiveNPCs) {
                    if (n.type == NPCID.DetonatingBubble) {
                        n.life = 0;
                        n.HitEffect();
                        n.active = false;
                        n.netUpdate = true;
                    }
                }
                int tornadoType = ModContent.ProjectileType<FishronSharkTornadoProj>();
                foreach (var proj in Main.ActiveProjectiles) {
                    if (proj.type == tornadoType) {
                        proj.ai[1] = 1f;
                        //加寿走刷新戳（各端一致落地，含 netUpdate）
                        FishronSharkTornadoProj.RefreshLifetime(proj);
                    }
                }
            }
        }

        public override IFishronState OnUpdate(FishronStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;

            //幕一：升至场心蓄漩，静止的公爵是给玩家的输出窗口
            if (Timer <= SpinUpEnd) {
                npc.damage = 0;
                Vector2 goal = player.Center + new Vector2(0, -420f);
                Vector2 desired = (goal - npc.Center).SafeNormalize(Vector2.Zero)
                    * MathHelper.Lerp(18f, 3f, Timer / (float)SpinUpEnd);
                npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.1f);
                FaceBody(npc, player.Center, 0.1f);

                float progress = Timer / (float)SpinUpEnd;
                context.SetChargeState(3, progress);
                context.FrameCommand = Timer > SpinUpEnd - 26 ? 1 : 0;
                FishronStormSky.PushRainBoost(0.5f * progress);

                //聚拢的水汽（密度∝sqrt，72% 截断）
                if (!VaultUtils.isServer && Math.Sqrt(progress) > Main.rand.NextFloat() && progress < 0.72f) {
                    FishronMotionFX.SpawnChargeGatherFX(npc.Center, progress, 190f);
                }
                if (Timer % 24 == 0) {
                    FishronMotionFX.CameraPunch(npc.Center, 1.5f + progress * 3f, 14, "FishronMaelstromRumble");
                }
                if (Timer == 8) {
                    SoundEngine.PlaySound(SoundID.Zombie20 with { Volume = 1.3f, Pitch = -0.4f }, npc.Center);
                }
                return null;
            }

            //幕二：死寂，雨声骤停，天黑到底
            if (Timer <= SilenceEnd) {
                npc.damage = 0;
                npc.velocity *= 0.9f;
                context.SetChargeState(3, 1f);
                context.StormBoost = 0.2f;
                FishronStormSky.PushRainCut();
                return null;
            }

            //引爆帧：雷击己身
            if ((int)Timer == SilenceEnd + 1) {
                FishronStormSky.PushFlash(1f, npc.Center);
                FishronMotionFX.CameraPunch(npc.Center, 12f, 22, "FishronMaelstromBoom");
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 1.3f, Pitch = 0.1f }, npc.Center);
                FishronMotionFX.SpawnSplashBurst(npc.Center, 2.2f);
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 3; i++) {
                        Vector2 skyFrom = npc.Center + new Vector2((i - 1) * 340f, -1100f);
                        InnoVault.PRT.PRTLoader.NewParticle<CalamityOverhaul.Content.PRTTypes.PRT_SkyBolt>(
                            npc.Center, Vector2.Zero, FishronMotionFX.StormBolt, 1f)?
                            .Configure(skyFrom, npc.Center, 28);
                    }
                }
                //第一波：间歇泉自一侧行进推过（服务端）
                if (!VaultUtils.isClient) {
                    int dir = Main.rand.NextBool() ? 1 : -1;
                    for (int i = 0; i < 8; i++) {
                        float x = player.Center.X + dir * (-640f + i * 180f);
                        Vector2 vent = FishronMotionFX.FindSurfaceBelow(new Vector2(x, player.Center.Y - 40f), out _);
                        Projectile.NewProjectile(npc.GetSource_FromAI(), vent, Vector2.Zero,
                            ModContent.ProjectileType<FishronGeyserProj>(),
                            FishronGeyserProj.GeyserDamage, 0f, Main.myPlayer,
                            26 + i * 13, 470f);
                    }
                }
            }

            //第二波：海啸双向合拢（一常一高，错高留缝）
            if ((int)Timer == TsunamiWave && !VaultUtils.isClient) {
                Projectile.NewProjectile(npc.GetSource_FromAI(),
                    new Vector2(player.Center.X - 1250f, player.Center.Y - 120f),
                    new Vector2(13f, 0f), ModContent.ProjectileType<FishronTsunamiWallProj>(),
                    FishronTsunamiWallProj.WaveDamage, 0f, Main.myPlayer, 1f, 0f);
                Projectile.NewProjectile(npc.GetSource_FromAI(),
                    new Vector2(player.Center.X + 1250f, player.Center.Y - 120f),
                    new Vector2(-13f, 0f), ModContent.ProjectileType<FishronTsunamiWallProj>(),
                    FishronTsunamiWallProj.WaveDamage, 0f, Main.myPlayer, -1f, 1f);
            }

            //第三波：预判落雷四连（服务端排程：先亮 36 帧预告，到点落雷）
            if (!VaultUtils.isClient) {
                if (Timer >= BoltWaveStart && Timer < MegaTelegraph && (int)(Timer - BoltWaveStart) % 16 == 0) {
                    Vector2 ground = FishronMotionFX.FindSurfaceBelow(
                        player.Center + new Vector2(player.velocity.X * 30f, -40f), out _);
                    boltPlan.Add(((int)Timer + FishronLightningRainState.BoltTelegraphTime, ground));
                    Projectile.NewProjectile(npc.GetSource_FromAI(), ground, -Vector2.UnitY,
                        ModContent.ProjectileType<FishronTelegraph>(), 0, 0f, Main.myPlayer,
                        -1, -1, FishronTelegraph.PackParams(1, FishronLightningRainState.BoltTelegraphTime));
                }
                for (int i = boltPlan.Count - 1; i >= 0; i--) {
                    if ((int)Timer >= boltPlan[i].frame) {
                        Vector2 ground = boltPlan[i].ground;
                        Projectile.NewProjectile(npc.GetSource_FromAI(),
                            new Vector2(ground.X, ground.Y - 980f), Vector2.Zero,
                            ModContent.ProjectileType<FishronSkyBoltProj>(),
                            FishronSkyBoltProj.BoltDamage, 0f, Main.myPlayer,
                            ground.Y);
                        boltPlan.RemoveAt(i);
                    }
                }
            }

            //弹幕波期间：镇在风暴眼里缓浮，正对玩家，静止的王座即输出窗口
            if (Timer > SilenceEnd + 1 && Timer < MegaTelegraph) {
                Vector2 eye = player.Center + new Vector2(0, -400f);
                npc.velocity = Vector2.Lerp(npc.velocity, (eye - npc.Center).SafeNormalize(Vector2.Zero) * 4f, 0.05f);
                FaceBody(npc, player.Center, 0.08f);
            }

            //压轴巨冲：预告→贯穿
            if (Timer >= MegaTelegraph && Timer < MegaDash) {
                npc.damage = 0;
                float progress = (Timer - MegaTelegraph) / (float)(MegaDash - MegaTelegraph);
                //预告锁定帧同步冻结方向
                if (Timer < MegaDash - FishronTelegraph.LockTime || megaDashDir == Vector2.Zero) {
                    megaDashDir = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                }
                context.SetChargeState(1, progress);
                context.DashDirection = megaDashDir;
                context.FrameCommand = 1;
                npc.velocity = Vector2.Lerp(npc.velocity, -megaDashDir * (2f + progress * 7f), 0.2f);
                FaceBody(npc, npc.Center + megaDashDir * 100f, 0.28f);
                if ((int)Timer == MegaTelegraph && !VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center,
                        megaDashDir, ModContent.ProjectileType<FishronTelegraph>(), 0, 0f, Main.myPlayer,
                        npc.whoAmI, player.whoAmI, FishronTelegraph.PackParams(0, MegaDash - MegaTelegraph));
                }
                return null;
            }
            if ((int)Timer == MegaDash) {
                megaLaunched = true;
                Vector2 dir = megaDashDir == Vector2.Zero
                    ? (player.Center - npc.Center).SafeNormalize(Vector2.UnitY) : megaDashDir;
                npc.velocity = dir * 62f;
                npc.netUpdate = true;
                FishronMotionFX.SpawnDashBurst(npc.Center, dir, 1.4f);
                SoundEngine.PlaySound(SoundID.Zombie20 with { Volume = 1.2f, Pitch = 0.35f }, npc.Center);
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                        ModContent.ProjectileType<FishronTideTrailProj>(),
                        FishronTideTrailProj.TrailDamage, 0f, Main.myPlayer,
                        npc.whoAmI, 30);
                }
            }
            if (megaLaunched && Timer < ExhaleStart) {
                AimBodyAlongVelocity(npc);
                context.FrameCommand = 2;
                npc.velocity *= 0.995f;
                if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                    FishronMotionFX.SpawnSprayCone(npc.Center, -npc.velocity.SafeNormalize(Vector2.UnitY),
                        2, 3f, 9f, 0.5f);
                }
                return null;
            }

            //尾声：力竭喘息，奖励窗口
            if (Timer >= ExhaleStart) {
                npc.damage = 0;
                npc.velocity *= 0.93f;
                FaceBody(npc, player.Center, 0.06f);
                context.StormBoost = -0.15f;
                if ((int)Timer == ExhaleStart + 10) {
                    SoundEngine.PlaySound(SoundID.Zombie20 with { Volume = 0.7f, Pitch = -0.55f }, npc.Center);
                }
            }

            if (Timer >= TotalTime) {
                return new FishronHoverState();
            }
            return null;
        }

        public override void OnExit(FishronStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            context.Npc.damage = context.Npc.defDamage;
        }
    }
}
