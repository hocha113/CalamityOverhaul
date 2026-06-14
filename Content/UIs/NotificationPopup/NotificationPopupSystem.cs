using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;

namespace CalamityOverhaul.Content.UIs.NotificationPopup
{
    /// <summary>
    /// 通用弹窗通知系统，屏幕右侧滑入式弹窗管理器，
    /// 各模块通过 <see cref="Add"/> 注册自定义 <see cref="NotificationEntry"/> 即可显示弹窗
    /// </summary>
    internal class NotificationPopupSystem : UIHandle
    {
        private class ActiveEntry
        {
            public NotificationEntry Entry;
            public int Timer;
            public float CurrentY;
            public float ScaleAnim; //加入缩放动画状态

            public int TotalLifetime => Entry.SlideTime * 2 + Entry.DisplayTime;
        }

        public override bool Active => VaultLoad.LoadenContent;

        public static NotificationPopupSystem Instance
            => UIHandleLoader.GetUIHandleOfType<NotificationPopupSystem>();

        private static readonly Queue<NotificationEntry> _pending = new();
        private static readonly List<ActiveEntry> _active = new();

        /// <summary>同时在屏幕上显示的最大弹窗数</summary>
        private const int MaxActive = 6;

        /// <summary>弹窗堆叠起始位置（屏幕高度比例）</summary>
        private const float StartYRatio = 0.22f;

        /// <summary>添加一条弹窗通知到队列</summary>
        public static void Add(NotificationEntry entry) {
            _pending.Enqueue(entry);
        }

        public override void LogicUpdate() {
            //从队列中取出新弹窗
            if (_active.Count < MaxActive && _pending.Count > 0) {
                var entry = _pending.Dequeue();
                float targetY = GetTargetY(_active.Count);
                _active.Add(new ActiveEntry {
                    Entry = entry,
                    Timer = 0,
                    CurrentY = targetY,
                    ScaleAnim = 0f
                });

                SoundStyle sound = entry.AppearSound ?? CWRSound.Rollout with { Volume = 0.5f };
                SoundEngine.PlaySound(sound);
            }

            //更新现有弹窗
            for (int i = _active.Count - 1; i >= 0; i--) {
                var note = _active[i];
                note.Timer++;
                note.Entry.LifeTimer = note.Timer;

                //Y轴平滑堆叠（加速跟踪）
                float targetY = GetTargetY(i);
                note.CurrentY = MathHelper.Lerp(note.CurrentY, targetY, 0.14f);

                //缩放平滑
                float targetScale = (note.Timer < note.Entry.SlideTime + note.Entry.DisplayTime) ? 1f : 0f;
                note.ScaleAnim = MathHelper.Lerp(note.ScaleAnim, targetScale, 0.12f);

                //生命周期结束
                if (note.Timer >= note.TotalLifetime) {
                    _active.RemoveAt(i);
                }
            }
        }

        public override void Update() {
            for (int i = _active.Count - 1; i >= 0; i--) {
                var note = _active[i];
                float progress = GetProgress(note);
                float scale = MathHelper.Clamp(note.ScaleAnim, 0f, 1f);
                if (progress <= 0f || scale < 0.05f) continue;
                float w = note.Entry.Width;
                float h = note.Entry.Height;
                float x = Main.screenWidth - w * progress;
                float yCenter = note.CurrentY + h / 2f;
                float scaledW = w * scale;
                float scaledH = h * scale;
                Rectangle rect = new(
                    (int)(x + (w - scaledW) / 2f),
                    (int)(yCenter - scaledH / 2f),
                    (int)scaledW,
                    (int)scaledH);

                if (rect.Contains(Main.MouseScreen.ToPoint())) {
                    Main.LocalPlayer.mouseInterface = true;
                    if (keyLeftPressState == KeyPressState.Pressed) {
                        Main.mouseLeftRelease = false;
                        if (note.Entry.OnClick()) {
                            //提前进入滑出阶段
                            int slideOutStart = note.Entry.SlideTime + note.Entry.DisplayTime;
                            if (note.Timer < slideOutStart)
                                note.Timer = slideOutStart;
                        }
                    }
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            foreach (var note in _active) {
                float progress = GetProgress(note);
                if (progress <= 0f) continue;

                float scale = MathHelper.Clamp(note.ScaleAnim, 0f, 1f);
                if (scale < 0.05f) continue;
                float w = note.Entry.Width;
                float h = note.Entry.Height;

                float x = Main.screenWidth - w * progress;
                float yCenter = note.CurrentY + h / 2f;

                //按中心点计算缩放后的绘制矩形
                float scaledW = w * scale;
                float scaledH = h * scale;
                Rectangle panelRect = new(
                    (int)(x + (w - scaledW) / 2f),
                    (int)(yCenter - scaledH / 2f),
                    (int)scaledW,
                    (int)scaledH);

                //alpha跟随滑入滑出衰减
                float alpha = MathHelper.Clamp(progress, 0f, 1f) * scale;
                note.Entry.DrawContent(spriteBatch, panelRect, alpha);
            }
        }

        /// <summary>计算第 index 条弹窗的目标 Y（考虑不同弹窗高度）</summary>
        private static float GetTargetY(int index) {
            float y = Main.screenHeight * StartYRatio;
            for (int i = 0; i < index && i < _active.Count; i++) {
                y += _active[i].Entry.Height + _active[i].Entry.Gap;
            }
            return y;
        }

        /// <summary>根据计时器计算滑入/滑出进度（0~1），使用弹性缓动</summary>
        private static float GetProgress(ActiveEntry note) {
            int timer = note.Timer;
            int slide = note.Entry.SlideTime;
            int display = note.Entry.DisplayTime;

            if (timer < slide) {
                //滑入阶段，弹性ease-out带轻微过冲
                float p = timer / (float)slide;
                return EaseOutBack(p);
            }
            else if (timer > slide + display) {
                //滑出阶段，加速淡出
                float p = 1f - (timer - slide - display) / (float)slide;
                return EaseInCubic(p);
            }
            return 1f;
        }

        /// <summary>带回弹的ease-out（轻微过冲）</summary>
        private static float EaseOutBack(float t) {
            const float c1 = 1.3f; //过冲系数，较温和
            const float c3 = c1 + 1f;
            float t1 = t - 1f;
            return 1f + c3 * t1 * t1 * t1 + c1 * t1 * t1;
        }

        /// <summary>三次方ease-in</summary>
        private static float EaseInCubic(float t) {
            return t * t * t;
        }
    }
}
