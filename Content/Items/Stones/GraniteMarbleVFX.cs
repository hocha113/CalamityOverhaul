using CalamityOverhaul.Common;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Items.Stones
{
    /// <summary>
    /// 花岗/大理石系列共用VFX枢纽：主题色常量、专属渐变条、shader参数装配与投射物拖尾样板
    /// </summary>
    internal class GraniteMarbleVFX : ICWRLoader
    {
        //资源所在目录（贴图与 .cs 同放在 Content 下，默认自动加载）
        public const string GraniteTex = "CalamityOverhaul/Content/Items/Stones/Granites/";
        public const string MarbleTex = "CalamityOverhaul/Content/Items/Stones/Marbles/";

        //主题色
        public static readonly Color GraniteCore = new Color(120, 185, 255);
        public static readonly Color GraniteDeep = new Color(70, 120, 220);
        public static readonly Color GraniteSpark = new Color(150, 210, 255);
        public static readonly Color MarbleCore = new Color(255, 247, 220);
        public static readonly Color MarbleGold = new Color(228, 196, 120);
        public static readonly Color MarbleDust = new Color(214, 210, 196);

        //专属渐变条：运行时生成 64×5 横向渐变（对齐 ColorBar 资源规格），懒加载、卸载时释放
        private static Texture2D graniteBar;
        private static Texture2D marbleBar;

        /// <summary>花岗渐变条：x=0 深蓝 → 青 → x=1 白蓝（GradientTrail 沿 x 采样）</summary>
        public static Texture2D GraniteBar => graniteBar ??= BuildGradientBar(
            new Color(45, 75, 190), new Color(90, 200, 255), new Color(205, 240, 255));

        /// <summary>大理石渐变条：x=0 暖白 → 鎏金 → x=1 白（GradientTrail 沿 x 采样）</summary>
        public static Texture2D MarbleBar => marbleBar ??= BuildGradientBar(
            new Color(255, 238, 208), new Color(222, 178, 98), new Color(255, 255, 252));

        //仅在绘制路径（客户端主线程）懒调用，多段色标间平滑插值
        private static Texture2D BuildGradientBar(params Color[] stops) {
            const int width = 64, height = 5;
            var tex = new Texture2D(Main.instance.GraphicsDevice, width, height);
            Color[] data = new Color[width * height];
            for (int x = 0; x < width; x++) {
                float f = x / (width - 1f) * (stops.Length - 1);
                int i = Math.Min((int)f, stops.Length - 2);
                Color c = Color.Lerp(stops[i], stops[i + 1], f - i);
                for (int y = 0; y < height; y++) {
                    data[y * width + x] = c;
                }
            }
            tex.SetData(data);
            return tex;
        }

        void ICWRLoader.UnLoadData() {
            //GPU 资源回主线程释放；先摘引用再调度，避免闭包读到已置空的字段
            Texture2D g = graniteBar, m = marbleBar;
            graniteBar = marbleBar = null;
            if (g != null || m != null) {
                Main.RunOnMainThread(() => {
                    g?.Dispose();
                    m?.Dispose();
                });
            }
        }

        /// <summary>
        /// 脚下着地探测：沿重力方向 2px 的 TileCollision 探针，坐骑上视为未着地
        /// </summary>
        public static bool IsGrounded(Player player) {
            if (player.mount.Active) {
                return false;
            }
            Vector2 probeVelocity = Vector2.UnitY * player.gravDir * 2f;
            Vector2 constrained = Collision.TileCollision(player.position, probeVelocity
                , player.width, player.height, false, false, (int)player.gravDir);
            return constrained.Y != probeVelocity.Y;
        }

        /// <summary>GradientTrail 标准参数，调用方设 BlendState 后 DrawTrail</summary>
        public static void ApplyGradientTrail(Effect effect, Texture2D gradientBar, Texture2D baseImage) {
            effect.Parameters["transformMatrix"].SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"].SetValue((float)Main.timeForVisualEffects * 0.08f);
            effect.Parameters["uTimeG"].SetValue(Main.GlobalTimeWrappedHourly * 0.2f);
            effect.Parameters["udissolveS"].SetValue(1f);
            effect.Parameters["uBaseImage"].SetValue(baseImage);
            effect.Parameters["uFlow"].SetValue(CWRAsset.Airflow.Value);
            effect.Parameters["uGradient"].SetValue(gradientBar);
            effect.Parameters["uDissolve"].SetValue(CWRAsset.Extra_193.Value);
        }

        /// <summary>
        /// GraniteArc 标准参数（青蓝电弧带）；带内 uv.x=0 为最新端（oldPos[0] 侧），x=1 为尾端
        /// </summary>
        public static void ApplyGraniteArc(Effect effect, float fade = 1f) {
            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uFade"]?.SetValue(fade);
            effect.Parameters["uNoiseTex"]?.SetValue(CWRAsset.PerlinNoise.Value);
        }

        /// <summary>
        /// MarbleSlash 标准参数（白芯金边刀光）；uv.x=1 为最新挥砍缘，uv.y=0 为外缘（刀尖侧）
        /// </summary>
        /// <param name="effect">EffectLoader.MarbleSlash.Value</param>
        /// <param name="fade">整体透明度 0~1（收势时衰减）</param>
        /// <param name="heat">强击度 0~1，重击/终结挥砍时提升金边与白芯亮度</param>
        public static void ApplyMarbleSlash(Effect effect, float fade = 1f, float heat = 0f) {
            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uFade"]?.SetValue(fade);
            effect.Parameters["uHeat"]?.SetValue(heat);
            effect.Parameters["uNoiseTex"]?.SetValue(CWRAsset.PerlinNoise.Value);
        }

        /// <summary>
        /// 投射物标准拖尾样板：oldPos→世界位置数组（零位补 position）、Trail 懒建+更新、
        /// Additive 绘制后恢复 AlphaBlend。传入的 effect 需已完成参数装配
        /// （<see cref="ApplyGradientTrail"/> / <see cref="ApplyGraniteArc"/>）
        /// </summary>
        /// <param name="projectile">拖尾主体，需在 SetStaticDefaults 配置 TrailCacheLength/TrailingMode</param>
        /// <param name="trail">调用方持有的 Trail 字段（懒初始化）</param>
        /// <param name="widthFunc">宽度函数，入参为沿带进度 0~1</param>
        /// <param name="colorFunc">颜色函数，入参为纹理坐标（x 沿带、y 跨带）</param>
        /// <param name="effect">已装配参数的 shader</param>
        public static void DrawTrailFromOldPos(Projectile projectile, ref Trail trail
            , TrailThicknessCalculator widthFunc, TrailColorEvaluator colorFunc, Effect effect) {
            if (effect == null || projectile.oldPos == null || projectile.oldPos.Length == 0) {
                return;
            }
            Vector2[] positions = new Vector2[projectile.oldPos.Length];
            for (int i = 0; i < positions.Length; i++) {
                if (projectile.oldPos[i] == Vector2.Zero) {
                    projectile.oldPos[i] = projectile.position;
                }
                positions[i] = projectile.oldPos[i] + projectile.Size * 0.5f;
            }
            trail ??= new Trail(positions, widthFunc, colorFunc);
            trail.TrailPositions = positions;
            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            trail.DrawTrail(effect);
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        }

        /// <summary>GradientTrail 全套拖尾：装配参数并按标准样板绘制</summary>
        public static void DrawGradientTrailFromOldPos(Projectile projectile, ref Trail trail
            , TrailThicknessCalculator widthFunc, TrailColorEvaluator colorFunc
            , Texture2D gradientBar, Texture2D baseImage) {
            Effect effect = EffectLoader.GradientTrail?.Value;
            if (effect == null) {
                return;
            }
            ApplyGradientTrail(effect, gradientBar, baseImage);
            DrawTrailFromOldPos(projectile, ref trail, widthFunc, colorFunc, effect);
        }

        /// <summary>GraniteArc 全套拖尾：青蓝电弧带，花岗系投射物标准拖尾</summary>
        public static void DrawGraniteArcTrailFromOldPos(Projectile projectile, ref Trail trail
            , TrailThicknessCalculator widthFunc, TrailColorEvaluator colorFunc, float fade = 1f) {
            Effect effect = EffectLoader.GraniteArc?.Value;
            if (effect == null) {
                return;
            }
            ApplyGraniteArc(effect, fade);
            DrawTrailFromOldPos(projectile, ref trail, widthFunc, colorFunc, effect);
        }
    }
}
