using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.States
{
    /// <summary>低血大招·水晶圣殿：六芒晶环+辐辏光束网+穹顶陨雨→过载死寂→全体碎裂→力竭奖励窗</summary>
    [InnoVault.StateMachines.VaultState((int)QueenSlimeStateIndex.CrystalCathedral, typeof(QueenSlimeStateContext))]
    internal class QueenCrystalCathedralState : QueenSlimeStateBase
    {
        public override string StateName => "CrystalCathedral";
        public override QueenSlimeStateIndex StateIndex => QueenSlimeStateIndex.CrystalCathedral;

        #region 节奏常量
        private const int NodeCount = 6;
        private const float CathedralRadius = 540f;
        private const int AscendTime = 50;                        //升位+清场
        private const int BuildTime = 96;                         //六柱逐一物化(16f/柱)
        private const int WebStart = AscendTime + BuildTime;      //146
        private const int WebTime = 380;                          //圣殿运转
        private const int GutterStart = WebStart + WebTime;       //526 光束熄灭
        private const int SilenceTime = 22;                       //死寂
        private const int ShatterFrame = GutterStart + SilenceTime;//548 全碎
        private const int WindedTime = 86;                        //力竭奖励窗
        private const int TotalTime = ShatterFrame + WindedTime;  //~634
        #endregion

        private Vector2 cathedralCenter;
        private bool anchored;

        public QueenCrystalCathedralState() {
        }

        public override void OnEnter(QueenSlimeStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            DisableContactDamage(npc);
            npc.noGravity = true;
            npc.noTileCollide = true;
            npc.dontTakeDamage = true;
            anchored = false;

            if (!VaultUtils.isClient) {
                QueenProjHelper.ClearQueenProjectiles();
            }
        }

        public override IQueenSlimeState OnUpdate(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;
            DisableContactDamage(npc);

            if (!anchored) {
                anchored = true;
                cathedralCenter = player.Center + new Vector2(0f, -60f);
                if (!VaultUtils.isServer) {
                    VaultUtils.Text(QueenSlimeAI.QueenSlime_CathedralText.Value, QueenMotion.CrystalBlue);
                    SoundEngine.PlaySound(SoundID.Item4 with { Volume = 1f, Pitch = -0.4f }, player.Center);
                }
            }

            //客户端重建圣殿中心(节点均值)
            if (VaultUtils.isClient && Timer > AscendTime + 20 && Timer % 30 == 0) {
                List<NPC> found = context.CollectPrismNodes();
                if (found.Count >= 3) {
                    Vector2 sum = Vector2.Zero;
                    foreach (var n in found) {
                        sum += n.Center;
                    }
                    cathedralCenter = sum / found.Count;
                }
            }

            //幕一 升位
            if (Timer <= AscendTime) {
                float p = Timer / (float)AscendTime;
                QueenMotion.SpringHover(npc, cathedralCenter, 0.02f, 0.11f, 26f);
                context.PoseCommand = 5;
                context.WingFlapBoost = 1.4f;
                context.SetChargeState(3, p * 0.5f);
                context.PrismShimmer = p;
                return null;
            }

            //幕二 六柱升起(逐一物化，音阶爬升)
            if (Timer <= WebStart) {
                int buildT = Timer - AscendTime;
                QueenMotion.SpringHover(npc, cathedralCenter, 0.01f, 0.1f, 10f);
                context.PoseCommand = 5;
                context.SetChargeState(3, 0.5f + buildT / (float)BuildTime * 0.5f);

                if (buildT % (BuildTime / NodeCount) == 1) {
                    int i = buildT / (BuildTime / NodeCount);
                    if (i < NodeCount) {
                        if (!VaultUtils.isClient) {
                            SpawnCathedralNode(context, i);
                        }
                        SoundEngine.PlaySound(SoundID.Item29 with {
                            Volume = 0.8f, Pitch = -0.4f + i * 0.16f, MaxInstances = 3
                        }, cathedralCenter);
                    }
                }
                if (!VaultUtils.isServer && Timer % 3 == 0) {
                    QueenMotion.ChargeGatherFX(QueenSlimeRenderHelper.CrownAnchor(npc), buildT / (float)BuildTime, 240f, buildT * 0.01f);
                }
                return null;
            }

            //织网帧(服务端一次)
            if (Timer == WebStart + 1 && !VaultUtils.isClient) {
                WeaveCathedral(context);
            }

            //幕三 圣殿运转
            if (Timer <= GutterStart) {
                int webT = Timer - WebStart;
                //皇后居中缓旋，全程锁血解除(奖励击打窗在此段开启)
                npc.dontTakeDamage = false;
                float swayT = webT * 0.016f;
                Vector2 anchor = cathedralCenter + new Vector2((float)Math.Sin(swayT) * 70f, (float)Math.Cos(swayT * 1.4f) * 46f);
                QueenMotion.SpringHover(npc, anchor, 0.01f, 0.1f, 9f);
                context.PoseCommand = 5;
                context.PrismShimmer = 1f;
                context.SetChargeState(3, 0.85f + 0.15f * (float)Math.Sin(webT * 0.09f));
                FaceTarget(npc, player.Center);

                //穹顶陨雨波(服务端)：每70f一波，自圣殿顶缘洒向内部
                if (webT % 70 == 24 && !VaultUtils.isClient) {
                    FireDomeRain(context, webT / 70);
                }
                if (webT % 70 == 24) {
                    SoundEngine.PlaySound(SoundID.Item155 with { Volume = 0.75f, Pitch = 0.3f }, npc.Center);
                    context.PushSquash(0.3f);
                }
                return null;
            }

            //幕四 过载死寂：光束熄灭，一切静止
            if (Timer <= ShatterFrame) {
                npc.velocity *= 0.85f;
                context.ResetChargeState();
                context.PrismShimmer = MathHelper.Clamp(1f - (Timer - GutterStart) / (float)SilenceTime, 0f, 1f);
                if (Timer == GutterStart + 2) {
                    SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.7f, Pitch = 0.8f }, npc.Center);
                }
                //死寂帧：全碎
                if (Timer == ShatterFrame) {
                    DoGrandShatter(context);
                }
                return null;
            }

            //幕五 力竭奖励窗：缓缓下坠喘息
            npc.dontTakeDamage = false;
            npc.velocity = Vector2.Lerp(npc.velocity, new Vector2(0f, 1.4f), 0.06f);
            context.PoseCommand = 0;
            context.WingFlapBoost = 0.2f;
            if (Timer % 12 == 0) {
                context.PushSquash(-0.12f);
            }

            if (Timer >= TotalTime && !VaultUtils.isClient) {
                return new QueenAerialBalletState();
            }
            return null;
        }

        /// <summary>生成一根圣殿晶柱(服务端)，槽位+100标记圣殿血量档</summary>
        private void SpawnCathedralNode(QueenSlimeStateContext context, int i) {
            //首柱前清掉战场残留节点，防旧节点混入圣殿几何
            if (i == 0) {
                foreach (var stale in context.CollectPrismNodes()) {
                    QueenMotion.ScriptKill(stale);
                }
            }
            float angle = MathHelper.TwoPi * i / NodeCount - MathHelper.PiOver2;
            Vector2 pos = cathedralCenter + angle.ToRotationVector2() * CathedralRadius;
            NPC node = QueenMotion.SpawnMinion(context.Npc, NPCID.QueenSlimeMinionBlue,
                QueenMinionRole.PrismNode, QueenPrismNodeAI.CathedralSlotOffset + i, pos,
                QueenSlimeMinionAI.PrismNodeLife(cathedral: true));
            if (node != null) {
                node.ai[3] = BuildTime + WebTime + SilenceTime + 40;
                node.netUpdate = true;
            }
        }

        /// <summary>织圣殿(服务端)：辐辏马灯+外环反向马灯</summary>
        private void WeaveCathedral(QueenSlimeStateContext context) {
            List<NPC> nodes = context.CollectPrismNodes();
            if (nodes.Count == 0) {
                return;
            }

            //王冠→各柱辐辏(跑马灯)
            for (int i = 0; i < nodes.Count; i++) {
                Projectile.NewProjectile(context.Npc.GetSource_FromAI(), QueenSlimeRenderHelper.CrownAnchor(context.Npc),
                    Vector2.Zero, ModContent.ProjectileType<QueenPrismBeamProj>(), QueenPrismBeamProj.BeamDamage, 0f, Main.myPlayer,
                    context.Npc.whoAmI, nodes[i].whoAmI,
                    QueenPrismBeamProj.PackMode(QueenPrismBeamProj.BeamMode.CathedralSpoke, i, WebTime));
            }

            //柱间外环(反向跑马灯，phase 反排)
            for (int i = 0; i < nodes.Count; i++) {
                NPC from = nodes[i];
                NPC to = nodes[(i + 1) % nodes.Count];
                Projectile.NewProjectile(context.Npc.GetSource_FromAI(), from.Center, Vector2.Zero,
                    ModContent.ProjectileType<QueenPrismBeamProj>(), QueenPrismBeamProj.BeamDamage, 0f, Main.myPlayer,
                    from.whoAmI, to.whoAmI,
                    QueenPrismBeamProj.PackMode(QueenPrismBeamProj.BeamMode.WebMarquee, nodes.Count - 1 - i, WebTime));
            }
        }

        /// <summary>穹顶陨雨(服务端)：自圣殿上缘向内洒落</summary>
        private void FireDomeRain(QueenSlimeStateContext context, int wave) {
            NPC npc = context.Npc;
            int count = context.IsDeathMode ? 7 : 5;
            float halfSpan = CathedralRadius * 0.82f;
            float stagger = wave % 2 == 1 ? halfSpan / count : 0f;
            for (int i = 0; i < count; i++) {
                float x = cathedralCenter.X + MathHelper.Lerp(-halfSpan, halfSpan, i / (float)(count - 1)) + stagger;
                Vector2 spawn = new Vector2(x, cathedralCenter.Y - CathedralRadius - 60f);
                Vector2 vel = new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), 2.2f);
                Projectile.NewProjectile(npc.GetSource_FromAI(), spawn, vel,
                    ModContent.ProjectileType<QueenGelMeteorProj>(), QueenGelMeteorProj.MeteorDamage, 0f, Main.myPlayer,
                    1f, 0f, (wave * count + i) * 0.11f);
            }
        }

        /// <summary>全碎终拍：柱毁+径向碎晶+全场唯一大震</summary>
        private void DoGrandShatter(QueenSlimeStateContext context) {
            NPC npc = context.Npc;

            if (!VaultUtils.isClient) {
                foreach (var node in context.CollectPrismNodes()) {
                    //每柱放射五枚碎晶
                    Vector2 outDir = (node.Center - cathedralCenter).SafeNormalize(Vector2.UnitY);
                    for (int i = -2; i <= 2; i++) {
                        Vector2 vel = outDir.RotatedBy(i * 0.3f) * 8.8f;
                        Projectile.NewProjectile(npc.GetSource_FromAI(), node.Center, vel,
                            ModContent.ProjectileType<QueenShardProj>(), QueenShardProj.ShardDamage, 0f, Main.myPlayer,
                            (int)QueenShardProj.Mode.Shard, 0f, i * 0.2f + 0.5f);
                    }
                    QueenMotion.ScriptKill(node);
                }
            }

            if (!VaultUtils.isServer) {
                //全场唯一的大震与层叠碎音
                QueenMotion.Shake(cathedralCenter, 13f, 34, "QueenCathedralShatter");
                SoundEngine.PlaySound(SoundID.Shatter with { Volume = 1.1f, Pitch = -0.2f }, cathedralCenter);
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.9f, Pitch = -0.4f }, cathedralCenter);
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.9f, Pitch = -0.6f }, cathedralCenter);
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_DWave>(cathedralCenter, Vector2.Zero,
                        QueenMotion.PrismHue(i * 0.33f) * 0.9f, 0.5f + i * 0.2f)?
                        .Configure(new Vector2(1f, 1f), 0f, 2.2f + i * 0.7f, 26);
                }
            }
            context.PushSquash(-0.4f);
        }

        public override void OnExit(QueenSlimeStateContext context) {
            base.OnExit(context);
            NPC npc = context.Npc;
            npc.dontTakeDamage = false;
            DisableContactDamage(npc);
            npc.noGravity = true;
            npc.noTileCollide = true;
        }
    }
}
