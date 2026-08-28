using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams
{
    /// <summary>
    /// 鬼梦贴地雾带：<see cref="KikasaDreamGroundField"/> 供带符号离地距离场，
    /// KikasaDreamFog.fx 逐像素采样定密度（连续噪声场着雾，无生灭即无闪烁）。
    /// 旧的逐列探地三角带在复杂地形（陡坡/多层洞穴/悬空岛）会把雾带钉在玩家视线高度
    /// 横贯岩面，已废除；距离场对任意地形逐像素贴合。
    /// 驱散源经 <see cref="KikasaDreamFogField"/> 喂 uniform，玩家/光标/恶犬处雾让位。
    /// 由 <see cref="KikasaDomains.KikasaDomainRender"/> 的 EndEntityDraw 驱动，仅梦侧可视时绘制
    /// </summary>
    internal static class KikasaDreamFogRender
    {
        /// <summary>与着色器 uRepulse[6] 对齐的槽位数</summary>
        private const int RepulseSlots = 6;

        //窗口 quad 4 顶点 + 驱散槽上载缓冲，逐帧复用零分配
        private static readonly VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
        private static readonly Vector4[] repulseUpload = new Vector4[RepulseSlots];

        internal static void Draw(SpriteBatch spriteBatch) {
            KikasaDomainPlayer kdp = KikasaDomain.Viewed;
            if (kdp == null || !kdp.DreamWorldVisual || kdp.DreamBlend <= 0.01f) {
                return;
            }
            Player viewer = Main.LocalPlayer;
            if (viewer?.active != true) {
                return;
            }
            Effect fx = EffectLoader.KikasaDreamFog?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                //雾是纯氛围件，着色器缺失直接没有，不做灰度贴片回退
                return;
            }

            //距离场逐 tick 全量重建（同 tick 幂等；内部 SetData 前清 s1/s2，须在绑定之前调）
            KikasaDreamGroundField.Update();
            if (!KikasaDreamGroundField.Ready) {
                return;
            }

            //quad 跨度：距离场窗口 ∩ 梦界圆（与物品封禁/禁弹同一半径口径）
            Point origin = KikasaDreamGroundField.OriginTile;
            float winLeft = origin.X * 16f;
            float winTop = origin.Y * 16f;
            float winRight = (origin.X + KikasaDreamGroundField.WindowW) * 16f;
            float winBottom = (origin.Y + KikasaDreamGroundField.WindowH) * 16f;
            float casterX = kdp.Player.Center.X;
            float left = MathF.Max(winLeft, casterX - KikasaDream.WorldRange);
            float right = MathF.Min(winRight, casterX + KikasaDream.WorldRange);
            if (right - left < 16f) {
                return;
            }
            BuildQuad(left, right, winTop, winBottom);

            fx.CurrentTechnique = fx.Techniques["TechGroundFog"];
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(kdp.EffectTime);
            //风向与雨帘同源定相，量级压低：梦里的死风，雾贴地缓行
            fx.Parameters["uWind"]?.SetValue(MathF.Sin(Main.worldID % 255 * 0.37f) * 16f);
            fx.Parameters["uAlpha"]?.SetValue(kdp.DreamBlend);
            //距离场窗口 UV 映射（KiyumeFog 的 uFogOrigin/uFogUvMul/uFogUvClamp 同式）
            fx.Parameters["uFieldOrigin"]?.SetValue(new Vector2(winLeft, winTop));
            fx.Parameters["uFieldUvMul"]?.SetValue(new Vector2(
                1f / (KikasaDreamGroundField.CapW * KikasaDreamGroundField.CellPx),
                1f / (KikasaDreamGroundField.CapH * KikasaDreamGroundField.CellPx)));
            fx.Parameters["uFieldUvClamp"]?.SetValue(new Vector4(
                0.5f / KikasaDreamGroundField.CapW,
                0.5f / KikasaDreamGroundField.CapH,
                (KikasaDreamGroundField.WindowW - 0.5f) / KikasaDreamGroundField.CapW,
                (KikasaDreamGroundField.WindowH - 0.5f) / KikasaDreamGroundField.CapH));
            FillRepulse();
            fx.Parameters["uRepulse"]?.SetValue(repulseUpload);

            //EndEntityDraw 入口批未开启（同 KikasaWispFX 之例），只动设备态并画完还原
            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;
            device.Textures[2] = KikasaDreamGroundField.Texture;
            device.SamplerStates[2] = SamplerState.LinearClamp;

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
            //归还纹理槽：同帧邻居泄漏纪律
            device.Textures[1] = null;
            device.Textures[2] = null;
        }

        //顶点契约与 KikasaDreamFog.fx 对齐：POSITION=世界坐标（VS 过 transformMatrix），
        //密度全部由 PS 采距离场推导，COLOR0/TEXCOORD0 不再承载数据
        private static void BuildQuad(float left, float right, float top, float bottom) {
            verts[0] = new VertexPositionColorTexture(new Vector3(left, top, 0f), Color.White, Vector2.Zero);
            verts[1] = new VertexPositionColorTexture(new Vector3(right, top, 0f), Color.White, Vector2.Zero);
            verts[2] = new VertexPositionColorTexture(new Vector3(left, bottom, 0f), Color.White, Vector2.Zero);
            verts[3] = new VertexPositionColorTexture(new Vector3(right, bottom, 0f), Color.White, Vector2.Zero);
        }

        private static void FillRepulse() {
            var repulsors = KikasaDreamFogField.Repulsors;
            for (int i = 0; i < RepulseSlots; i++) {
                repulseUpload[i] = i < repulsors.Count ? repulsors[i] : Vector4.Zero;
            }
        }
    }
}
