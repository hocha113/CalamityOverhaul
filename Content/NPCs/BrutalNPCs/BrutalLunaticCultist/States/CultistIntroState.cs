using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>入场：帷幕→法阵描绘→吸气静默→炸阵亮相→升空开战</summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.Intro, typeof(CultistStateContext))]
    internal class CultistIntroState : CultistStateBase
    {
        public override string StateName => "Intro";
        public override CultistStateIndex StateIndex => CultistStateIndex.Intro;

        private const int VeilRise = 40;
        private const int SigilDrawEnd = 150;
        private const int SilenceEnd = 165;
        private const int RevealHold = 240;
        private const int IntroEnd = 276;

        private Vector2 introCenter;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            introCenter = context.Npc.Center;
            context.Npc.alpha = 255;
            context.Npc.dontTakeDamage = true;
            context.Npc.velocity = Vector2.Zero;
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            context.SkipDefaultHover = true;
            npc.velocity = Vector2.Zero;
            context.CastPose = CultistPose.Stand;

            //舞台法阵常驻声明
            context.StageSigilPos = introCenter + new Vector2(0f, 46f);
            context.StageSigilRadius = 210f;

            //幕一 黑幕升起
            if (Timer <= VeilRise) {
                CultistScreenFX.DeclareVeil(introCenter, 0.5f * (Timer / (float)VeilRise), context.Element);
                if (Timer == 12) {
                    CultistRenderHelper.ChantVoice(introCenter, 0.9f, -0.4f);
                }
                return null;
            }

            //幕二 法阵描绘+符文汇聚
            if (Timer <= SigilDrawEnd) {
                float t = (Timer - VeilRise) / (float)(SigilDrawEnd - VeilRise);
                CultistScreenFX.DeclareVeil(introCenter, 0.5f, context.Element);
                context.StageSigilProgress = t;

                //身影渐显
                npc.alpha = (int)MathHelper.Lerp(255f, 90f, MathHelper.Clamp((Timer - 90f) / 60f, 0f, 1f));

                if (!VaultUtils.isServer) {
                    CultistRenderHelper.ConvergeRunes(context.StageSigilPos, 420f, context.Element, 0.5f + t * 0.8f);
                    //吟唱加速升调
                    int interval = (int)MathHelper.Lerp(34f, 16f, t);
                    if ((int)Timer % interval == 0) {
                        CultistRenderHelper.ChantVoice(introCenter, 0.7f, MathHelper.Lerp(-0.3f, 0.25f, t));
                    }
                }
                return null;
            }

            //幕三 吸气静默：粒子全停、法阵微缩
            if (Timer <= SilenceEnd) {
                CultistScreenFX.DeclareVeil(introCenter, 0.55f, context.Element);
                context.StageSigilProgress = 1f - (Timer - SigilDrawEnd) / (float)(SilenceEnd - SigilDrawEnd) * 0.12f;
                npc.alpha = 90;
                return null;
            }

            //亮相帧
            if ((int)Timer == SilenceEnd + 1) {
                npc.alpha = 0;
                CultistScreenFX.PushFlash(0.85f, 24);
                CultistScreenFX.Punch(introCenter, 9f, 18, "CultistIntro");
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie105 with { Volume = 1.1f }, introCenter);
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.8f, Pitch = -0.2f }, introCenter);
                    //三相元素预示：120°错开的三色喷发
                    for (int e = 0; e < 3; e++) {
                        for (int i = 0; i < 9; i++) {
                            float baseAngle = -MathHelper.PiOver2 + e * MathHelper.TwoPi / 3f;
                            Vector2 vel = (baseAngle + Main.rand.NextFloat(-0.4f, 0.4f)).ToRotationVector2()
                                * Main.rand.NextFloat(4f, 11f);
                            CultistRenderHelper.SpawnElementMote(npc.Center, vel, (CultistElement)e,
                                Main.rand.NextFloat(0.8f, 1.4f), Main.rand.Next(20, 34));
                        }
                    }
                }
            }

            //幕四 亮相定格→嘶吼
            if (Timer <= RevealHold) {
                float t = (Timer - SilenceEnd) / (float)(RevealHold - SilenceEnd);
                context.CastPose = Timer < SilenceEnd + 40 ? CultistPose.Scream : CultistPose.Float;
                context.CastGlow = 1f - t * 0.5f;
                context.StageSigilProgress = 1f - t;
                context.StageSigilFlash = 1f - t;
                CultistScreenFX.DeclareVeil(introCenter, MathHelper.Lerp(0.55f, 0.18f, t), context.Element);
                npc.alpha = 0;
                FaceTarget(context);
                return null;
            }

            //升空入战位
            context.SkipDefaultHover = false;
            if (context.Target.Alives()) {
                SetHover(context, context.Target.Center + new Vector2(0f, -320f));
            }
            CultistScreenFX.DeclareVeil(introCenter, 0.12f, context.Element);

            if (Timer >= IntroEnd) {
                npc.dontTakeDamage = false;
                return new CultistWeaveState();
            }
            return null;
        }

        public override void OnExit(CultistStateContext context) {
            base.OnExit(context);
            context.Npc.dontTakeDamage = false;
            context.Npc.alpha = 0;
        }
    }
}
