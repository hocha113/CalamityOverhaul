using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Onikiri
{
    internal enum OnikiriParticleMode
    {
        Dialogue,
        Choice,
        Popup,
    }

    /// <summary>
    /// 鬼切叙事皮肤共享状态:计时器、两翼落花、选项 hover 缓动、墨迹未干打字机。<br/>
    /// 落花只在面板左右外沿飘落,永不穿过正文区(阅读保护纪律)
    /// </summary>
    internal sealed class OnikiriPanelState
    {
        /// <summary>shader 画布外扩:注连墨绸/绯月/纸垂住在这一圈</summary>
        public const int ShaderEdgePad = 18;

        //==== 调色板(与 CrimsonSlashRenderer 四色同源,此处为 CPU 侧 LDR 对应) ====
        public static readonly Color Paper = new(242, 234, 222);
        public static readonly Color HotWhite = new(255, 243, 226);
        public static readonly Color Bright = new(255, 41, 26);
        public static readonly Color Deep = new(158, 13, 18);
        public static readonly Color Dark = new(41, 4, 9);
        public static readonly Color Ink = new(24, 12, 15);
        public static readonly Color Seal = new(186, 32, 26);

        public float SwayTimer;
        public float PulseTimer;
        public float ShaderTime;

        //==== 墨迹未干:最新字符的绯红罩色随时间褪去 ====
        private int _lastVisibleChars = -1;
        private float _inkAge = 60f;

        //==== 选项 hover 缓动(框架给的是 0/1 突变,缓动归皮肤自己管) ====
        private readonly float[] _optionHover = new float[16];

        private readonly List<FallingPetal> _petals = [];
        private int _petalSpawnTimer;
        private OnikiriParticleMode _mode = OnikiriParticleMode.Dialogue;

        public void Update(Rectangle panelRect, bool active, OnikiriParticleMode mode = OnikiriParticleMode.Dialogue) {
            _mode = mode;
            SwayTimer = SkinAnimUtil.WrapTimer(SwayTimer, 0.022f);
            PulseTimer = SkinAnimUtil.WrapTimer(PulseTimer, 0.030f);
            ShaderTime = SkinAnimUtil.AdvanceShaderTime(ShaderTime);
            _inkAge = Math.Min(_inkAge + 1f, 60f);

            if (!active) {
                return;
            }

            int interval = mode switch {
                OnikiriParticleMode.Popup => 55,
                OnikiriParticleMode.Choice => 0,   //抉择时刻保持静场
                _ => 36,
            };
            int maxCount = mode == OnikiriParticleMode.Popup ? 3 : 6;

            if (interval > 0) {
                _petalSpawnTimer++;
                if (_petalSpawnTimer >= interval && _petals.Count < maxCount) {
                    _petalSpawnTimer = 0;
                    _petals.Add(new FallingPetal(panelRect));
                }
            }

            for (int i = _petals.Count - 1; i >= 0; i--) {
                if (_petals[i].Update(panelRect)) {
                    _petals.RemoveAt(i);
                }
            }
        }

        public void DrawPetals(SpriteBatch spriteBatch, float alpha) {
            foreach (FallingPetal petal in _petals) {
                petal.Draw(spriteBatch, alpha);
            }
        }

        /// <summary>喂入本帧可见字符数,内部维护"墨迹未干"年龄</summary>
        public void TrackTypewriter(int visibleChars) {
            if (visibleChars != _lastVisibleChars) {
                _lastVisibleChars = visibleChars;
                _inkAge = 0f;
            }
        }

        /// <summary>最新字符绯红罩色强度 0~1</summary>
        public float InkStrength => 1f - MathHelper.Clamp(_inkAge / 16f, 0f, 1f);

        /// <summary>逐帧缓动全部选项的 hover 值(index 为绝对选项序号)</summary>
        public void UpdateOptionHovers(int hoverIndex, int optionCount) {
            int count = Math.Min(optionCount, _optionHover.Length);
            for (int i = 0; i < count; i++) {
                float target = i == hoverIndex ? 1f : 0f;
                float cur = _optionHover[i];
                _optionHover[i] = cur + (target - cur) * (target > cur ? 0.24f : 0.14f);
            }
        }

        public float GetOptionHover(int optionIndex)
            => optionIndex >= 0 && optionIndex < _optionHover.Length ? _optionHover[optionIndex] : 0f;

        public void Reset() {
            SwayTimer = 0f;
            PulseTimer = 0f;
            ShaderTime = 0f;
            _petals.Clear();
            _petalSpawnTimer = 0;
            _lastVisibleChars = -1;
            _inkAge = 60f;
            Array.Clear(_optionHover, 0, _optionHover.Length);
        }

        /// <summary>
        /// 两翼落花。形体由三块像素矩形拼成(暗影/纸白花身/绯红瓣尖),
        /// 靠"横摆下落 + 宽度呼吸的翻飞透视"卖出花瓣感,而非依赖贴图轮廓
        /// </summary>
        private sealed class FallingPetal
        {
            private Vector2 _pos;
            private readonly float _fallSpeed;
            private readonly float _swayAmp;
            private readonly float _swayFreq;
            private readonly float _flipFreq;
            private readonly float _phase;
            private readonly float _scale;
            private readonly float _outwardDrift;
            private float _rot;
            private readonly float _rotSpd;
            private float _life;
            private readonly float _maxLife;

            public FallingPetal(Rectangle panel) {
                bool leftSide = Main.rand.NextBool();
                //只取左右两翼窄条,外扩 shader 边沿区(±14px),不进入正文区
                float x = leftSide
                    ? Main.rand.NextFloat(panel.X - 14f, panel.X + 16f)
                    : Main.rand.NextFloat(panel.Right - 16f, panel.Right + 14f);
                _pos = new Vector2(x, panel.Y - Main.rand.NextFloat(4f, 14f));
                _fallSpeed = Main.rand.NextFloat(0.32f, 0.62f);
                _swayAmp = Main.rand.NextFloat(0.28f, 0.75f);
                _swayFreq = Main.rand.NextFloat(0.035f, 0.06f);
                _flipFreq = Main.rand.NextFloat(0.05f, 0.09f);
                _phase = Main.rand.NextFloat(MathHelper.TwoPi);
                _scale = Main.rand.NextFloat(0.8f, 1.25f);
                _outwardDrift = (leftSide ? -1f : 1f) * Main.rand.NextFloat(0.02f, 0.07f);
                _rot = Main.rand.NextFloat(MathHelper.TwoPi);
                _rotSpd = Main.rand.NextFloat(-0.02f, 0.02f);
                _maxLife = Main.rand.NextFloat(210f, 320f);
            }

            public bool Update(Rectangle panel) {
                _life++;
                _pos.Y += _fallSpeed * (0.85f + 0.15f * (float)Math.Sin(_life * 0.05f + _phase));
                _pos.X += (float)Math.Cos(_life * _swayFreq + _phase) * _swayAmp * 0.35f + _outwardDrift;
                _rot += _rotSpd;
                return _life >= _maxLife || _pos.Y > panel.Bottom - 4f;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D pixel = VaultAsset.placeholder2.Value;
                Rectangle src = new(0, 0, 1, 1);
                float t = _life / _maxLife;
                float fade = (float)Math.Pow(Math.Sin(MathHelper.Clamp(t, 0f, 1f) * MathHelper.Pi), 0.8);
                //翻飞透视:花瓣绕长轴翻转,视觉宽度呼吸
                float flip = MathHelper.Lerp(0.32f, 1f, Math.Abs((float)Math.Sin(_life * _flipFreq + _phase)));
                Vector2 half = new(0.5f, 0.5f);
                Vector2 body = new(6.4f * _scale * flip, 3.5f * _scale);
                Vector2 tipOff = _rot.ToRotationVector2() * (3.1f * _scale * flip);

                sb.Draw(pixel, _pos + new Vector2(0.8f, 1.1f), src, Dark * (alpha * 0.34f * fade), _rot, half, body, SpriteEffects.None, 0f);
                sb.Draw(pixel, _pos, src, Paper * (alpha * 0.60f * fade), _rot, half, body, SpriteEffects.None, 0f);
                sb.Draw(pixel, _pos + tipOff, src, Bright * (alpha * 0.42f * fade), _rot, half, new Vector2(2.5f * _scale * flip, 2.0f * _scale), SpriteEffects.None, 0f);
            }
        }
    }
}
