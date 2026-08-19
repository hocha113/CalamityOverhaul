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

    /// <summary>雨丝住边框带不进正文:两翼细丝坠底溅积水涟漪,顶沿细丝落檐溅小圈(伞面承雨)</summary>
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
        private int _topSpawnTimer;

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
            //顶沿承雨:短程细丝砸在檐口。弹窗顶心已有悬珠,不加顶雨
            int topInterval = mode switch {
                KikasaParticleMode.Popup => 0,
                KikasaParticleMode.Choice => 30,
                _ => 9,
            };
            int topMax = mode == KikasaParticleMode.Choice ? 3 : 6;

            int flankCount = 0;
            int topCount = 0;
            foreach (RainThread thread in _threads) {
                if (thread.IsTop) {
                    topCount++;
                }
                else {
                    flankCount++;
                }
            }

            _threadSpawnTimer++;
            if (_threadSpawnTimer >= interval && flankCount < maxCount) {
                _threadSpawnTimer = 0;
                _threads.Add(RainThread.SpawnFlank(panelRect));
            }
            if (topInterval > 0) {
                _topSpawnTimer++;
                if (_topSpawnTimer >= topInterval && topCount < topMax) {
                    _topSpawnTimer = 0;
                    _threads.Add(RainThread.SpawnTop(panelRect));
                }
            }

            for (int i = _threads.Count - 1; i >= 0; i--) {
                if (_threads[i].Update(panelRect)) {
                    //雨丝落到承接面:顶沿溅檐口小圈,两翼溅积水大圈
                    if (_threads[i].Landed && _ripples.Count < 14) {
                        _ripples.Add(_threads[i].IsTop ? Ripple.Rim(_threads[i].Position) : Ripple.Pool(_threads[i].Position));
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
            _topSpawnTimer = 0;
            _lastVisibleChars = -1;
            _wetAge = 60f;
            Array.Clear(_optionHover, 0, _optionHover.Length);
        }

        /// <summary>一缕雨丝:细丝快速坠落,微斜;两翼长程见底即碎,顶沿短程砸檐即碎</summary>
        private sealed class RainThread
        {
            private Vector2 _pos;
            private readonly Vector2 _vel;
            private readonly float _len;
            private readonly float _thick;
            private readonly float _landJitter;
            private float _life;
            private readonly float _maxLife;

            public Vector2 Position => _pos;
            /// <summary>顶沿雨(落檐口)还是两翼雨(落底沿)</summary>
            public bool IsTop { get; }
            /// <summary>是否落到承接面(而非寿命耗尽半途消散)</summary>
            public bool Landed { get; private set; }

            private RainThread(Vector2 pos, bool isTop) {
                _pos = pos;
                IsTop = isTop;
                //雨向着一个统一的微斜角落,速度快过落花一个量级
                _vel = new Vector2(Main.rand.NextFloat(-0.22f, 0.06f), Main.rand.NextFloat(2.6f, 4.1f));
                _len = Main.rand.NextFloat(7f, 14f);
                _thick = Main.rand.NextFloat(0.8f, 1.25f);
                _landJitter = Main.rand.NextFloat(-1f, 3f); //檐口水蚀不平,落点上下错开
                _maxLife = Main.rand.NextFloat(120f, 180f);
            }

            /// <summary>两翼窄条 ±14px,不进正文</summary>
            public static RainThread SpawnFlank(Rectangle panel) {
                bool leftSide = Main.rand.NextBool();
                float x = leftSide
                    ? Main.rand.NextFloat(panel.X - 14f, panel.X + 16f)
                    : Main.rand.NextFloat(panel.Right - 16f, panel.Right + 14f);
                return new RainThread(new Vector2(x, panel.Y - Main.rand.NextFloat(6f, 18f)), false);
            }

            /// <summary>顶沿:自面板上方坠向檐口,避开圆角</summary>
            public static RainThread SpawnTop(Rectangle panel) {
                float x = Main.rand.NextFloat(panel.X + 18f, panel.Right - 18f);
                return new RainThread(new Vector2(x, panel.Y - Main.rand.NextFloat(34f, 80f)), true);
            }

            public bool Update(Rectangle panel) {
                _life++;
                _pos += _vel;
                float landY = IsTop ? panel.Y + _landJitter : panel.Bottom - 5f;
                if (_pos.Y >= landY) {
                    if (IsTop) {
                        _pos.Y = landY; //钉回檐口,涟漪坐在框线上
                    }
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

        /// <summary>溅开的涟漪:压扁细环,快张缓灭。底沿积水面大圈,顶沿檐口小圈</summary>
        private sealed class Ripple
        {
            private readonly Vector2 _pos;
            private readonly float _alphaMul;
            private float _life;
            private readonly float _maxLife;
            private readonly float _maxRadius;

            private Ripple(Vector2 pos, float maxLife, float maxRadius, float alphaMul) {
                _pos = pos;
                _maxLife = maxLife;
                _maxRadius = maxRadius;
                _alphaMul = alphaMul;
            }

            /// <summary>底沿积水面的涟漪</summary>
            public static Ripple Pool(Vector2 pos)
                => new(pos, Main.rand.NextFloat(26f, 38f), Main.rand.NextFloat(7f, 11f), 1f);

            /// <summary>檐口溅圈:硬承接面,小一号收得快</summary>
            public static Ripple Rim(Vector2 pos)
                => new(pos, Main.rand.NextFloat(22f, 32f), Main.rand.NextFloat(4.5f, 7.5f), 0.85f);

            public bool Update() {
                _life++;
                return _life >= _maxLife;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                float t = _life / _maxLife;
                float radius = _maxRadius * (1f - (1f - t) * (1f - t)); //easeOut 张开
                float fade = (1f - t) * (1f - t);
                KikasaPanelDraw.DrawRippleRing(sb, _pos, radius, alpha * 0.55f * _alphaMul * fade);
            }
        }
    }
}
