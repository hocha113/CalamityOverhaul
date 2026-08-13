using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.States
{
    /// <summary>入场演出：地狱深处隆起→墙体升起→器官苏醒</summary>
    [InnoVault.StateMachines.VaultState((int)WofStateIndex.Intro, typeof(WofStateContext))]
    internal class WofIntroState : WofStateBase
    {
        public override string StateName => "Intro";
        public override WofStateIndex StateIndex => WofStateIndex.Intro;

        /// <summary>深处隆起(闷震)</summary>
        private const int RumbleEnd = 46;
        /// <summary>墙体升起</summary>
        private const int RiseEnd = 170;
        /// <summary>器官苏醒帧</summary>
        private const int OrganFrame = 178;
        private const int TotalTime = 212;

        /// <summary>本端已播报文本</summary>
        private bool textShown;

        public override void OnEnter(WofStateContext context) {
            base.OnEnter(context);
            textShown = false;
        }

        public override IWofState OnUpdate(WofStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            //登场期不咬人也不可被打
            npc.damage = 0;
            npc.dontTakeDamage = true;
            context.SpeedOverride = 0.5f;
            context.SuppressYAnchor = true;
            WofWallField.CinematicAreaLock = 1;
            WofWallField.ComputeScanTargets(npc, out int topTarget, out int bottomTarget);

            //墙域未初始化时立即以地缝形态初始化(避免首帧整墙闪现)
            if (Main.wofDrawAreaBottom == -1 || Main.wofDrawAreaTop == -1) {
                Main.wofDrawAreaBottom = bottomTarget;
                Main.wofDrawAreaTop = bottomTarget - 160;
            }

            if (Timer <= RumbleEnd) {
                UpdateRumble(context, bottomTarget);
            }
            else if (Timer <= RiseEnd) {
                UpdateRise(context, topTarget, bottomTarget);
            }
            else {
                UpdateOrgans(context, topTarget, bottomTarget);
            }

            if (Timer >= TotalTime) {
                npc.dontTakeDamage = false;
                npc.damage = npc.defDamage;
                return new WofAdvanceState();
            }
            return null;
        }

        /// <summary>深处隆起：墙域压成地缝，闷震与血雾自地面渗出</summary>
        private void UpdateRumble(WofStateContext context, int bottomTarget) {
            NPC npc = context.Npc;
            Main.wofDrawAreaBottom = bottomTarget;
            Main.wofDrawAreaTop = bottomTarget - 160;
            //口器埋在地下
            npc.position.Y = bottomTarget + 140f;
            npc.velocity.Y = 0f;
            context.WallFlush = 0.5f;

            if (VaultUtils.isServer) {
                return;
            }
            if (Timer == 2) {
                SoundEngine.PlaySound(SoundID.NPCDeath10 with { Pitch = -0.8f, Volume = 0.7f }, npc.Center);
            }
            float p = Timer / (float)RumbleEnd;
            if (Timer % 4 == 0) {
                WofMotionFX.CameraPunch(npc.Center, 1.2f + 3.4f * p * p, 10, "WofIntroRumble");
            }
            //地缝渗血雾
            if (Timer % 3 == 0) {
                float x = npc.Center.X + Main.rand.NextFloat(-500f, 500f);
                PRTLoader.NewParticle<PRT_WofBloodMist>(new Vector2(x, bottomTarget - Main.rand.NextFloat(0f, 60f)),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(1.2f, 3f)),
                    WofMotionFX.BloodDark, Main.rand.NextFloat(0.9f, 1.6f))?.Configure(Main.rand.Next(40, 70), 0.5f);
            }
            if (Timer % 9 == 0) {
                WofMotionFX.SpawnBloodBurst(new Vector2(npc.Center.X + Main.rand.NextFloat(-380f, 380f), bottomTarget - 20f),
                    0.5f + p * 0.5f);
            }
        }

        /// <summary>墙体升起：上缘以缓动展开到全高，口器随中线浮出</summary>
        private void UpdateRise(WofStateContext context, int topTarget, int bottomTarget) {
            NPC npc = context.Npc;
            float p = MathHelper.Clamp((Timer - RumbleEnd) / (float)(RiseEnd - RumbleEnd), 0f, 1f);
            //先慢后快再收：立墙的力量感
            float ease = p < 0.5f ? 2f * p * p : 1f - (float)System.Math.Pow(-2f * p + 2f, 2f) / 2f;

            Main.wofDrawAreaBottom = bottomTarget;
            Main.wofDrawAreaTop = (int)MathHelper.Lerp(bottomTarget - 160, topTarget, ease);

            //口器从地下抬到中线
            float middle = (Main.wofDrawAreaTop + Main.wofDrawAreaBottom) * 0.5f - npc.height / 2;
            npc.position.Y = MathHelper.Lerp(bottomTarget + 140f, middle, ease);
            npc.velocity.Y = 0f;
            context.WallFlush = 0.5f + 0.4f * p;
            context.MouthCommand = 2;

            if (VaultUtils.isServer) {
                return;
            }
            if (Timer == RumbleEnd + 1) {
                WofMotionFX.MouthRoar(npc, 1.2f);
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 1f, Pitch = -0.6f }, npc.Center);
            }
            //升起的面缘大量渗血
            WofMotionFX.SpawnWallSeep(npc, 3.2f);
            if (Timer % 5 == 0) {
                WofMotionFX.CameraPunch(npc.Center, 2.6f, 8, "WofIntroRise");
            }
            //上缘顶开的碎肉
            if (Timer % 4 == 0) {
                float x = npc.Center.X + Main.rand.NextFloat(-320f, 320f);
                PRTLoader.NewParticle<PRT_WofGore>(new Vector2(x, Main.wofDrawAreaTop + 20f),
                    new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(3f, 8f)),
                    WofMotionFX.BloodDark, Main.rand.NextFloat(0.25f, 0.5f))?.Configure(Main.rand.Next(40, 70));
            }
        }

        /// <summary>器官苏醒：双眼与饥饿者破膜而出，终吼定场</summary>
        private void UpdateOrgans(WofStateContext context, int topTarget, int bottomTarget) {
            NPC npc = context.Npc;
            Main.wofDrawAreaBottom = bottomTarget;
            Main.wofDrawAreaTop = topTarget;
            float middle = (Main.wofDrawAreaTop + Main.wofDrawAreaBottom) * 0.5f - npc.height / 2;
            npc.position.Y = middle;
            npc.velocity.Y = 0f;
            context.WallFlush = 1f;
            context.MouthCommand = 1;

            //器官破膜(服务端生成，镜像原版位置与ai)
            if (Timer == OrganFrame && !VaultUtils.isClient) {
                float eyeTopY = (npc.Center.Y + Main.wofDrawAreaTop) / 2f;
                int eye = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.position.X, (int)eyeTopY, NPCID.WallofFleshEye, npc.whoAmI, 1f);
                if (eye < Main.maxNPCs) {
                    Main.npc[eye].netUpdate = true;
                }
                float eyeBottomY = (npc.Center.Y + Main.wofDrawAreaBottom) / 2f;
                eye = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.position.X, (int)eyeBottomY, NPCID.WallofFleshEye, npc.whoAmI, -1f);
                if (eye < Main.maxNPCs) {
                    Main.npc[eye].netUpdate = true;
                }

                for (int i = 0; i < 11; i++) {
                    int hungry = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.position.X, (int)eyeBottomY,
                        NPCID.TheHungry, npc.whoAmI, i * 0.1f - 0.05f);
                    if (hungry < Main.maxNPCs) {
                        Main.npc[hungry].netUpdate = true;
                    }
                }
            }

            if (VaultUtils.isServer) {
                return;
            }

            if (!textShown) {
                textShown = true;
                VaultUtils.Text(WallOfFleshAI.WofIntroText.Value, WofMotionFX.BloodHot);
            }

            //破膜血浆(各端本地，位置确定性推导)
            if (Timer == OrganFrame) {
                float eyeTopY = (npc.Center.Y + Main.wofDrawAreaTop) / 2f;
                float eyeBottomY = (npc.Center.Y + Main.wofDrawAreaBottom) / 2f;
                WofMotionFX.SpawnBloodBurst(new Vector2(npc.Center.X, eyeTopY), 1.4f, new Vector2(npc.direction, 0f));
                WofMotionFX.SpawnBloodBurst(new Vector2(npc.Center.X, eyeBottomY), 1.4f, new Vector2(npc.direction, 0f));
                SoundEngine.PlaySound(SoundID.NPCDeath12 with { Volume = 1f }, npc.Center);
            }
            if (Timer == OrganFrame + 16) {
                WofMotionFX.MouthRoar(npc, 1.5f);
                WofMotionFX.CameraPunch(npc.Center, 8f, 22, "WofIntroFinale");
            }
        }
    }
}
