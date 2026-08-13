using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.States
{
    /// <summary>阶段转换演出：跪伏震颤→王冠升辉→展翅绽光→升空亮相</summary>
    [InnoVault.StateMachines.VaultState((int)QueenSlimeStateIndex.PhaseTransition, typeof(QueenSlimeStateContext))]
    internal class QueenPhaseTransitionState : QueenSlimeStateBase
    {
        public override string StateName => "PhaseTransition";
        public override QueenSlimeStateIndex StateIndex => QueenSlimeStateIndex.PhaseTransition;

        #region 节奏常量
        private const int KneelTime = 60;             //跪伏震颤
        private const int UnfurlStart = KneelTime;    //展翅起点
        private const int BurstFrame = KneelTime + 34;//绽光帧
        private const int UnfurlEnd = KneelTime + 92; //翼全展
        private const int AscendEnd = UnfurlEnd + 80; //升空完成 ~232
        #endregion

        public QueenPhaseTransitionState() {
        }

        public override void OnEnter(QueenSlimeStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            npc.dontTakeDamage = true;
            DisableContactDamage(npc);
            npc.velocity.X = 0f;
            npc.noGravity = false;
            npc.noTileCollide = false;

            //清我方弹幕，公平阀
            if (!VaultUtils.isClient) {
                QueenProjHelper.ClearQueenProjectiles();
            }
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.9f, Pitch = -0.3f }, npc.Center);
        }

        public override IQueenSlimeState OnUpdate(QueenSlimeStateContext context) {
            NPC npc = context.Npc;

            Timer++;
            npc.dontTakeDamage = true;
            DisableContactDamage(npc);

            //幕一 跪伏震颤：凝胶失稳打摆
            if (Timer <= KneelTime) {
                npc.velocity.X *= 0.8f;
                context.PoseCommand = 3;
                float p = Timer / (float)KneelTime;
                //震颤幅度爬升
                context.PushSquash(0.14f * p * (float)System.Math.Sin(Timer * 0.55f));
                context.SetChargeState(3, p * 0.6f);
                if (!VaultUtils.isServer && Timer % 4 == 0) {
                    QueenMotion.ChargeGatherFX(npc.Center, p, 170f, p * 0.5f);
                    QueenMotion.GelSplashBurst(npc.Bottom, 0.45f, 2);
                }
                return null;
            }

            //幕二 展翅：翼展开度推进+绽光帧
            if (Timer <= UnfurlEnd) {
                float p = (Timer - UnfurlStart) / (float)(UnfurlEnd - UnfurlStart);
                context.WingSpread = QueenMotion.SnapOut(p, 4);
                context.WingFlapBoost = 1.4f;
                context.SetChargeState(3, 0.6f + p * 0.4f);
                context.PrismShimmer = p;
                context.PoseCommand = 1;
                npc.velocity.X *= 0.9f;

                if (Timer == BurstFrame) {
                    DoRadianceBurst(context);
                }
                return null;
            }

            //幕三 升空亮相
            if (Timer <= AscendEnd) {
                float p = (Timer - UnfurlEnd) / (float)(AscendEnd - UnfurlEnd);
                npc.noGravity = true;
                npc.noTileCollide = true;
                Vector2 anchor = context.Target.Center + new Vector2(0f, -360f);
                QueenMotion.SpringHover(npc, anchor, 0.012f * (0.4f + p), 0.1f, 16f);
                QueenMotion.FlightLean(npc);
                context.PoseCommand = 5;
                context.WingSpread = 1f;
                context.WingFlapBoost = 1f - p * 0.5f;
                context.PrismShimmer = 1f - p * 0.4f;

                //亮相尾拍：翼卫入列(服务端)
                if (Timer == AscendEnd - 30 && !VaultUtils.isClient) {
                    for (int i = 0; i < 2; i++) {
                        QueenMotion.SpawnMinion(npc, NPCID.QueenSlimeMinionPurple, QueenMinionRole.WingedEscort,
                            i, npc.Center + new Vector2((i == 0 ? -1 : 1) * 130f, -40f), QueenSlimeMinionAI.EscortLife());
                    }
                }
                return null;
            }

            //收演出，进入空中芭蕾
            context.Phase2Unfolded = true;
            npc.dontTakeDamage = false;
            if (!VaultUtils.isClient) {
                return new QueenAerialBalletState();
            }
            return null;
        }

        /// <summary>绽光帧：广播+光暴+羽压轻波</summary>
        private static void DoRadianceBurst(QueenSlimeStateContext context) {
            NPC npc = context.Npc;

            if (!VaultUtils.isServer) {
                VaultUtils.Text(QueenSlimeAI.QueenSlime_WingsText.Value, QueenMotion.RoyalPink);

                QueenMotion.CrystalShatterBurst(npc.Center, 1.5f, 0.15f, playSound: false);
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_DWave>(npc.Center, Vector2.Zero,
                        QueenMotion.PrismHue(i * 0.33f) * 0.85f, 0.3f + i * 0.12f)?
                        .Configure(new Vector2(1f, 1f), 0f, 1.5f + i * 0.5f, 22);
                }
                for (int i = 0; i < 14; i++) {
                    float ang = MathHelper.TwoPi * i / 14f;
                    PRTLoader.NewParticle<PRT_Sparkle>(npc.Center, ang.ToRotationVector2() * Main.rand.NextFloat(5f, 12f),
                        Color.White, Main.rand.NextFloat(0.8f, 1.3f))?
                        .Configure(QueenMotion.PrismHue(i / 14f), 30, 0.06f, 1.6f);
                }
                QueenMotion.Shake(npc.Center, 9f, 22, "QueenUnfurl");
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 1.1f, Pitch = 0.5f }, npc.Center);
                SoundEngine.PlaySound(SoundID.Item165 with { Volume = 0.8f, Pitch = 0.2f }, npc.Center);
            }
            context.PushSquash(0.55f);
        }

        public override void OnExit(QueenSlimeStateContext context) {
            base.OnExit(context);
            NPC npc = context.Npc;
            context.Phase2Unfolded = true;
            context.WingSpread = 1f;
            npc.dontTakeDamage = false;
            npc.noGravity = true;
            npc.noTileCollide = true;
        }
    }
}
