using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.States
{
    /// <summary>
    /// 入场演出：地面凝胶自四方汇流→池面隆起拔塔→塔身坍缩成王体→王冠天降扣顶→静止亮相
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)KingSlimeStateIndex.Intro, typeof(KingSlimeStateContext))]
    internal class KingSlimeIntroState : KingSlimeStateBase
    {
        public override string StateName => "Intro";
        public override KingSlimeStateIndex StateIndex => KingSlimeStateIndex.Intro;

        #region 节奏常量(运镜对齐)
        internal const int GatherEnd = 80;
        internal const int RiseEnd = 128;
        internal const int CondenseEnd = 158;
        internal const int CrownHitFrame = 186;
        internal const int StillEnd = 252;
        internal const int IntroEnd = 268;
        #endregion

        private Vector2 stagePoint;
        private bool stageInit;

        public override void OnEnter(KingSlimeStateContext context) {
            base.OnEnter(context);
            stageInit = false;
            context.Npc.dontTakeDamage = true;
        }

        public override IKingSlimeState OnUpdate(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            context.ContactDamageScale = 0f;
            context.SkipGravity = true;
            npc.velocity = Vector2.Zero;
            //加冕帧(含)前头顶无冠，命中帧由天降演出层画在锚点，次帧交棒常驻扣冠层
            context.HideCrown = Timer <= CrownHitFrame;

            //初始化舞台点：目标侧向地面；客户端不自算，跟随服务端同步的本体位置反推
            if (!stageInit) {
                stageInit = true;
                if (!VaultUtils.isClient) {
                    int side = npc.Center.X >= player.Center.X ? 1 : -1;
                    stagePoint = KingSlimeGelFX.FindGroundBelow(player.Center + new Vector2(side * 400f, -40f));
                    //本体先藏在舞台点地下
                    npc.Bottom = stagePoint + new Vector2(0f, 60f);
                    npc.netUpdate = true;
                }
                else {
                    stagePoint = npc.Bottom - new Vector2(0f, 60f);
                }
                context.HideBodySprite = true;
            }
            //汇聚幕内客户端持续贴齐服务端位置(防开场包迟到造成锚点漂移)
            if (VaultUtils.isClient && Timer <= GatherEnd) {
                stagePoint = npc.Bottom - new Vector2(0f, 60f);
            }

            if (Timer <= GatherEnd) {
                UpdateGather(context);
            }
            else if (Timer <= RiseEnd) {
                UpdateRise(context);
            }
            else if (Timer <= CondenseEnd) {
                UpdateCondense(context);
            }
            else if (Timer <= StillEnd) {
                UpdateCrownAndStill(context);
            }
            else if (Timer >= IntroEnd) {
                return Finish(context);
            }

            return null;
        }

        /// <summary>幕一：四方凝胶细流汇向舞台点，池面渐胀</summary>
        private void UpdateGather(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            context.HideBodySprite = true;
            float t = Timer / (float)GatherEnd;

            if (VaultUtils.isServer) {
                return;
            }

            //汇流凝胶珠：从四周飞向池心
            int streams = 1 + (int)(t * 3f);
            for (int i = 0; i < streams; i++) {
                if (!Main.rand.NextBool(2)) {
                    continue;
                }
                Vector2 from = stagePoint + new Vector2(Main.rand.NextFloat(-460f, 460f), Main.rand.NextFloat(-30f, 6f));
                Vector2 vel = (stagePoint - from).SafeNormalize(Vector2.UnitX) * Main.rand.NextFloat(4f, 9f);
                vel.Y -= Main.rand.NextFloat(0.5f, 2f);
                PRTLoader.NewParticle<PRT_BKSGelBead>(from, vel,
                    Color.Lerp(KingSlimeGelFX.GelMid, KingSlimeGelFX.GelDeep, Main.rand.NextFloat()) * 0.8f,
                    Main.rand.NextFloat(0.6f, 1.2f))?.Configure(Main.rand.Next(24, 40), 0.12f, 0.997f);
            }
            //池面冒泡与涟漪声
            if (Timer % 9 == 0) {
                KingSlimeGelFX.BubbleFizz(stagePoint - new Vector2(0f, 6f), 60f * t + 14f, 2);
            }
            if (Timer % 26 == 0) {
                SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = -0.3f + t * 0.3f, Volume = 0.4f + t * 0.3f, MaxInstances = 3 }, stagePoint);
            }
            //地面隆隆渐强
            if (Timer % 16 == 0) {
                KingSlimeGelFX.CameraPunch(stagePoint, 0.8f + t * 2f, 12, "BKSIntroRumble");
            }
        }

        /// <summary>幕二：凝胶塔拔地而起(身体以塔形显形)</summary>
        private void UpdateRise(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            float t = (Timer - GatherEnd) / (float)(RiseEnd - GatherEnd);

            //破面帧
            if (Timer == GatherEnd + 1) {
                SoundEngine.PlaySound(SoundID.Splash with { Pitch = -0.5f, Volume = 1.1f }, stagePoint);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.75f, Volume = 0.65f }, stagePoint);
                KingSlimeGelFX.LandingBurst(stagePoint, 15f, 1.3f);
                KingSlimeGelFX.CameraPunch(stagePoint, 6f, 16, "BKSIntroRise", -Vector2.UnitY);
                npc.Bottom = stagePoint;
                npc.netUpdate = true;
            }

            context.HideBodySprite = false;
            context.BodyOpacity = MathHelper.Clamp(t * 2.2f, 0.25f, 1f);
            npc.Bottom = stagePoint;

            //塔形：弹性过冲拔高
            float overshoot = 1f + 0.22f * MathF.Sin(MathHelper.Clamp(t * 1.25f, 0f, 1f) * MathHelper.Pi);
            context.VisualSquash = MathHelper.Lerp(context.VisualSquash, 1.75f * overshoot, 0.22f);
            context.AuraMode = 1;
            context.AuraProgress = t;

            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                //塔身垂流
                Vector2 pos = npc.Bottom - new Vector2(Main.rand.NextFloat(-0.4f, 0.4f) * npc.width, Main.rand.NextFloat(0.4f, 1.6f) * npc.height);
                PRTLoader.NewParticle<PRT_BKSGelBead>(pos, new Vector2(0f, Main.rand.NextFloat(1f, 3f)),
                    KingSlimeGelFX.GelMid * 0.7f, Main.rand.NextFloat(0.5f, 1f))?.Configure(20);
            }
        }

        /// <summary>幕三：塔身坍缩凝成王体</summary>
        private void UpdateCondense(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            npc.Bottom = stagePoint;
            context.BodyOpacity = 1f;

            if (Timer == RiseEnd + 1) {
                //塌缩拍：猛地压回
                context.SquashVelocity -= 0.55f;
                KingSlimeGelFX.SquishSound(npc.Center, -0.4f, 1f);
                KingSlimeGelFX.LandingBurst(npc.Bottom, 10f, 1.1f);
            }
        }

        /// <summary>幕四：王冠天降扣顶+静止威压</summary>
        private void UpdateCrownAndStill(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            npc.Bottom = stagePoint;

            //王冠坠落(渲染层按进度绘制)：加速下落，命中帧敲响
            if (Timer > CondenseEnd && Timer <= CrownHitFrame) {
                context.IntroCrownDrop = (Timer - CondenseEnd) / (float)(CrownHitFrame - CondenseEnd);
            }
            if (Timer == CrownHitFrame) {
                KingSlimeGelFX.CrownChime(npc.Top, -0.05f, 1.1f);
                KingSlimeGelFX.GoldGlint(npc.Top + new Vector2(0f, -10f), 20, 7f);
                KingSlimeGelFX.CameraPunch(npc.Top, 3.5f, 10, "BKSIntroCrown", Vector2.UnitY);
                //加冕砸扣：凝胶受压微陷+扣冠回弹
                context.CrownMountImpact(0.18f);
            }

            //静止亮相：只有呼吸(菜单式威压)
            if (Timer > CrownHitFrame && Timer == StillEnd - 10) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.35f, Volume = 1.05f }, npc.Center);
                SoundEngine.PlaySound(SoundID.QueenSlime with { Pitch = -0.6f, Volume = 0.9f }, npc.Center);
            }
        }

        private IKingSlimeState Finish(KingSlimeStateContext context) {
            context.Npc.dontTakeDamage = false;
            if (!VaultUtils.isClient) {
                return new KingSlimeHopState(context.IsAsuraMode ? 1 : 2);
            }
            return null;
        }

        public override void OnExit(KingSlimeStateContext context) {
            base.OnExit(context);
            context.Npc.dontTakeDamage = false;
            context.Npc.damage = context.Npc.defDamage;
        }
    }
}
