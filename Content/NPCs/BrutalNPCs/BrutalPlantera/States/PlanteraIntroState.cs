using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Rendering;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.States
{
    /// <summary>
    /// 入场演出：地底震颤→三根钩爪破土锚定→藤蔓把花苞从土里拽出来→
    /// 悬吊静场亮脉→花苞绽放开战
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PlanteraStateIndex.Intro, typeof(PlanteraStateContext))]
    internal class PlanteraIntroState : PlanteraStateBase
    {
        public override string StateName => "Intro";
        public override PlanteraStateIndex StateIndex => PlanteraStateIndex.Intro;

        #region 节奏常量
        private const int OmenEnd = 50;      //地底预兆
        private const int HooksEnd = 96;     //钩爪破土
        private const int RiseEnd = 152;     //花苞被拽出
        private const int StillEnd = 208;    //悬吊静场(威压拍)
        private const int BloomEnd = 238;    //绽放
        #endregion

        private Vector2 burialPoint;
        private Vector2 hangPoint;
        private bool bloomFired;

        public PlanteraIntroState() {
        }

        public override void OnEnter(PlanteraStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            bloomFired = false;

            NPC npc = context.Npc;
            npc.dontTakeDamage = true;
            npc.damage = 0;
        }

        public override IPlanteraState OnUpdate(PlanteraStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            context.SkipDefaultMovement = true;
            npc.damage = 0;
            context.RotationMode = 2;

            Timer++;

            if (Timer <= OmenEnd) {
                UpdateOmen(context);
                return null;
            }
            if (Timer <= HooksEnd) {
                UpdateHookLaunch(context);
                return null;
            }
            if (Timer <= RiseEnd) {
                UpdateRise(context);
                return null;
            }
            if (Timer <= StillEnd) {
                UpdateStill(context);
                return null;
            }
            if (Timer <= BloomEnd) {
                UpdateBloom(context);
                return null;
            }

            return new PlanteraCanopyState();
        }

        #region 幕一 地底预兆
        private void UpdateOmen(PlanteraStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            npc.velocity = Vector2.Zero;
            npc.dontTakeDamage = true;

            if (Timer == 1) {
                //本体埋进玩家脚下深处，隐形待命
                burialPoint = player.Center + new Vector2(0f, 560f);
                npc.Center = burialPoint;
                npc.alpha = 255;
                npc.netUpdate = true;
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.55f, Pitch = -0.7f }, player.Center);
            }

            //地面隆隆，t³爬升
            float t = Timer / (float)OmenEnd;
            float ramp = t * t * t;
            if (!VaultUtils.isServer) {
                Vector2 ground = player.Bottom + new Vector2(0f, 8f);
                int dustCount = 1 + (int)(ramp * 4f);
                for (int i = 0; i < dustCount; i++) {
                    Dust dust = Dust.NewDustDirect(ground + new Vector2(Main.rand.NextFloat(-160f, 160f), 0f),
                        4, 4, Main.rand.NextBool() ? DustID.Dirt : DustID.JungleGrass,
                        0, 0, 110, default, Main.rand.NextFloat(1f, 2f));
                    dust.velocity = new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(1.5f, 3.5f + ramp * 4f));
                }
                if (Timer % 13 == 0) {
                    PlanteraScreenFX.CameraPunch(player.Center, 1f + ramp * 3f, 12, "PlanteraIntroRumble");
                    SoundEngine.PlaySound(SoundID.WormDig with {
                        Volume = 0.4f + ramp * 0.5f,
                        Pitch = -0.7f + ramp * 0.3f,
                        MaxInstances = 3
                    }, player.Center);
                }
            }
        }
        #endregion

        #region 幕二 钩爪破土
        private void UpdateHookLaunch(PlanteraStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            npc.velocity = Vector2.Zero;

            //服务端逐根生成+派锚：左上/右上/正下三方合围
            if (!VaultUtils.isClient) {
                int localTimer = Timer - OmenEnd;
                if (localTimer == 1 || localTimer == 13 || localTimer == 25) {
                    int ordinal = (localTimer - 1) / 12;
                    int hookIndex = PlanteraAI.SpawnHook(npc, ordinal);
                    if (hookIndex >= 0 && hookIndex < Main.maxNPCs) {
                        NPC hook = Main.npc[hookIndex];
                        hook.Center = burialPoint;
                        float angle = ordinal switch {
                            0 => -MathHelper.PiOver2 - 0.85f,
                            1 => -MathHelper.PiOver2 + 0.85f,
                            _ => MathHelper.PiOver2,
                        };
                        Vector2 wish = player.Center + angle.ToRotationVector2() * (ordinal == 2 ? 360f : 430f);
                        Vector2 anchor = PlanteraHookAI.FindAnchorNear(wish, 10f, Vector2.Zero);
                        PlanteraHookAI.Command(hook, anchor);
                        hook.netUpdate = true;
                    }
                }
            }

            //破土帧本地反馈(按钩爪出土时机近似)
            if (!VaultUtils.isServer) {
                int localTimer = Timer - OmenEnd;
                if (localTimer == 2 || localTimer == 14 || localTimer == 26) {
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.9f, Pitch = -0.4f }, player.Center);
                    PlanteraScreenFX.CameraPunch(player.Center, 3.5f, 10, "PlanteraIntroHook");
                }
            }
        }
        #endregion

        #region 幕三 花苞被拽出
        private void UpdateRise(PlanteraStateContext context) {
            NPC npc = context.Npc;

            //钩爪刚生成，别等周期刷新
            if (Timer == HooksEnd + 1) {
                context.RefreshParts();
            }

            float t = (Timer - HooksEnd) / (float)(RiseEnd - HooksEnd);
            //末段弹性过冲
            float ease = VaultUtils.EaseOutBack(MathHelper.Clamp(t, 0f, 1f));

            hangPoint = context.HookCentroid();
            Vector2 path = Vector2.Lerp(burialPoint, hangPoint, ease);
            npc.velocity = path - npc.Center;

            //破土显形
            if (t > 0.12f && npc.alpha > 0) {
                npc.alpha = Math.Max(npc.alpha - 26, 0);
                if (!VaultUtils.isServer && npc.alpha > 120) {
                    for (int i = 0; i < 6; i++) {
                        Dust dust = Dust.NewDustDirect(npc.BottomLeft, npc.width, 12,
                            DustID.Dirt, 0, 0, 90, default, Main.rand.NextFloat(1.4f, 2.4f));
                        dust.velocity = new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(2f, 6f));
                    }
                }
            }

            if (Timer == HooksEnd + 8 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.WormDig with { Volume = 1f, Pitch = 0.25f }, npc.Center);
                PlanteraScreenFX.CameraPunch(npc.Center, 5f, 14, "PlanteraIntroRise");
            }

            //藤蔓拽紧行波
            if (!VaultUtils.isServer) {
                foreach (var hook in context.Hooks) {
                    PlanteraVineRenderer.PushPulse(hook.whoAmI, 0.4f + t * 0.4f);
                }
            }

            context.RotationMode = 0;
        }
        #endregion

        #region 幕四 悬吊静场
        private void UpdateStill(PlanteraStateContext context) {
            NPC npc = context.Npc;

            //几乎静止，只余轻摆——威压来自静
            npc.velocity *= 0.86f;
            context.RotationMode = 0;
            npc.alpha = 0;

            float t = (Timer - RiseEnd) / (float)(StillEnd - RiseEnd);
            context.GlowPulse = t * 0.55f;

            //荧光自钩爪流向本体：脉络点亮
            if (!VaultUtils.isServer) {
                foreach (var hook in context.Hooks) {
                    PlanteraVineRenderer.PushPulse(hook.whoAmI, 0.25f + t * 0.45f);
                }
                if (Main.rand.NextBool(3)) {
                    PlanteraRenderHelper.SpawnAmbientMote(npc.Center + Main.rand.NextVector2Circular(120f, 100f), false);
                }
                //吸入聚能
                if (t > 0.4f) {
                    PlanteraRenderHelper.SpawnChargeIntake(context, t * 0.7f);
                }
            }
        }
        #endregion

        #region 幕五 绽放
        private void UpdateBloom(PlanteraStateContext context) {
            NPC npc = context.Npc;

            if (!bloomFired) {
                bloomFired = true;
                npc.dontTakeDamage = false;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.15f, Pitch = 0.15f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.Grass with { Volume = 1f, Pitch = -0.5f }, npc.Center);
                    PlanteraRenderHelper.SpawnPetalBurst(npc.Center, 26, 9f, false);
                    PlanteraScreenFX.CameraPunch(npc.Center, 8f, 18, "PlanteraIntroBloom");
                    PlanteraScreenFX.PushFlash(npc.Center, 0.45f, 12);
                    PlanteraScreenFX.PushRing(npc.Center, 620f, false, 30);
                }
            }

            npc.velocity *= 0.9f;
            context.GlowPulse = MathHelper.Lerp(0.9f, 0.35f, (Timer - StillEnd) / (float)(BloomEnd - StillEnd));
        }
        #endregion

        public override void OnExit(PlanteraStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            NPC npc = context.Npc;
            npc.alpha = 0;
            npc.dontTakeDamage = false;
            npc.damage = npc.defDamage;
        }
    }
}
