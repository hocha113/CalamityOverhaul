using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.MurasamaProj
{
    /// <summary>
    /// 村正次元斩终结技渲染系统
    /// 负责碎屏扭曲、滤镜、径向模糊等后处理效果
    /// </summary>
    internal class MuraSlayAllRender : RenderHandle
    {
        /// <summary>
        /// 扭曲效果强度
        /// </summary>
        public float TwistStrength { get; set; } = 0f;

        /// <summary>
        /// 是否应该进行扭曲
        /// </summary>
        public bool ShouldTwist { get; set; }

        //碎片数据
        private readonly Vector2[] piecesPos = new Vector2[24];
        private readonly float[] piecesRotate = new float[24];
        private readonly float[] piecesScale = new float[24];
        private readonly Color[] piecesColor = new Color[24];

        /// <summary>
        /// 初始化碎片数据
        /// </summary>
        public void InitializePieces() {
            for (int i = 0; i < piecesPos.Length; i++) {
                piecesPos[i] = Main.LocalPlayer.Center + Main.rand.NextVector2Circular(Main.screenWidth / 2.4f, Main.screenHeight / 2);
                piecesRotate[i] = Main.rand.NextFloat(0, MathHelper.TwoPi);
                piecesScale[i] = Main.rand.NextFloat(1f, 3f);
                //颜色控制碎屏方向，红色通道是旋转，绿色通道是强度
                piecesColor[i] = new Color(Main.rand.Next(40, 256), Main.rand.Next(40, 256), 0);
            }
        }

        /// <summary>
        /// 查找当前活跃的次元斩弹幕
        /// </summary>
        private MuraDimensionSlash FindActiveDimensionSlash() {
            foreach (Projectile proj in Main.projectile) {
                if (!proj.active) continue;
                if (proj.type != ModContent.ProjectileType<MuraDimensionSlash>()) continue;
                if (proj.ModProjectile is MuraDimensionSlash slash) {
                    return slash;
                }
            }
            return null;
        }

        public override void EndCaptureDraw(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            //查找活跃的次元斩弹幕
            MuraDimensionSlash activeSlash = FindActiveDimensionSlash();

            //如果没有活跃的次元斩弹幕，直接调用原方法
            if (activeSlash == null) {
                return;
            }

            //第一阶段:应用滤镜和径向模糊到实时画面
            ApplyFirstPassEffects(graphicsDevice, spriteBatch, activeSlash);

            //第二阶段:绘制碎屏扭曲效果
            DrawBrokenScreenEffect(graphicsDevice, spriteBatch, screenSwap, activeSlash);

            //第三阶段:最终合成
            DrawFinalComposite(graphicsDevice, spriteBatch, screenSwap, activeSlash);
        }

        /// <summary>
        /// 第一阶段:应用滤镜和径向模糊效果到实时画面
        /// </summary>
        private void ApplyFirstPassEffects(GraphicsDevice gd, SpriteBatch sb, MuraDimensionSlash slash) {
            //将当前实时屏幕内容复制到screenTargetSwap并应用shader效果
            gd.SetRenderTarget(Main.screenTargetSwap);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

            //应用滤镜（屏幕反色/暗红调效果）
            if (slash.FilterIntensity >= 2) {
                MuraSlayAllAssets.Filter.Parameters["filterRGB"].SetValue(new Vector3(0.1f, 0f, 0.05f));
                MuraSlayAllAssets.Filter.CurrentTechnique.Passes[0].Apply();
            }

            //径向模糊（能量汇聚/爆发效果）
            if (slash.RadialBlurStrength > 0) {
                Vector2 offset = new Vector2(0.5f, 0.5f);
                Projectile p1 = null;
                foreach (var p in Main.projectile) {
                    if (p.active && p.type == ModContent.ProjectileType<EndSkillEffectStart>()) {
                        p1 = p;
                    }
                }
                if (p1 != null) {
                    var p1Pos = p1.Center - Main.screenPosition;
                    offset = new Vector2(p1Pos.X / Main.screenWidth, p1Pos.Y / Main.screenHeight);
                }
                MuraSlayAllAssets.RadialBlur.Parameters["center"].SetValue(offset);
                MuraSlayAllAssets.RadialBlur.Parameters["strength"].SetValue(slash.RadialBlurStrength / 2f);
                MuraSlayAllAssets.RadialBlur.CurrentTechnique.Passes[0].Apply();
            }

            //绘制当前实时屏幕内容
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();
        }

        /// <summary>
        /// 第二阶段:绘制碎屏扭曲效果（空间被切割的视觉表现）
        /// </summary>
        private void DrawBrokenScreenEffect(GraphicsDevice gd, SpriteBatch sb, RenderTarget2D screenSwap, MuraDimensionSlash slash) {
            gd.SetRenderTarget(screenSwap);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap
                , DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.Transform);

            ShouldTwist = false;

            if (slash.BrokenScreenState != 3) {
                if (slash.BrokenScreenState == 0) {
                    InitializePieces();
                    slash.BrokenScreenState = 1;
                }
                //绘制碎片（扭曲采样点）
                for (int i = 0; i < piecesPos.Length; i++) {
                    sb.Draw(MuraSlayAllAssets.Triangle, piecesPos[i] - Main.screenPosition, null
                        , piecesColor[i], piecesRotate[i], MuraSlayAllAssets.Triangle.Size() / 2f
                        , piecesScale[i], SpriteEffects.None, 0);
                    //碎屏收缩阶段
                    if (slash.BrokenScreenState == 2) {
                        piecesScale[i] = Math.Max(piecesScale[i] - 0.17f, 0f);
                    }
                }
                ShouldTwist = true;
                TwistStrength = 0.04f;
            }

            sb.End();
        }

        /// <summary>
        /// 第三阶段:最终合成，保持画面实时更新
        /// </summary>
        private void DrawFinalComposite(GraphicsDevice gd, SpriteBatch sb, RenderTarget2D screenSwap, MuraDimensionSlash slash) {
            gd.SetRenderTarget(Main.screenTarget);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

            //应用扭曲shader（碎屏扭曲效果）
            if (ShouldTwist) {
                EffectLoader.PowerSFShader.Value.Parameters["tex0"].SetValue(screenSwap);
                EffectLoader.PowerSFShader.Value.Parameters["i"].SetValue(TwistStrength);
                EffectLoader.PowerSFShader.Value.CurrentTechnique.Passes[0].Apply();
            }

            //RGB色差效果（爆发瞬间的视觉冲击）
            if (slash.ColorSeparation > 0) {
                MuraSlayAllAssets.ScreenColorMess.Parameters["offsetStrength"].SetValue(slash.ColorSeparation);
                MuraSlayAllAssets.ScreenColorMess.CurrentTechnique.Passes[0].Apply();
            }

            //绘制处理后的实时画面
            sb.Draw(Main.screenTargetSwap, Vector2.Zero, Color.White);
            sb.End();
        }
    }
}
