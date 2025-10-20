using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using static CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.HalibutUIAsset;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    /// <summary>
    /// 技能图标飞行实体，用于新技能解锁时的视觉效果
    /// </summary>
    internal class SkillIconEntity
    {
        public FishSkill FishSkill;
        public Vector2 Position;
        public float Scale = 0.5f;
        public float Rotation;
        public float RotationSpeed;
        public float Alpha = 1f;
        public int LifeTime;
        public int MaxLifeTime = 60; //1秒的飞行时间

        //贝塞尔曲线控制点
        private Vector2 startPos;
        private Vector2 endPos;
        private Vector2 controlPoint1;
        private Vector2 controlPoint2;

        public SkillIconEntity(FishSkill fishSkill, Vector2 start, Vector2 end) {
            FishSkill = fishSkill;
            startPos = start;
            endPos = end;

            //创建贝塞尔曲线控制点，产生优雅的弧线飞行轨迹
            Vector2 mid = (start + end) / 2;
            float distance = Vector2.Distance(start, end);

            //第一个控制点：向上偏移
            controlPoint1 = start + new Vector2(distance * 0.2f, -distance * 0.4f);
            //第二个控制点：继续向上但开始向目标靠近
            controlPoint2 = mid + new Vector2(distance * 0.1f, -distance * 0.3f);

            Position = start;
            RotationSpeed = (Main.rand.NextFloat() - 0.5f) * 0.2f; //随机旋转速度
            LifeTime = 0;
        }

        public bool Update() {
            LifeTime++;

            //计算进度（0到1）
            float progress = (float)LifeTime / MaxLifeTime;
            float easedProgress = CWRUtils.EaseOutCubic(progress);

            //使用贝塞尔曲线计算位置
            Position = CWRUtils.CubicBezier(easedProgress, startPos, controlPoint1, controlPoint2, endPos);

            //旋转效果
            Rotation += RotationSpeed;

            //缩放动画：先放大再缩小
            if (progress < 0.3f) {
                Scale = 0.5f + (progress / 0.3f) * 0.5f; //0.5 -> 1.0
            }
            else {
                Scale = 1.0f - ((progress - 0.3f) / 0.7f) * 0.4f; //1.0 -> 0.6
            }

            //透明度：到达终点前保持不透明，最后快速淡出
            if (progress < 0.85f) {
                Alpha = 1f;
            }
            else {
                Alpha = 1f - ((progress - 0.85f) / 0.15f);
            }

            return LifeTime >= MaxLifeTime;
        }

        public void Draw(SpriteBatch spriteBatch) {
            if (FishSkill?.Icon == null) return;

            Vector2 iconSize = new Vector2(Skillcon.Width, Skillcon.Height / 5);
            Vector2 origin = iconSize / 2;

            //绘制发光外圈
            Color glowColor = Color.Gold with { A = 0 } * (Alpha * 0.6f);
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4 + Rotation;
                Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 3f;
                spriteBatch.Draw(FishSkill.Icon, Position + offset, null, glowColor, Rotation, origin, Scale * 1.1f, SpriteEffects.None, 0);
            }

            //绘制主图标
            Color mainColor = Color.White * Alpha;
            spriteBatch.Draw(FishSkill.Icon, Position, null, mainColor, Rotation, origin, Scale, SpriteEffects.None, 0);
        }
    }
}
