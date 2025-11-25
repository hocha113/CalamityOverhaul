using CalamityOverhaul.Common;
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
        public static Texture2D RestartFish = null;   //重启
        public static Texture2D Superposition = null; //叠加
        public static Texture2D FishTeleport = null;  //传送

        //跟踪最大冷却，用于计算剩余比例
        private readonly Dictionary<DomainSkill, int> _maxCooldown = new();
        private readonly List<CooldownIcon> _activeIcons = new();

        //抖动强度记录（0-1），独立于图标实例，避免列表重建丢失
        private readonly Dictionary<DomainSkill, float> _shakeStrength = new();

        //出现/消失整体动画
        private float _panelAppear; //0-1

        //图标尺寸（资源都是 34x34）
        private const int IconSize = 34;
        private const int IconPadding = 6; //竖向间距

        private enum DomainSkill { Restart, Superposition, Teleport }

        private class CooldownIcon
        {
            public DomainSkill Skill;
            public Texture2D Texture;
            public int Current;   //当前剩余冷却帧
            public int Max;       //最大冷却帧
            public float RemainingRatio; //剩余比例(0-1) 1=刚开始冷却
            public Vector2 DrawPos; //左上角屏幕坐标
            public float LocalAppear; //个体出现动画 0-1
            public float Shake; //当前帧抖动强度 0-1（由外部字典赋值）
        }

        public override bool Active => _panelAppear > 0.01f || HasAnyCooldown();

        private static bool TryGetHalibut(out HalibutPlayer hp, out Player player) {
            player = Main.LocalPlayer;
            if (player?.active == true && player.TryGetOverride(out hp) && hp.HeldHalibut) return true;
            hp = null; return false;
        }

        private bool HasAnyCooldown() {
            if (!TryGetHalibut(out var hp, out _)) return false;
            return hp.RestartFishCooldown > 0 || hp.SuperpositionCooldown > 0 || hp.FishTeleportCooldown > 0;
        }

        public override void Update() {
            if (!TryGetHalibut(out var hp, out var player)) {
                _panelAppear = MathHelper.Clamp(_panelAppear - 0.08f, 0f, 1f);
                _activeIcons.Clear();
                _shakeStrength.Clear();
                return;
            }

            //按键触发抖动：如果处于冷却且试图使用
            if (hp.RestartFishCooldown > 0 && CWRKeySystem.Halibut_Restart.JustPressed) TriggerShake(DomainSkill.Restart);
            if (hp.SuperpositionCooldown > 0 && CWRKeySystem.Halibut_Superposition.JustPressed) TriggerShake(DomainSkill.Superposition);
            if (hp.FishTeleportCooldown > 0 && CWRKeySystem.Halibut_Teleport.JustPressed) TriggerShake(DomainSkill.Teleport);

            //衰减 shake 值
            DecayShake(DomainSkill.Restart);
            DecayShake(DomainSkill.Superposition);
            DecayShake(DomainSkill.Teleport);

            _activeIcons.Clear();
            CollectCooldown(hp.RestartFishCooldown, DomainSkill.Restart, RestartFish);
            CollectCooldown(hp.SuperpositionCooldown, DomainSkill.Superposition, Superposition);
            CollectCooldown(hp.FishTeleportCooldown, DomainSkill.Teleport, FishTeleport);

            bool any = _activeIcons.Count > 0;
            _panelAppear = MathHelper.Clamp(_panelAppear + (any ? 0.12f : -0.12f), 0f, 1f);
            if (_panelAppear <= 0f) return;

            _activeIcons.Sort((a, b) => b.Current.CompareTo(a.Current));

            Vector2 screenAnchor = new(Main.screenWidth - 80, Main.screenHeight - 180);
            float globalOffset = MathHelper.Lerp(20f, 0f, CWRUtils.EaseOutCubic(_panelAppear));

            for (int i = 0; i < _activeIcons.Count; i++) {
                var icon = _activeIcons[i];
                float target = _panelAppear > 0f ? 1f : 0f;
                float delay = i * 0.08f;
                float speed = 0.15f;
                if (_panelAppear < 1f) target = Math.Min(target, Math.Max(0f, (_panelAppear - delay) / 0.5f));
                icon.LocalAppear = MathHelper.Lerp(icon.LocalAppear, target, speed);

                float localSlide = MathHelper.Lerp(16f, 0f, CWRUtils.EaseOutCubic(icon.LocalAppear));
                float y = screenAnchor.Y + globalOffset + i * (IconSize + IconPadding) + localSlide;
                float x = screenAnchor.X - IconSize / 2f;
                icon.DrawPos = new Vector2(x, y);

                if (_shakeStrength.TryGetValue(icon.Skill, out float shake)) {
                    icon.Shake = shake;
                }
            }

            if (_activeIcons.Count > 0) {
                Size = new Vector2(IconSize, _activeIcons.Count * (IconSize + IconPadding) - IconPadding);
                DrawPosition = _activeIcons[0].DrawPos;
                UIHitBox = DrawPosition.GetRectangle(Size);
            }
        }

        private void TriggerShake(DomainSkill skill) {
            if (!_shakeStrength.TryGetValue(skill, out float v) || v < 0.6f) {
                //重置或强化为高值
                _shakeStrength[skill] = 1f;
            }
            else {
                //若正在抖动，稍微叠加但不超过1
                _shakeStrength[skill] = MathHelper.Clamp(v + 0.25f, 0f, 1f);
            }
        }

        private void DecayShake(DomainSkill skill) {
            if (_shakeStrength.TryGetValue(skill, out float v)) {
                v *= 0.82f; //指数衰减
                if (v < 0.02f) v = 0f;
                _shakeStrength[skill] = v;
            }
        }

        private void CollectCooldown(int current, DomainSkill skill, Texture2D tex) {
            if (current <= 0 || tex == null) return;
            if (!_maxCooldown.TryGetValue(skill, out int max) || current > max) { _maxCooldown[skill] = current; max = current; }
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

        public override void Draw(SpriteBatch spriteBatch) {
            if (_panelAppear <= 0f || _activeIcons.Count == 0) return;

            foreach (var icon in _activeIcons) {
                float a = _panelAppear * icon.LocalAppear;
                if (a <= 0.01f) continue;

                //抖动偏移：使用快速sin + 轻微乱序(基于技能枚举) 制造更自然的警告感
                Vector2 shakeOffset = Vector2.Zero;
                if (icon.Shake > 0f) {
                    float t = (float)Main.GameUpdateCount * 0.4f;
                    float phase = (int)icon.Skill * 1.7f; //枚举区分
                    float amp = 4f * icon.Shake; //最大4px
                    shakeOffset.X = (float)Math.Sin(t * 5f + phase) * amp;
                    shakeOffset.Y = (float)Math.Cos(t * 3.2f + phase) * amp * 0.5f;
                }

                Rectangle rect = new((int)(icon.DrawPos.X + shakeOffset.X), (int)(icon.DrawPos.Y + shakeOffset.Y), IconSize, IconSize);
                bool hovered = rect.Contains(Main.mouseX, Main.mouseY);
                if (hovered) Main.LocalPlayer.mouseInterface = true; //防止与背包冲突

                //背景
                spriteBatch.Draw(icon.Texture, rect, Color.White * 0.15f * a);
                //主体（悬停或抖动稍微提亮）`
                float brighten = hovered ? 1.15f : 0.9f + icon.Shake * 0.25f;
                spriteBatch.Draw(icon.Texture, rect, Color.White * brighten * a);

                //圆形进度遮罩
                if (GearProgress?.Value != null) {
                    GearProgress.Value.Parameters["Progress"].SetValue(1f - icon.RemainingRatio);
                    GearProgress.Value.Parameters["Rotation"].SetValue(-MathHelper.PiOver2);
                    Main.spriteBatch.End();
                    Main.spriteBatch.Begin(0, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, GearProgress.Value, Main.UIScaleMatrix);
                    Main.spriteBatch.Draw(icon.Texture, rect, (hovered ? Color.Cyan : Color.DeepSkyBlue) * 0.85f * a);
                    Main.spriteBatch.End();
                    Main.spriteBatch.Begin(0, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
                }

                //只在悬停时显示数字
                if (hovered) {
                    int seconds = (int)Math.Ceiling(icon.Current / 60f);
                    string text = seconds.ToString();
                    var font = FontAssets.ItemStack.Value;
                    var size = font.MeasureString(text);
                    var textPos = new Vector2(rect.X + IconSize / 2f, rect.Y + IconSize / 2f) - size / 2f + new Vector2(0, 1);
                    Utils.DrawBorderString(spriteBatch, text, textPos, Color.White * a, 0.6f, 0f, 0f, -1);
                }
            }
        }
    }
}
