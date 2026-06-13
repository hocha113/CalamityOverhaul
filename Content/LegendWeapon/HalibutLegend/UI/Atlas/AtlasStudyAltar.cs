using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.Atlas
{
    /// <summary>
    /// 研究祭坛：悬浮在技能海域顶部（海面）的环形祭坛
    /// 把可研究的鱼放入，计时完成后点亮对应技能节点
    /// 研究数据存于 <see cref="HalibutSave"/>（计时在数据层推进，UI只负责交互与表现）
    /// </summary>
    internal class AtlasStudyAltar
    {
        /// <summary>
        /// 祭坛环半径
        /// </summary>
        public const float Radius = 46f;

        private float hover;
        private float rejectFlash;
        private readonly HalibutUIParticlePool particles = new(50);
        private int dissolveTimer;

        /// <summary>
        /// 次级选鱼面板：点击祭坛弹出，列出背包中可研究的鱼供投入研究
        /// </summary>
        public readonly AtlasFishStudyPanel Panel = new();

        /// <summary>
        /// 当前帧的屏幕中心位置，由海域每帧写入
        /// </summary>
        public Vector2 ScreenCenter { get; set; }

        public bool Hovered { get; private set; }

        /// <summary>
        /// 选鱼面板是否打开（打开时海域应让出全部交互）
        /// </summary>
        public bool PanelOpen => Panel.IsOpen;

        public void Update(Rectangle contentArea, HalibutSave save, bool inputAvailable) {
            particles.Update();

            //研究中持续冒出被溶解的光粒（无论面板是否打开都推进）
            if (save.IsStudying) {
                dissolveTimer++;
                if (dissolveTimer % 6 == 0) {
                    Vector2 from = ScreenCenter + Main.rand.NextVector2Circular(14f, 14f);
                    particles.SpawnSpark(from, new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.8f, 1.7f)),
                        HalibutTheme.Glow, 0.8f);
                }
            }
            if (rejectFlash > 0f) {
                rejectFlash = MathF.Max(rejectFlash - 0.04f, 0f);
            }

            //次级选鱼面板优先吃输入；打开时祭坛本体不再响应悬停/点击
            Panel.Update(contentArea, save, inputAvailable);
            if (Panel.IsOpen) {
                Hovered = false;
                hover = MathHelper.Lerp(hover, 0f, 0.15f);
                return;
            }

            Hovered = inputAvailable &&
                Vector2.Distance(Main.MouseScreen, ScreenCenter) < Radius + 12f;
            hover = MathHelper.Lerp(hover, Hovered ? 1f : 0f, 0.15f);

            if (!Hovered) {
                return;
            }
            Main.LocalPlayer.mouseInterface = true;

            //悬浮显示祭坛内正在研究的鱼
            if (save.StudyItem.Alives()) {
                Main.HoverItem = save.StudyItem.Clone();
                Main.hoverItemName = save.StudyItem.Name;
            }

            if (!(Main.mouseLeft && Main.mouseLeftRelease)) {
                return;
            }
            //点击祭坛 = 打开选鱼面板（全屏停时间关背包，无法用鼠标手持鱼放入）
            Main.mouseLeftRelease = false;
            Panel.Open(save);
        }

        /// <summary>
        /// 静默重置选鱼面板（切换图鉴视图 / 重新打开图鉴时调用）
        /// </summary>
        public void ResetPanel() => Panel.Reset();

        /// <summary>
        /// 在所有海域元素之上绘制选鱼面板，由海域在 Draw 末尾调用
        /// </summary>
        public void DrawPanel(SpriteBatch sb, Rectangle contentArea, HalibutSave save, float alpha, float time) {
            Panel.Draw(sb, contentArea, save, alpha, time);
        }

        public void Draw(SpriteBatch sb, HalibutSave save, float alpha, float time) {
            Vector2 center = ScreenCenter;
            float breath = HalibutTheme.Breath(time, 5.1f);

            //祭坛环：双环 + 缓转刻度
            HalibutRenderer.DrawSoftGlow(sb, center, Radius + 26f,
                HalibutTheme.Teal * ((0.30f + hover * 0.18f) * alpha));
            HalibutRenderer.DrawRing(sb, center, Radius, 1.6f,
                HalibutTheme.Glow * ((0.55f + breath * 0.2f + hover * 0.25f) * alpha));
            HalibutRenderer.DrawRing(sb, center, Radius - 7f, 1f,
                HalibutTheme.Teal * (0.6f * alpha));
            float markRot = time * 0.4f;
            for (int i = 0; i < 3; i++) {
                float a0 = markRot + i * MathHelper.TwoPi / 3f;
                HalibutRenderer.DrawArcStroke(sb, center, Radius + 6f, a0, a0 + 0.7f, 1.2f,
                    HalibutTheme.GlowHi * (0.45f * alpha));
            }

            //拒绝反馈红闪
            if (rejectFlash > 0.01f) {
                HalibutRenderer.DrawRing(sb, center, Radius + 3f, 1.8f,
                    HalibutTheme.Danger * (rejectFlash * 0.8f * alpha));
            }

            particles.Draw(sb, alpha);

            //祭坛内容
            if (save.StudyItem.Alives() && save.StudyItem.type > ItemID.None) {
                float progress = save.IsStudying
                    ? MathHelper.Clamp(save.StudyTimer / (float)save.StudyDuration, 0f, 1f)
                    : 0f;
                //被研究的鱼：缓慢旋转漂浮 + 随进度透明化（溶解感）
                float dissolve = 1f - progress * 0.55f;
                float bob = MathF.Sin(time * 1.6f) * 3f;
                float itemRot = MathF.Sin(time * 0.8f) * 0.18f;
                VaultUtils.SimpleDrawItem(sb, save.StudyItem.type, center + new Vector2(0f, bob),
                    36, 1f, itemRot, Color.White * (dissolve * alpha));

                //研究进度弧
                if (save.IsStudying) {
                    float aStart = -MathHelper.PiOver2;
                    HalibutRenderer.DrawArcStroke(sb, center, Radius - 3f,
                        aStart, aStart + MathHelper.TwoPi * progress, 2.2f,
                        HalibutTheme.Accent * (0.95f * alpha));

                    //剩余时间与百分比
                    int remainSec = Math.Max(0, (save.StudyDuration - save.StudyTimer) / 60);
                    string timeText = $"{remainSec / 60:D2}:{remainSec % 60:D2}";
                    HalibutRenderer.DrawGlowTextCentered(sb, timeText, center + new Vector2(0f, -Radius - 16f),
                        HalibutTheme.Accent * alpha, HalibutTheme.Deep * (0.5f * alpha), 0.8f);
                    HalibutRenderer.DrawGlowTextCentered(sb, $"{(int)(progress * 100)}%",
                        center + new Vector2(0f, Radius + 15f),
                        HalibutTheme.Text * alpha, HalibutTheme.Deep * (0.4f * alpha), 0.72f);
                }
            }
            else {
                //空祭坛：中心一点幽光
                HalibutRenderer.DrawDisc(sb, center, 3.4f + breath * 1.5f, 3f,
                    HalibutTheme.GlowHi * ((0.5f + breath * 0.3f) * alpha));
            }

            //标题
            HalibutRenderer.DrawGlowTextCentered(sb, HalibutAtlas.AltarTitle.Value,
                center + new Vector2(0f, -Radius - 34f),
                HalibutTheme.Text * alpha, HalibutTheme.Glow * (0.35f * alpha), 0.86f);

            //悬停提示
            if (Hovered && !save.StudyItem.Alives()) {
                HalibutRenderer.DrawCursorPanel(sb, Main.MouseScreen, HalibutAtlas.AltarTitle.Value,
                    HalibutTheme.GlowHi, HalibutAtlas.AltarHint.Value, alpha);
            }
        }
    }
}
