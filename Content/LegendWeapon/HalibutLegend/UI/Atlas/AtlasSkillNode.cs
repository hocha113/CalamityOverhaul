using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.Atlas
{
    /// <summary>图鉴技能节点视图模型</summary>
    internal class AtlasSkillNode
    {
        public readonly FishSkill Skill;
        public readonly int Tier;
        /// <summary>
        /// 在海域布局空间中的基准位置：
        /// X 为相对屏幕中线的偏移（实时换算，UI缩放/分辨率变化时无需重建），Y 为绝对深度
        /// </summary>
        public Vector2 LayoutPos;
        /// <summary>
        /// 漂浮相位种子
        /// </summary>
        public readonly float DriftSeed;
        /// <summary>
        /// 平滑悬停量
        /// </summary>
        public float Hover;
        /// <summary>
        /// 点亮闪光（解锁瞬间触发，1→0衰减）
        /// </summary>
        public float Ignite;

        public const float HitRadius = 26f;

        public AtlasSkillNode(FishSkill skill) {
            Skill = skill;
            Tier = AtlasTierMap.GetTier(skill);
            DriftSeed = (skill.Name.GetHashCode() & 0xFFFF) / (float)0xFFFF * MathHelper.TwoPi;
        }

        /// <summary>
        /// 当前帧的屏幕位置（中线实时换算 + 布局位置 + 滚动 + 漂浮）
        /// </summary>
        public Vector2 ScreenPos(float scroll, float time) {
            Vector2 drift = new(
                MathF.Sin(time * 0.7f + DriftSeed) * 4f,
                MathF.Sin(time * 0.52f + DriftSeed * 1.7f) * 5f);
            return new Vector2(HalibutTheme.UIScreenW * 0.5f + LayoutPos.X, LayoutPos.Y)
                + drift - new Vector2(0f, scroll);
        }

        public void TriggerIgnite() => Ignite = 1f;

        public void UpdateState(bool hovered) {
            Hover = MathHelper.Lerp(Hover, hovered ? 1f : 0f, 0.18f);
            if (Ignite > 0f) {
                Ignite = MathF.Max(Ignite - 0.02f, 0f);
            }
        }

        /// <summary>
        /// 绘制节点
        /// </summary>
        /// <param name="sb">画布</param>
        /// <param name="pos">屏幕位置</param>
        /// <param name="unlocked">是否已解锁</param>
        /// <param name="equipped">是否在装备栏中</param>
        /// <param name="selected">是否为当前选用技能</param>
        /// <param name="alpha">整体透明度</param>
        /// <param name="time">动画时间</param>
        public void Draw(SpriteBatch sb, Vector2 pos, bool unlocked, bool equipped, bool selected,
            float alpha, float time) {
            Texture2D icon = Skill.Icon;
            if (icon == null) {
                return;
            }
            Color tierCol = HalibutTheme.TierColor(Tier);
            float baseScale = 34f / MathF.Max(icon.Width, icon.Height);
            float scale = baseScale * (1f + Hover * 0.22f + Ignite * 0.25f);

            if (unlocked) {
                //外晕
                float breath = HalibutTheme.Breath(time, DriftSeed, 1.8f);
                HalibutRenderer.DrawSoftGlow(sb, pos, 26f + breath * 5f + Hover * 10f,
                    tierCol * ((0.30f + breath * 0.12f + Hover * 0.25f) * alpha));
                //光环
                HalibutRenderer.DrawRing(sb, pos, 21f + Hover * 4f, 1.2f,
                    tierCol * ((0.5f + Hover * 0.4f) * alpha));
                //图标
                sb.Draw(icon, pos, null, Color.White * alpha, 0f, icon.Size() * 0.5f, scale, SpriteEffects.None, 0f);
                //冷却中暗化扇区
                if (Skill.CooldownRatio > 0.01f) {
                    HalibutRenderer.DrawCooldownSweep(sb, pos, 19f, Skill.CooldownRatio, alpha * 0.8f);
                }
            }
            else {
                //未解锁：剪影 + 暗环 + 问号
                HalibutRenderer.DrawRing(sb, pos, 20f + Hover * 3f, 1f,
                    HalibutTheme.Disabled * ((0.35f + Hover * 0.3f) * alpha));
                sb.Draw(icon, pos, null, HalibutTheme.Void * (0.92f * alpha), 0f,
                    icon.Size() * 0.5f, scale, SpriteEffects.None, 0f);
                HalibutRenderer.DrawGlowTextCentered(sb, "?", pos,
                    HalibutTheme.TextDim * ((0.6f + Hover * 0.4f) * alpha),
                    HalibutTheme.Deep * (0.5f * alpha), 0.8f);
            }

            //装备标记：节点下方的小光点
            if (equipped) {
                HalibutRenderer.DrawDisc(sb, pos + new Vector2(0f, 24f), 2.2f, 1.6f,
                    HalibutTheme.GlowHi * (0.9f * alpha));
            }
            //当前选用：暖金描环
            if (selected) {
                float pulse = HalibutTheme.Breath(time, DriftSeed, 3f);
                HalibutRenderer.DrawRing(sb, pos, 25f + pulse * 2f, 1.4f,
                    HalibutTheme.Accent * ((0.7f + pulse * 0.3f) * alpha));
            }

            //点亮闪光
            if (Ignite > 0.01f) {
                float t = 1f - Ignite;
                HalibutRenderer.DrawRing(sb, pos, 10f + t * 46f, MathHelper.Lerp(3.5f, 0.8f, t),
                    HalibutTheme.Caustic * (Ignite * alpha));
                HalibutRenderer.DrawSoftGlow(sb, pos, 36f * Ignite, HalibutTheme.Caustic * (Ignite * 0.7f * alpha));
            }

            //悬停时显示技能名
            if (Hover > 0.25f && unlocked) {
                HalibutRenderer.DrawGlowTextCentered(sb, Skill.DisplayName?.Value ?? Skill.Name,
                    pos + new Vector2(0f, -32f), HalibutTheme.Text * (Hover * alpha),
                    tierCol * (Hover * 0.4f * alpha), 0.8f);
            }
        }
    }
}
