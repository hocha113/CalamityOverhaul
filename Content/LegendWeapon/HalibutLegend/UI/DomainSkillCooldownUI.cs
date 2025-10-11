using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent; //FontAssets

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    [VaultLoaden(CWRConstant.UI + "Halibut/FishSkill")]
    internal class DomainSkillCooldownUI : UIHandle
    {
        [VaultLoaden("@InnoVault/Effects/")]
        private static Asset<Effect> GearProgress { get; set; }

        //使用反射加载，大小都是34*34，名称需与资源名一致
        public static Texture2D RestartFish;   //重启
        public static Texture2D Superposition; //叠加
        public static Texture2D FishTeleport;  //传送

        //跟踪最大冷却，用于计算剩余比例
        private readonly Dictionary<DomainSkill, int> _maxCooldown = new();
        private readonly List<CooldownIcon> _activeIcons = new();

        //出现/消失整体动画
        private float _panelAppear; //0-1

        //图标尺寸（资源都是 34x34）
        private const int IconSize = 34;
        private const int IconPadding = 6; //竖向间距

        private enum DomainSkill
        {
            Restart,
            Superposition,
            Teleport
        }

        private class CooldownIcon
        {
            public DomainSkill Skill;
            public Texture2D Texture;
            public int Current;   //当前剩余冷却帧
            public int Max;       //最大冷却帧
            public float RemainingRatio; //剩余比例(0-1) 1=刚开始冷却
            public Vector2 DrawPos; //左上角屏幕坐标
            public float LocalAppear; //个体出现动画 0-1
        }

        public override bool Active => _panelAppear > 0.01f || HasAnyCooldown();

        private static bool TryGetHalibut(out HalibutPlayer hp, out Player player)
        {
            player = Main.LocalPlayer;
            if (player?.active == true && player.TryGetOverride<HalibutPlayer>(out hp) && hp.HasHalibut) {
                return true;
            }
            hp = null;
            return false;
        }

        private bool HasAnyCooldown()
        {
            if (!TryGetHalibut(out var hp, out _)) return false;
            return hp.RestartFishCooldown > 0 || hp.SuperpositionCooldown > 0 || hp.FishTeleportCooldown > 0;
        }

        public override void Update()
        {
            if (!TryGetHalibut(out var hp, out var player)) {
                _panelAppear = MathHelper.Clamp(_panelAppear - 0.08f, 0f, 1f);
                _activeIcons.Clear();
                return;
            }

            _activeIcons.Clear();
            CollectCooldown(hp.RestartFishCooldown, DomainSkill.Restart, RestartFish);
            CollectCooldown(hp.SuperpositionCooldown, DomainSkill.Superposition, Superposition);
            CollectCooldown(hp.FishTeleportCooldown, DomainSkill.Teleport, FishTeleport);

            bool any = _activeIcons.Count > 0;
            _panelAppear = MathHelper.Clamp(_panelAppear + (any ? 0.12f : -0.12f), 0f, 1f);
            if (_panelAppear <= 0f) return;

            _activeIcons.Sort((a, b) => b.Current.CompareTo(a.Current));

            Vector2 screenAnchor = new(Main.screenWidth / 13 * 12, Main.screenHeight / 13 * 12);

            float globalOffset = MathHelper.Lerp(20f, 0f, EaseOutCubic(_panelAppear));

            for (int i = 0; i < _activeIcons.Count; i++) {
                var icon = _activeIcons[i];
                float target = _panelAppear > 0f ? 1f : 0f;
                float delay = i * 0.08f;
                float speed = 0.15f;
                if (_panelAppear < 1f) {
                    target = Math.Min(target, Math.Max(0f, (_panelAppear - delay) / 0.5f));
                }
                icon.LocalAppear = MathHelper.Lerp(icon.LocalAppear, target, speed);

                float localSlide = MathHelper.Lerp(16f, 0f, EaseOutCubic(icon.LocalAppear));
                float y = screenAnchor.Y + globalOffset + i * (IconSize + IconPadding) + localSlide;
                float x = screenAnchor.X - IconSize / 2f;
                icon.DrawPos = new Vector2(x, y);
            }

            if (_activeIcons.Count > 0) {
                Size = new Vector2(IconSize, _activeIcons.Count * (IconSize + IconPadding) - IconPadding);
                DrawPosition = _activeIcons[0].DrawPos;
                UIHitBox = DrawPosition.GetRectangle(Size);
            }
        }

        private void CollectCooldown(int current, DomainSkill skill, Texture2D tex)
        {
            if (current <= 0 || tex == null) return;
            if (!_maxCooldown.TryGetValue(skill, out int max) || current > max) {
                _maxCooldown[skill] = current;
                max = current;
            }
            if (max <= 0) return;
            float remainingRatio = MathHelper.Clamp(current / (float)max, 0f, 1f);
            _activeIcons.Add(new CooldownIcon {
                Skill = skill,
                Texture = tex,
                Current = current,
                Max = max,
                RemainingRatio = remainingRatio,
                LocalAppear = (remainingRatio >= 0.999f) ? 0f : 1f
            });
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (_panelAppear <= 0f || _activeIcons.Count == 0) return;

            foreach (var icon in _activeIcons) {
                float a = _panelAppear * icon.LocalAppear;
                if (a <= 0.01f) continue;

                Rectangle rect = new Rectangle((int)icon.DrawPos.X, (int)icon.DrawPos.Y, IconSize, IconSize);
                Vector2 pos = icon.DrawPos;

                //背景圆（淡灰）
                spriteBatch.Draw(icon.Texture, rect, Color.White * 0.15f * a);

                //主图标（基色）
                spriteBatch.Draw(icon.Texture, rect, Color.White * 0.9f * a);

                //冷却遮罩（使用 GearProgress 着色器）
                if (GearProgress?.Value != null) {
                    GearProgress.Value.Parameters["Progress"].SetValue(icon.RemainingRatio); //0=冷却完成,1=刚开始
                    GearProgress.Value.Parameters["Rotation"].SetValue(-MathHelper.PiOver2);
                    Main.spriteBatch.End();
                    Main.spriteBatch.Begin(0, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, GearProgress.Value, Main.UIScaleMatrix);
                    //用带进度效果再绘制一次图标（可叠加颜色显示剩余部分）
                    Main.spriteBatch.Draw(icon.Texture, rect, Color.Cyan * 0.85f * a);
                    Main.spriteBatch.End();
                    Main.spriteBatch.Begin(0, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
                }

                //文本（秒） 可选：只在剩余>1s 显示
                int seconds = (int)Math.Ceiling(icon.Current / 60f);
                if (icon.Current > 30) { //冷却剩余>0.5s再显示
                    string text = seconds.ToString();
                    var font = FontAssets.ItemStack.Value;
                    Vector2 size = font.MeasureString(text);
                    Vector2 textPos = pos + new Vector2(IconSize / 2f, IconSize / 2f) - size / 2f + new Vector2(0, 1);
                    Color c = Color.White * a * 0.92f;
                    Utils.DrawBorderString(spriteBatch, text, textPos, c, 0.6f, 0f, 0f, -1);
                }
            }
        }

        private static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - t, 3f);
    }
}
