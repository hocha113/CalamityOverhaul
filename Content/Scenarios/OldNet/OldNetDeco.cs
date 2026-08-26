using CalamityOverhaul.Content.Scenarios.OldNet.Backgrounds;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.OldNet
{
    /// <summary>
    /// 旧网环境装饰：上浮数据尘（PRT_CyberMote 的旧网变体，烬红衰败色板 +
    /// ~4% 冷青幸存残光，密度随带内腐化上量）；黑墙涌动期自西向东加放红烬横波。
    /// client-only，由 <see cref="Backgrounds.OldNetAmbience"/> 每帧驱动
    /// </summary>
    internal static class OldNetDeco
    {
        public static void Update() {
            if (Main.gameMenu) {
                return;
            }
            float presence = OldNetAmbience.Presence;
            if (presence < 0.25f) {
                return;
            }
            int playerCol = (int)(Main.LocalPlayer.Center.X / 16f);
            float corrupt = OldNetMetrics.CorruptionAt(playerCol);

            //基础上浮尘：密度随腐化 1/12 → 1/5，稳态屏内约 20~45 粒
            int rate = (int)MathHelper.Lerp(12f, 5f, corrupt);
            if (Main.rand.NextBool(Math.Max(rate, 1))) {
                float x = Main.screenPosition.X + Main.rand.NextFloat(Main.screenWidth);
                float y = Main.screenPosition.Y + Main.screenHeight + Main.rand.NextFloat(20f, 80f);
                Vector2 vel = new(0f, -Main.rand.NextFloat(0.6f, 1.5f));
                //烬红为主，~4% 冷青幸存残光（与天幕余烬同语汇）
                Color core = Main.rand.NextBool(25)
                    ? new Color(66, 160, 170) : new Color(205, 62, 34);
                bool horiz = false;

                //⑧ 深层剖面：纵深 0.4 起上浮尘渐次替换为顶棚沉降尘（协议沉积物），
                //出生于屏幕上缘缓慢下沉，颜色压暗三成；比例随纵深过渡，总量不变（池预算零净增）
                float depthT = OldNetLinkFX.Depth01;
                if (depthT > 0.4f && Main.rand.NextFloat() < (depthT - 0.4f) / 0.6f) {
                    y = Main.screenPosition.Y - Main.rand.NextFloat(20f, 60f);
                    vel = new Vector2(0f, Main.rand.NextFloat(0.3f, 0.7f));
                    core = Main.rand.NextBool(25)
                        ? new Color(46, 112, 119) : new Color(144, 43, 24);
                }

                //③ 网的注视 T4：数据尘整体转向黑墙奔流（网在回收关于你的一切）。
                //watch [3.5,4] 淡入：三成改横尘西向奔流（涌动波配色），其余获得常驻西向偏置
                //（偏置须经 Configure 传进 PRT——AI 的蛇行是赋值不是叠加，出生速度首帧即被覆写）
                float watch = OldNetLinkFX.Watch;
                float westBias = 0f;
                if (watch >= 3.5f) {
                    float wf = MathHelper.Clamp((watch - 3.5f) / 0.5f, 0f, 1f);
                    if (Main.rand.NextBool(3)) {
                        horiz = true;
                        vel = new Vector2(-(1.2f + 2.4f * wf), Main.rand.NextFloat(-0.3f, 0.3f));
                        core = new Color(235, 70, 36);
                        y = Main.screenPosition.Y + Main.rand.NextFloat(Main.screenHeight);
                    }
                    else {
                        westBias = -Main.rand.NextFloat(0.4f, 1.2f) * wf;
                        vel.X += westBias;
                    }
                }

                PRTLoader.NewParticle<PRT_OldNetMote>(new Vector2(x, y), vel, core,
                    Main.rand.NextFloat(0.6f, 1.2f))
                    ?.Configure(Main.rand.Next(150, 240), horizontal: horiz, westBias: westBias);
            }

            //④ 底噪虹吸流：越过安全带后脚踝高度向西奔流的暗红信号丝（被抽走的信号）。
            //生成频率按玩家列的耗速 d；落点定色定速按生成列自身的 d——安全带列 dLoc=0 直接跳过，
            //渐层零点真为零，玩家用余光读出"这里的每一秒都在扣费"
            float drainD = MathHelper.Clamp(
                OldNetMetrics.DrainPerSecondAt(playerCol) / 0.5f, 0f, 1f);
            if (drainD > 0f && Main.rand.NextBool(Math.Max((int)(18f - 12f * drainD), 1))) {
                float sx = Main.screenPosition.X + Main.rand.NextFloat(Main.screenWidth);
                float dLoc = MathHelper.Clamp(
                    OldNetMetrics.DrainPerSecondAt((int)(sx / 16f)) / 0.5f, 0f, 1f);
                if (dLoc > 0f) {
                    float sy = Main.LocalPlayer.Bottom.Y + Main.rand.NextFloat(-40f, 40f);
                    float speed = 0.8f + 2.2f * dLoc;
                    //时停考古：连虹吸都慢下来（纯表现，与噪音×0.25 的时停语义呼应，不碰实际扣费）
                    if (WorldFreezeSystem.IsActive) {
                        speed *= 0.5f;
                    }
                    Color siphonCol = Color.Lerp(new Color(120, 36, 26), new Color(205, 62, 34), dLoc);
                    PRTLoader.NewParticle<PRT_OldNetMote>(new Vector2(sx, sy),
                        new Vector2(-speed, 0f), siphonCol, Main.rand.NextFloat(0.5f, 0.9f))
                        ?.Configure(Main.rand.Next(90, 150), horizontal: true, siphon: true);
                }
            }

            //黑墙涌动：自西向东的红烬横波（墙在向旧网深处呼气）；
            //⑥ 大潮吸气期横波停发（尘被吸向墙，不该同时向东喷）
            float surge = OldNetSkyEvents.Surge;
            if (surge > 0.25f && OldNetSkyEvents.TideSuck < 0.05f && Main.rand.NextBool(3)) {
                float x = Main.screenPosition.X - Main.rand.NextFloat(30f, 90f);
                float y = Main.screenPosition.Y + Main.rand.NextFloat(Main.screenHeight);
                Vector2 vel = new(1.2f + Main.rand.NextFloat(2.2f, 4.5f) * surge,
                    Main.rand.NextFloat(-0.35f, 0.35f));
                PRTLoader.NewParticle<PRT_OldNetMote>(new Vector2(x, y), vel,
                    new Color(235, 70, 36), Main.rand.NextFloat(0.7f, 1.3f))
                    ?.Configure(Main.rand.Next(90, 150), horizontal: true);
            }

            //⑥ 大潮退潮：头 3s 尘以涌动横波姿态自西向东"呼出"一轮烬红（复用涌动波块形制）
            float exhale = OldNetSkyEvents.TideExhale;
            if (exhale > 0.05f && Main.rand.NextBool(3)) {
                float x = Main.screenPosition.X - Main.rand.NextFloat(30f, 90f);
                float y = Main.screenPosition.Y + Main.rand.NextFloat(Main.screenHeight);
                Vector2 vel = new(1.2f + Main.rand.NextFloat(2.2f, 4.5f) * exhale,
                    Main.rand.NextFloat(-0.35f, 0.35f));
                PRTLoader.NewParticle<PRT_OldNetMote>(new Vector2(x, y), vel,
                    new Color(235, 70, 36), Main.rand.NextFloat(0.7f, 1.3f))
                    ?.Configure(Main.rand.Next(90, 150), horizontal: true);
            }
        }
    }

    /// <summary>旧网数据尘：速度拉伸微光条；竖尘缓升蛇行，横尘（涌动波）直线掠过</summary>
    internal class PRT_OldNetMote : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override int InGame_World_MaxCount => 150;
        public override bool CanPool => true;

        private float drift;
        private float driftPhase;
        private bool horizontal;
        //④ 虹吸丝流模式：西向流动，进入安全带列提前渐灭（安全带内绝对干净）
        private bool siphon;
        private float safeFade;
        //③ T4 竖尘常驻西向偏置：AI 蛇行是赋值不是叠加，偏置必须走字段逐帧叠进去
        private float westBias;

        public PRT_OldNetMote Configure(int lifeTime, bool horizontal = false,
            bool siphon = false, float westBias = 0f) {
            Lifetime = lifeTime;
            this.horizontal = horizontal;
            this.siphon = siphon;
            this.westBias = westBias;
            safeFade = 1f;
            drift = Main.rand.NextFloat(0.5f, 1.3f);
            driftPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void Reset() {
            base.Reset();
            drift = 0f;
            driftPhase = 0f;
            horizontal = false;
            siphon = false;
            safeFade = 1f;
            westBias = 0f;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            //竖尘：缓慢上浮 + 横向蛇行；横尘：保持冲势 + 纵向轻摆
            if (horizontal) {
                Velocity.Y = MathF.Sin(Time * 0.03f + driftPhase) * 0.18f * drift;
            }
            else {
                //⑥ 大潮吸气：全部竖尘获得西向加速度倒卷向墙（蛇行让位）；潮息即恢复
                float suck = OldNetSkyEvents.TideSuck;
                if (suck > 0.01f) {
                    Velocity.X = MathHelper.Clamp(
                        Velocity.X - 0.055f * suck, -(0.2f + 2.6f * suck), 0f);
                }
                else {
                    //③ T4 回流：蛇行之上叠加常驻西向偏置（叠加不覆写，二审实锤修复）
                    Velocity.X = MathF.Sin(Time * 0.02f + driftPhase) * 0.22f * drift + westBias;
                }
            }

            //虹吸丝流：一旦流进安全带列（耗速为零处）就平滑渐灭，近墙侧不残留
            if (siphon) {
                bool inSafe = OldNetMetrics.DrainPerSecondAt((int)(Position.X / 16f)) <= 0f;
                safeFade = MathHelper.Clamp(safeFade + (inSafe ? -0.09f : 0.02f), 0f, 1f);
            }

            float life = LifetimeCompletion;
            float fadeIn = MathHelper.Clamp(Time / 30f, 0f, 1f);
            float fadeOut = 1f - MathHelper.Clamp((life - 0.75f) / 0.25f, 0f, 1f);
            Opacity = fadeIn * fadeOut * 0.85f * (siphon ? safeFade : 1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Opacity < 0.02f) {
                return false;
            }
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 drawPos = Position - Main.screenPosition;

            //沿速度方向拉伸的微光条
            float speed = Velocity.Length();
            float len = (7f + speed * 6f) * Scale;
            float wid = 1.3f * Scale;
            float rot = Velocity.ToRotation();
            Vector2 origin = new(0.5f, 0.5f);
            Rectangle src = new(0, 0, 1, 1);

            Color outer = Color * (Opacity * 0.35f);
            spriteBatch.Draw(pixel, drawPos, src, outer, rot,
                origin, new Vector2(len * 1.3f, wid * 2.6f), SpriteEffects.None, 0f);

            Color inner = Color * Opacity;
            spriteBatch.Draw(pixel, drawPos, src, inner, rot,
                origin, new Vector2(len, wid), SpriteEffects.None, 0f);

            return false;
        }
    }
}
