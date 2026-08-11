using CalamityOverhaul.Content.LegendWeapon.KikasaLegend;
using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Kikasa
{
    internal enum KikasaParticleMode
    {
        Dialogue,
        Choice,
        Popup,
    }

    /// <summary>雨丝只在左右外沿,不进正文;落底溅开涟漪</summary>
    internal sealed class KikasaPanelState
    {
        /// <summary>shader 画布外扩</summary>
        public const int ShaderEdgePad = 18;

        //==== 调色板别名(源 KikasaStoryTheme,Void 仅 shader 侧用不设别名) ====
        public static readonly Color Deep = KikasaStoryTheme.Deep;
        public static readonly Color Mid = KikasaStoryTheme.Mid;
        public static readonly Color Rain = KikasaStoryTheme.Rain;
        public static readonly Color Moon = KikasaStoryTheme.Moon;
        public static readonly Color WetInk = KikasaStoryTheme.WetInk;
        public static readonly Color Text = KikasaStoryTheme.Text;
        public static readonly Color TextDim = KikasaStoryTheme.TextDim;

        public float SwayTimer;
        public float PulseTimer;
        public float ShaderTime;

        //==== 水光未干 ====
        private int _lastVisibleChars = -1;
        private float _wetAge = 60f;

        //==== 选项 hover 缓动(框架是 0/1) ====
        private readonly float[] _optionHover = new float[16];

        private readonly List<RainThread> _threads = [];
        private readonly List<Ripple> _ripples = [];
        private int _threadSpawnTimer;

        public void Update(Rectangle panelRect, bool active, KikasaParticleMode mode = KikasaParticleMode.Dialogue) {
            SwayTimer = SkinAnimUtil.WrapTimer(SwayTimer, 0.020f);
            PulseTimer = SkinAnimUtil.WrapTimer(PulseTimer, 0.028f);
            ShaderTime = SkinAnimUtil.AdvanceShaderTime(ShaderTime);
            _wetAge = Math.Min(_wetAge + 1f, 60f);

            if (!active) {
                return;
            }

            int interval = mode switch {
                KikasaParticleMode.Popup => 26,
                KikasaParticleMode.Choice => 46,   //抉择时刻雨声放轻
                _ => 15,
            };
            int maxCount = mode == KikasaParticleMode.Popup ? 4 : 8;

            _threadSpawnTimer++;
            if (_threadSpawnTimer >= interval && _threads.Count < maxCount) {
                _threadSpawnTimer = 0;
                _threads.Add(new RainThread(panelRect));
            }

            for (int i = _threads.Count - 1; i >= 0; i--) {
                if (_threads[i].Update(panelRect)) {
                    //雨丝落底:溅开一圈涟漪
                    if (_threads[i].Landed && _ripples.Count < 10) {
                        _ripples.Add(new Ripple(_threads[i].Position));
                    }
                    _threads.RemoveAt(i);
                }
            }
            for (int i = _ripples.Count - 1; i >= 0; i--) {
                if (_ripples[i].Update()) {
                    _ripples.RemoveAt(i);
                }
            }
        }

        public void DrawRain(SpriteBatch spriteBatch, float alpha) {
            foreach (RainThread thread in _threads) {
                thread.Draw(spriteBatch, alpha);
            }
            foreach (Ripple ripple in _ripples) {
                ripple.Draw(spriteBatch, alpha);
            }
        }

        /// <summary>喂可见字符数,维护水光年龄</summary>
        public void TrackTypewriter(int visibleChars) {
            if (visibleChars != _lastVisibleChars) {
                _lastVisibleChars = visibleChars;
                _wetAge = 0f;
            }
        }

        /// <summary>水光强度 0~1(打字机尾字未干)</summary>
        public float WetStrength => 1f - MathHelper.Clamp(_wetAge / 16f, 0f, 1f);

        /// <summary>选项 hover 缓动,index 绝对序号</summary>
        public void UpdateOptionHovers(int hoverIndex, int optionCount) {
            int count = Math.Min(optionCount, _optionHover.Length);
            for (int i = 0; i < count; i++) {
                float target = i == hoverIndex ? 1f : 0f;
                float cur = _optionHover[i];
                _optionHover[i] = cur + (target - cur) * (target > cur ? 0.22f : 0.13f);
            }
        }

        public float GetOptionHover(int optionIndex)
            => optionIndex >= 0 && optionIndex < _optionHover.Length ? _optionHover[optionIndex] : 0f;

        public void Reset() {
            SwayTimer = 0f;
            PulseTimer = 0f;
            ShaderTime = 0f;
            _threads.Clear();
            _ripples.Clear();
            _threadSpawnTimer = 0;
            _lastVisibleChars = -1;
            _wetAge = 60f;
            Array.Clear(_optionHover, 0, _optionHover.Length);
        }

        /// <summary>一缕雨丝:两翼窄条内快速坠落,微斜,末端见底即碎</summary>
        private sealed class RainThread
        {
            private Vector2 _pos;
            private readonly Vector2 _vel;
            private readonly float _len;
            private readonly float _thick;
            private float _life;
            private readonly float _maxLife;

            public Vector2 Position => _pos;
            /// <summary>是否落到面板底沿(而非寿命耗尽半途消散)</summary>
            public bool Landed { get; private set; }

            public RainThread(Rectangle panel) {
                bool leftSide = Main.rand.NextBool();
                //两翼窄条 ±14px,不进正文
                float x = leftSide
                    ? Main.rand.NextFloat(panel.X - 14f, panel.X + 16f)
                    : Main.rand.NextFloat(panel.Right - 16f, panel.Right + 14f);
                _pos = new Vector2(x, panel.Y - Main.rand.NextFloat(6f, 18f));
                //雨向着一个统一的微斜角落,速度快过落花一个量级
                _vel = new Vector2(Main.rand.NextFloat(-0.22f, 0.06f), Main.rand.NextFloat(2.6f, 4.1f));
                _len = Main.rand.NextFloat(7f, 14f);
                _thick = Main.rand.NextFloat(0.8f, 1.25f);
                _maxLife = Main.rand.NextFloat(120f, 180f);
            }

            public bool Update(Rectangle panel) {
                _life++;
                _pos += _vel;
                if (_pos.Y >= panel.Bottom - 5f) {
                    Landed = true;
                    return true;
                }
                return _life >= _maxLife;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D pixel = VaultAsset.placeholder2.Value;
                Rectangle src = new(0, 0, 1, 1);
                float fade = MathHelper.Clamp(_life / 8f, 0f, 1f);
                float rot = _vel.ToRotation();
                Vector2 half = new(0.5f, 0.5f);
                //速度拉伸的丝体 + 前端一点湿亮
                sb.Draw(pixel, _pos, src, Rain * (alpha * 0.42f * fade), rot, half, new Vector2(_len, _thick), SpriteEffects.None, 0f);
                sb.Draw(pixel, _pos + _vel.SafeNormalize(Vector2.UnitY) * (_len * 0.42f), src,
                    Moon * (alpha * 0.30f * fade), rot, half, new Vector2(_len * 0.30f, _thick * 0.9f), SpriteEffects.None, 0f);
            }
        }

        /// <summary>溅开的涟漪:压扁细环,快张缓灭</summary>
        private sealed class Ripple
        {
            private readonly Vector2 _pos;
            private float _life;
            private readonly float _maxLife;
            private readonly float _maxRadius;

            public Ripple(Vector2 pos) {
                _pos = pos;
                _maxLife = Main.rand.NextFloat(26f, 38f);
                _maxRadius = Main.rand.NextFloat(7f, 11f);
            }

            public bool Update() {
                _life++;
                return _life >= _maxLife;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                float t = _life / _maxLife;
                float radius = _maxRadius * (1f - (1f - t) * (1f - t)); //easeOut 张开
                float fade = (1f - t) * (1f - t);
                KikasaPanelDraw.DrawRippleRing(sb, _pos, radius, alpha * 0.55f * fade);
            }
        }
    }
}
