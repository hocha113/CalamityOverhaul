using CalamityOverhaul.Common;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Items.Stones
{
    /// <summary>花岗/大理石共用VFX，色、渐变条、shader、拖尾样板</summary>
    internal class GraniteMarbleVFX : ICWRLoader
    {
        //贴图目录
        public const string GraniteTex = "CalamityOverhaul/Content/Items/Stones/Granites/";
        public const string MarbleTex = "CalamityOverhaul/Content/Items/Stones/Marbles/";

        //主题色
        public static readonly Color GraniteCore = new Color(120, 185, 255);
        public static readonly Color GraniteDeep = new Color(70, 120, 220);
        public static readonly Color GraniteSpark = new Color(150, 210, 255);
        public static readonly Color MarbleCore = new Color(255, 247, 220);
        public static readonly Color MarbleGold = new Color(228, 196, 120);
        public static readonly Color MarbleDust = new Color(214, 210, 196);

        //64×5 横向渐变，懒加载
        private static Texture2D graniteBar;
        private static Texture2D marbleBar;

        /// <summary>花岗条 x=0深蓝→青→x=1白蓝</summary>
        public static Texture2D GraniteBar => graniteBar ??= BuildGradientBar(
            new Color(45, 75, 190), new Color(90, 200, 255), new Color(205, 240, 255));

        /// <summary>大理石条 x=0暖白→鎏金→x=1白</summary>
        public static Texture2D MarbleBar => marbleBar ??= BuildGradientBar(
            new Color(255, 238, 208), new Color(222, 178, 98), new Color(255, 255, 252));

        //主线程懒生成
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
            //先摘引用再主线程 Dispose
            Texture2D g = graniteBar, m = marbleBar;
            graniteBar = marbleBar = null;
            if (g != null || m != null) {
                Main.RunOnMainThread(() => {
                    g?.Dispose();
                    m?.Dispose();
                });
            }
        }

        /// <summary>沿重力 2px TileCollision 探针，坐骑视为未着地</summary>
        public static bool IsGrounded(Player player) {
            if (player.mount.Active) {
                return false;
            }
            Vector2 probeVelocity = Vector2.UnitY * player.gravDir * 2f;
            Vector2 constrained = Collision.TileCollision(player.position, probeVelocity
                , player.width, player.height, false, false, (int)player.gravDir);
            return constrained.Y != probeVelocity.Y;
        }

        /// <summary>GradientTrail 参数，调用方设 BlendState 后 DrawTrail</summary>
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

        /// <summary>GraniteArc 参数，uv.x=0 最新端</summary>
        public static void ApplyGraniteArc(Effect effect, float fade = 1f) {
            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uFade"]?.SetValue(fade);
            effect.Parameters["uNoiseTex"]?.SetValue(CWRAsset.PerlinNoise.Value);
        }

        /// <summary>MarbleSlash 参数，uv.x=1 最新缘、uv.y=0 外缘</summary>
        /// <param name="fade">透明度 0~1</param>
        /// <param name="heat">强击度 0~1</param>
        public static void ApplyMarbleSlash(Effect effect, float fade = 1f, float heat = 0f) {
            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uFade"]?.SetValue(fade);
            effect.Parameters["uHeat"]?.SetValue(heat);
            effect.Parameters["uNoiseTex"]?.SetValue(CWRAsset.PerlinNoise.Value);
        }

        /// <summary>oldPos 拖尾样板，Additive 后恢复 AlphaBlend，effect 需已装配</summary>
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

        /// <summary>GradientTrail 全套</summary>
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

        /// <summary>GraniteArc 全套</summary>
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
