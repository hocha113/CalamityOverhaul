using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.Onikiris.CrimsonRendSlashs;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.Onikiris.OniAnnihilates
{
    /// <summary>
    /// 蓄力领域渲染：世界锚定压扁 quad 交给 <see cref="EffectLoader.OniAnnihilateField"/>。<br/>
    /// 透视压扁由几何完成（HalfY = HalfX × 压扁率，与立体环斩共用同一"透视语言"），
    /// shader 在地平面圆坐标里作画；前后半分两 pass 提交 —— 后半 + 升腾墨流走玩家身前
    /// 回调层（画在身后），前半走实体扩展层（盖住脚面），立体感来自真实遮挡。<br/>
    /// 调色板与绯红裂空斩共享（<see cref="CrimsonSlashRenderer"/> 四色）
    /// </summary>
    internal static class OniAnnihilateFieldRenderer
    {
        /// <summary>领域压扁率：与 OniFinaleRing 的 squash 区间一致的透视语言</summary>
        public const float Squash = 0.34f;

        /// <summary>领域单帧动态量（由主控时间轴合成）</summary>
        public struct FieldState
        {
            public float Expand;     //0..1 展开
            public float Drain;      //0..1 收束抽干
            public float Pulse;      //0..1 脉冲闪
            public float Charge;     //0..1 蓄力总进度
            public float FlowTime;   //外部积分的流动时间（死寂减速）
            public float Opacity;
            public float Seed;
        }

        /// <summary>升腾墨流子带定义：多股不同宽高/种子/强度的柱状流，层间视差</summary>
        public struct StreamDef
        {
            public float OffsetX;      //相对领域中心的横向偏移(px)
            public float Width;        //quad 宽(px)
            public float Height;       //quad 高(px)
            public float SeedOffset;   //噪声相位偏移
            public float IntensityMul; //相对整体的强度
        }

        /// <summary>设备状态 + 帧级公共 uniform；返回 false 表示资产未就绪</summary>
        public static bool BeginDraw(GraphicsDevice device, out Effect fx
            , out BlendState prevBlend, out RasterizerState prevRaster, out DepthStencilState prevDepth) {
            fx = EffectLoader.OniAnnihilateField?.Value;
            Texture2D noise = OnikiriAssets.NoiseSoft01?.Value;
            prevBlend = device.BlendState;
            prevRaster = device.RasterizerState;
            prevDepth = device.DepthStencilState;
            if (fx == null || noise == null) {
                return false;
            }

            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uColHot"]?.SetValue(CrimsonSlashRenderer.ColHot);
            fx.Parameters["uColBright"]?.SetValue(CrimsonSlashRenderer.ColBright);
            fx.Parameters["uColDeep"]?.SetValue(CrimsonSlashRenderer.ColDeep);
            fx.Parameters["uColDark"]?.SetValue(CrimsonSlashRenderer.ColDark);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            return true;
        }

        public static void EndDraw(GraphicsDevice device
            , BlendState prevBlend, RasterizerState prevRaster, DepthStencilState prevDepth) {
            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;
        }

        /// <summary>逐帧动态 uniform（两 technique 共用字段一次写入）</summary>
        private static void ApplyState(Effect fx, in FieldState s) {
            fx.Parameters["uFlowTime"]?.SetValue(s.FlowTime);
            fx.Parameters["uOpacity"]?.SetValue(MathHelper.Clamp(s.Opacity, 0f, 1f));
            fx.Parameters["uExpand"]?.SetValue(MathHelper.Clamp(s.Expand, 0f, 1f));
            fx.Parameters["uDrain"]?.SetValue(MathHelper.Clamp(s.Drain, 0f, 1f));
            fx.Parameters["uPulse"]?.SetValue(MathHelper.Clamp(s.Pulse, 0f, 1.2f));
            fx.Parameters["uCharge"]?.SetValue(MathHelper.Clamp(s.Charge, 0f, 1f));
        }

        /// <summary>
        /// 领域椭圆一侧：halfSel=+1 前半（实体层，盖住脚面）、-1 后半（玩家身后）。<br/>
        /// center 为椭圆中心（脚底），halfX 半长轴(px)，压扁率固定 <see cref="Squash"/>
        /// </summary>
        public static void DrawField(GraphicsDevice device, Effect fx
            , Vector2 center, float halfX, in FieldState s, float halfSel) {
            fx.CurrentTechnique = fx.Techniques["FieldTech"];
            ApplyState(fx, in s);
            fx.Parameters["uSeed"]?.SetValue(s.Seed);
            fx.Parameters["uHalfSel"]?.SetValue(halfSel);

            float halfY = halfX * Squash;
            SubmitQuad(device, fx
                , center - new Vector2(halfX, halfY)
                , center + new Vector2(halfX, halfY));
        }

        /// <summary>升腾墨流子带：bottomCenter 为流带根部（领域后半内），向上立起</summary>
        public static void DrawStream(GraphicsDevice device, Effect fx
            , Vector2 bottomCenter, in StreamDef def, in FieldState s, float intensity) {
            fx.CurrentTechnique = fx.Techniques["FlowTech"];
            ApplyState(fx, in s);
            fx.Parameters["uSeed"]?.SetValue(s.Seed + def.SeedOffset);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity * def.IntensityMul, 0f, 1.2f));

            Vector2 root = bottomCenter + new Vector2(def.OffsetX, 0f);
            SubmitQuad(device, fx
                , root + new Vector2(-def.Width * 0.5f, -def.Height)
                , root + new Vector2(def.Width * 0.5f, 0f));
        }

        /// <summary>轴对齐 quad：uv (0,0)=左上 (1,1)=右下（shader 里 uv.y=1 为前缘/底部）</summary>
        private static void SubmitQuad(GraphicsDevice device, Effect fx, Vector2 topLeft, Vector2 bottomRight) {
            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture(topLeft.ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture(new Vector2(bottomRight.X, topLeft.Y).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture(new Vector2(topLeft.X, bottomRight.Y).ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture(bottomRight.ToVector3(), Color.White, new Vector2(1f, 1f));

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }
        }
    }
}
