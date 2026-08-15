using System;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI
{
    /// <summary>
    /// 湖畔村图几何：画心尺寸、构图锚点（画内 uv）、热区区域、开合动画参数。
    /// 色板一律走 <see cref="KikasaHudTheme"/> 的双形态访问器，不另立一套。
    /// </summary>
    internal static class KikasaSceneTheme
    {
        /// <summary>岸线在画内的 uv.y——必须与 KikasaScene.fx 的 SHORE_Y 一致</summary>
        public const float ShoreY = 0.63f;

        /// <summary>满水位时水面 uv.y（贴着岸线略低）</summary>
        public const float WaterFullY = 0.655f;

        /// <summary>空湖时水面 uv.y（缩到画底，湖床全露）</summary>
        public const float WaterEmptyY = 0.96f;

        /// <summary>画心宽高比（横卷）</summary>
        public const float AspectRatio = 0.56f;

        /// <summary>画心中心的屏高占比</summary>
        public const float CenterYRatio = 0.44f;

        //====== 画内构图锚点（uv）——村落/鸟居/窗火由 KikasaScene.fx 程序化承担，不设锚点 ======

        /// <summary>恶犬站点（贴图中心；岸上，望着湖）</summary>
        public static readonly Vector2 HoundUv = new(0.36f, 0.56f);

        /// <summary>恶犬身高占画高比（狼贴图按此适配）</summary>
        public const float HoundHeight = 0.13f;

        /// <summary>湖底记忆剪影中心</summary>
        public static readonly Vector2 MemoryUv = new(0.20f, 0.80f);

        //====== 热区（uv 矩形：x, y, w, h） ======

        /// <summary>血湖热区：岸线以下整幅（含干湖床——干湖也点得动）</summary>
        public static readonly Vector4 LakeHotspot = new(0.03f, 0.645f, 0.94f, 0.33f);

        /// <summary>恶犬热区（狼身轮廓外扩一圈）</summary>
        public static readonly Vector4 HoundHotspot = new(0.27f, 0.47f, 0.18f, 0.165f);

        //====== 开合 ======

        /// <summary>开合动画每帧推进量</summary>
        public const float OpenSpeed = 0.085f;

        /// <summary>画心目标矩形（UI 空间），随屏幕收放</summary>
        public static Rectangle CanvasRect() {
            float w = MathHelper.Clamp(KikasaHudTheme.UIScreenW - 240f, 540f, 780f);
            float h = w * AspectRatio;
            float cx = KikasaHudTheme.UIScreenW * 0.5f;
            float cy = KikasaHudTheme.UIScreenH * CenterYRatio;
            return new Rectangle((int)(cx - w * 0.5f), (int)(cy - h * 0.5f), (int)w, (int)h);
        }

        /// <summary>uv 点换算到画心矩形内的 UI 坐标</summary>
        public static Vector2 UvToScreen(Rectangle canvas, Vector2 uv)
            => new(canvas.X + canvas.Width * uv.X, canvas.Y + canvas.Height * uv.Y);

        /// <summary>uv 矩形换算到 UI 空间</summary>
        public static Rectangle UvToScreen(Rectangle canvas, Vector4 uvRect)
            => new((int)(canvas.X + canvas.Width * uvRect.X),
                (int)(canvas.Y + canvas.Height * uvRect.Y),
                (int)(canvas.Width * uvRect.Z),
                (int)(canvas.Height * uvRect.W));

        /// <summary>当前水面 uv：涨水进度映射到画内水位</summary>
        public static float WaterUv(float riseProgress)
            => MathHelper.Lerp(WaterEmptyY, WaterFullY, MathHelper.Clamp(riseProgress, 0f, 1f));

        /// <summary>异相位呼吸波</summary>
        public static float Breath(float time, float seed, float speed = 2f)
            => MathF.Sin(time * speed + seed * 17.39f) * 0.5f + 0.5f;
    }
}
