using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using static CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.HalibutUIAsset;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    internal class SeaEyeButton
    {
        public int Index;
        public float Angle;
        public bool IsActive;
        public Vector2 Position;
        public bool IsHovered;
        public int? LayerNumber; //激活层数（激活顺序）
        public int LayerNumberDisplay {
            get {
                int num;
                if (IsActive) {
                    num = LayerNumber ?? 1;
                }
                else {
                    num = DomainUI.Instance.ActiveEyeCount + 1;
                }
                return num;
            }
        }

        private float hoverScale = 1f;
        private float glowIntensity = 0f;
        private float blinkTimer = 0f;
        private const float EyeSize = 20f;

        /// <summary>
        /// 判断当前眼睛是否处于死机状态
        /// </summary>
        public bool IsCrashed {
            get {
                if (!Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    return false;
                }
                int crashLevel = halibutPlayer.CrashesLevel();
                return LayerNumberDisplay <= crashLevel;
            }
        }

        public SeaEyeButton(int index, float angle) {
            Index = index;
            Angle = angle;
            IsActive = false;
            LayerNumber = null;
        }

        public void Toggle() {
            IsActive = !IsActive;
            blinkTimer = 15f;
            if (!IsActive) {
                LayerNumber = null;
            }
            PlayEyeDeactivationSound(this);
        }

        /// <summary>
        /// 播放眼睛关闭音效
        /// </summary>
        private static void PlayEyeDeactivationSound(SeaEyeButton eye) {
            bool wasCrashed = eye.IsCrashed;

            if (!wasCrashed) {
                //机械停止音
                SoundEngine.PlaySound(SoundID.Item1 with { //金属音
                    Volume = 0.4f,
                    Pitch = -0.6f,
                    MaxInstances = 3
                });

                //压力释放
                SoundEngine.PlaySound(SoundID.Item54 with { //柔和破裂音
                    Volume = 0.3f,
                    Pitch = 0.1f,
                    MaxInstances = 2
                });
            }
            else {
                //能量消散音
                SoundEngine.PlaySound(SoundID.Item30 with {
                    Volume = 0.35f,
                    Pitch = -0.2f,
                    MaxInstances = 3
                });

                //轻柔的气息音
                SoundEngine.PlaySound(SoundID.Item85 with { //魔法消散音
                    Volume = 0.3f,
                    Pitch = -0.3f,
                    MaxInstances = 2
                });

                //封印音（淡出感）
                SoundEngine.PlaySound(SoundID.Item20 with { //钩爪收回音
                    Volume = 0.25f,
                    Pitch = -0.5f,
                    MaxInstances = 2
                });
            }

            // 通用：轻微的回响消失
            SoundEngine.PlaySound(SoundID.MenuClose with {
                Volume = 0.2f,
                Pitch = -0.4f,
                MaxInstances = 5
            });
        }

        public void Update(Vector2 center, float orbitRadius, float panelAlpha) {
            float wave = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + Index * 0.3f) * 2f;
            Position = center + Angle.ToRotationVector2() * (orbitRadius + wave);
            Rectangle hitbox = new Rectangle((int)(Position.X - EyeSize / 2), (int)(Position.Y - EyeSize / 2), (int)EyeSize, (int)EyeSize);
            IsHovered = hitbox.Contains(Main.MouseScreen.ToPoint()) && panelAlpha >= 1f;
            float targetScale = IsHovered ? 1.2f : 1f;
            hoverScale = MathHelper.Lerp(hoverScale, targetScale, 0.15f);
            float targetGlow = IsActive ? 1f : 0.3f;
            if (IsHovered) {
                targetGlow += 0.3f;
            }
            glowIntensity = MathHelper.Lerp(glowIntensity, targetGlow, 0.1f);
            if (blinkTimer > 0f) {
                blinkTimer--;
            }
        }

        public void Draw(SpriteBatch spriteBatch, float alpha) {
            if (SeaEye == null) {
                return;
            }

            int frameHeight = SeaEye.Height / 4; //现在是4帧
            bool shouldBlink = blinkTimer > 0f && blinkTimer % 10 < 5;

            //判断使用哪组帧
            bool isCrashed = IsCrashed;
            int baseFrame = isCrashed ? 2 : 0; //死机使用帧2-3，正常使用帧0-1

            //选择睁眼或闭眼帧
            int frame = (IsActive && !shouldBlink) ? (baseFrame + 1) : baseFrame;

            Rectangle sourceRect = new Rectangle(0, frame * frameHeight, SeaEye.Width, frameHeight);
            Vector2 drawPos = Position;
            Vector2 origin = new Vector2(SeaEye.Width / 2, frameHeight / 2);
            float scale = (EyeSize / SeaEye.Width) * hoverScale;

            if (IsActive) {
                //死机状态使用不同的发光颜色
                Color glowColor = isCrashed
                    ? new Color(255, 80, 80) * (alpha * glowIntensity * 0.5f) //红色发光
                    : new Color(100, 220, 255) * (alpha * glowIntensity * 0.5f); //正常蓝色发光

                float glowPulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + Index) * 0.3f + 0.7f;
                spriteBatch.Draw(SeaEye, drawPos, sourceRect, glowColor * glowPulse, 0f, origin, scale * 1.25f, SpriteEffects.None, 0f);
            }

            //死机状态的眼睛颜色偏暗红
            Color eyeColor = IsActive ? Color.White : new Color(100, 100, 120);
            if (isCrashed && IsActive) {
                eyeColor = Color.Lerp(eyeColor, new Color(255, 100, 100), 0.5f); //混合红色
            }

            eyeColor *= alpha * glowIntensity;
            spriteBatch.Draw(SeaEye, drawPos, sourceRect, eyeColor, 0f, origin, scale, SpriteEffects.None, 0f);
        }
    }

    internal class DomainRing
    {
        public Vector2 Center;
        public float TargetRadius;
        public float CurrentRadius;
        public int LayerIndex;
        public float Alpha;
        public bool ShouldRemove;
        private float rotation = 0f;
        private float spawnProgress = 0f;
        private const float SpawnDuration = 30f;
        public DomainRing(Vector2 center, float radius, int index) {
            Center = center;
            TargetRadius = radius;
            CurrentRadius = 0f;
            LayerIndex = index;
            Alpha = 0f;
            ShouldRemove = false;
        }
        public void Update() {
            if (spawnProgress < 1f) {
                spawnProgress += 1f / SpawnDuration;
                spawnProgress = Math.Clamp(spawnProgress, 0f, 1f);
                float easedProgress = EaseOutCubic(spawnProgress);
                CurrentRadius = TargetRadius * easedProgress;
                Alpha = easedProgress;
            }
            else {
                CurrentRadius = TargetRadius;
                Alpha = 1f;
            }
            rotation += 0.005f * (1f + LayerIndex * 0.1f);
        }
        private static float EaseOutCubic(float t) {
            return 1f - (float)Math.Pow(1f - t, 3);
        }
        public void Draw(SpriteBatch spriteBatch, float panelAlpha) {
            if (Alpha < 0.01f) {
                return;
            }
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            int segments = 48;
            float angleStep = MathHelper.TwoPi / segments;
            float colorProgress = LayerIndex / 9f;
            Color baseColor = Color.Lerp(new Color(80, 180, 255), new Color(120, 220, 255), colorProgress);
            for (int i = 0; i < segments; i++) {
                float angle1 = i * angleStep + rotation;
                float angle2 = (i + 1) * angleStep + rotation;
                float wave1 = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + angle1 * 2f) * 2f;
                float wave2 = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + angle2 * 2f) * 2f;
                Vector2 p1 = Center + angle1.ToRotationVector2() * (CurrentRadius + wave1);
                Vector2 p2 = Center + angle2.ToRotationVector2() * (CurrentRadius + wave2);
                Vector2 diff = p2 - p1;
                float length = diff.Length();
                float segRotation = diff.ToRotation();
                float brightness = 0.6f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + angle1 * 3f) * 0.3f;
                Color segColor = baseColor * brightness * Alpha * panelAlpha * 0.6f;
                spriteBatch.Draw(pixel, p1, new Rectangle(0, 0, 1, 1), segColor, segRotation, Vector2.Zero, new Vector2(length, 1.5f), SpriteEffects.None, 0f);
            }
        }
    }

    internal class EyeParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;
        public float MaxLife;
        public float Scale;
        public float Rotation;
        public Color Color;
        public EyeParticle(Vector2 pos, Vector2 vel, Color color) {
            Position = pos;
            Velocity = vel;
            Life = 0f;
            MaxLife = Main.rand.NextFloat(30f, 50f);
            Scale = Main.rand.NextFloat(0.5f, 1f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            Color = color;
        }
        public void Update() {
            Life++;
            Position += Velocity;
            Velocity *= 0.95f;
            Rotation += 0.05f;
        }
        public void Draw(SpriteBatch spriteBatch, float panelAlpha) {
            float progress = Life / MaxLife;
            float alpha = (1f - progress) * panelAlpha * 0.7f;
            Texture2D tex = TextureAssets.Extra[ExtrasID.SharpTears].Value;
            Color drawColor = Color * alpha;
            spriteBatch.Draw(tex, Position, null, drawColor, Rotation, tex.Size() / 2, Scale * (0.3f + progress * 0.2f), SpriteEffects.None, 0f);
        }
    }

    internal class EyeActivationAnimation
    {
        private Vector2 startPos;
        private float progress;
        private const float Duration = 25f;
        private int eyeLayerNumber; //记录这个眼睛的层数

        public bool Finished { get; private set; }

        public EyeActivationAnimation(Vector2 start, int layerNumber = 1) {
            startPos = start;
            progress = 0f;
            Finished = false;
            eyeLayerNumber = layerNumber;
        }

        public void Update(Vector2 target) {
            if (Finished) {
                return;
            }
            progress += 1f / Duration;
            progress = Math.Clamp(progress, 0f, 1f);
            if (progress >= 1f) {
                Finished = true;
            }
        }

        public void Draw(SpriteBatch spriteBatch, float alpha) {
            if (Finished) {
                return;
            }
            if (SeaEye == null) {
                return;
            }

            Texture2D tex = SeaEye;
            int frameHeight = tex.Height / 4; //现在是4帧

            //判断是否死机
            bool isCrashed = false;
            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                int crashLevel = halibutPlayer.CrashesLevel();
                isCrashed = eyeLayerNumber <= crashLevel;
            }

            //使用对应的睁眼帧
            int baseFrame = isCrashed ? 2 : 0;
            int frame = baseFrame + 1; //使用睁眼帧

            Rectangle sourceRect = new Rectangle(0, frame * frameHeight, tex.Width, frameHeight);
            Vector2 center = DomainUI.Instance.halibutCenter;
            Vector2 pos = Vector2.Lerp(startPos, center, EaseOut(progress));
            float scale = MathHelper.Lerp(0.8f, 1.8f, EaseOutBack(progress));
            float fade = 1f - Math.Abs(progress - 0.5f) * 2f;
            Color color = Color.White * (alpha * fade);
            Vector2 origin = new Vector2(tex.Width / 2, frameHeight / 2);
            spriteBatch.Draw(tex, pos, sourceRect, color, 0f, origin, scale * 0.4f, SpriteEffects.None, 0f);

            //根据死机状态使用不同的光环颜色
            Color ringColor = isCrashed
                ? new Color(255, 100, 100) * (alpha * fade * 0.5f) //红色光环
                : new Color(120, 220, 255) * (alpha * fade * 0.5f); //蓝色光环

            DrawPulseRing(spriteBatch, pos, scale * 22f, ringColor, 2f);
        }

        private static float EaseOut(float t) {
            return 1f - (float)Math.Pow(1f - t, 3f);
        }

        private static float EaseOutBack(float t) {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * (float)Math.Pow(t - 1, 3) + c1 * (float)Math.Pow(t - 1, 2);
        }

        private void DrawPulseRing(SpriteBatch spriteBatch, Vector2 center, float radius, Color color, float thickness) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            int segs = 40;
            float step = MathHelper.TwoPi / segs;
            for (int i = 0; i < segs; i++) {
                float a1 = i * step;
                float a2 = (i + 1) * step;
                Vector2 p1 = center + a1.ToRotationVector2() * radius;
                Vector2 p2 = center + a2.ToRotationVector2() * radius;
                Vector2 diff = p2 - p1;
                float len = diff.Length();
                float rot = diff.ToRotation();
                spriteBatch.Draw(pixel, p1, new Rectangle(0, 0, 1, 1), color, rot, Vector2.Zero, new Vector2(len, thickness), SpriteEffects.None, 0f);
            }
        }
    }

    internal class HalibutPulseEffect
    {
        private float progress;
        private const float Duration = 35f;
        private Vector2 center;
        public bool Finished => progress >= 1f;
        public HalibutPulseEffect(Vector2 c) {
            center = c;
            progress = 0f;
        }
        public void Update() {
            if (Finished) {
                return;
            }
            progress += 1f / Duration;
            progress = Math.Clamp(progress, 0f, 1f);
        }
        public void Draw(SpriteBatch spriteBatch, float alpha) {
            if (Finished) {
                return;
            }
            float eased = 1f - (float)Math.Pow(1f - progress, 3f);
            float radius = MathHelper.Lerp(18f, 80f, eased);
            float fade = 1f - eased;
            Color col = new Color(110, 210, 255) * (alpha * fade * 0.6f);
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            int segs = 64;
            float step = MathHelper.TwoPi / segs;
            for (int i = 0; i < segs; i++) {
                float a1 = i * step;
                float a2 = (i + 1) * step;
                Vector2 p1 = center + a1.ToRotationVector2() * radius;
                Vector2 p2 = center + a2.ToRotationVector2() * radius;
                Vector2 diff = p2 - p1;
                float len = diff.Length();
                float rot = diff.ToRotation();
                spriteBatch.Draw(pixel, p1, new Rectangle(0, 0, 1, 1), col, rot, Vector2.Zero, new Vector2(len, 2f), SpriteEffects.None, 0f);
            }
        }
    }

    internal static class DomainEyeDescriptions
    {
        public static string GetDescription(int layer) {
            if (layer >= 1 && layer < DomainUI.EyeLayerDescriptions.Length) {
                var lt = DomainUI.EyeLayerDescriptions[layer];
                if (lt != null) {
                    return lt.Value;
                }
            }
            return "Error";
        }
    }

    /// <summary>
    /// 第十只中心额外之眼
    /// </summary>
    internal class ExtraSeaEyeButton
    {
        public bool IsActive;
        public bool IsHovered;
        private float hoverScale = 1f;
        private float glowIntensity = 0f;
        private float blinkTimer = 0f;
        private const float EyeSize = 30f; //稍大

        public int LayerNumberDisplay => 10;

        public bool IsCrashed {
            get {
                if (!Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    return false;
                }
                int crashLevel = halibutPlayer.CrashesLevel();
                return LayerNumberDisplay <= crashLevel;
            }
        }

        public void ForceClose() {
            if (IsActive) {
                Toggle();
            }
        }

        public void Toggle() {
            IsActive = !IsActive;
            blinkTimer = 18f;
            PlayToggleSound();
        }

        private void PlayToggleSound() {
            if (IsActive) {
                SoundEngine.PlaySound(SoundID.Item29 with {
                    Volume = 0.5f,
                    Pitch = 0.1f
                });
                SoundEngine.PlaySound(SoundID.MaxMana with {
                    Volume = 0.4f,
                    Pitch = -0.2f
                });
            }
            else {
                SoundEngine.PlaySound(SoundID.MenuClose with {
                    Volume = 0.35f,
                    Pitch = -0.5f
                });
            }
        }

        public void Update(Vector2 center, bool canShow, float panelAlpha) {
            if (!canShow) {
                IsHovered = false;
                hoverScale = MathHelper.Lerp(hoverScale, 0.85f, 0.2f);
                glowIntensity = MathHelper.Lerp(glowIntensity, 0f, 0.2f);
                return;
            }

            Rectangle hitbox = new Rectangle((int)(center.X - EyeSize / 2), (int)(center.Y - EyeSize / 2), (int)EyeSize, (int)EyeSize);
            IsHovered = hitbox.Contains(Main.MouseScreen.ToPoint()) && panelAlpha >= 1f;

            float targetScale = IsHovered ? 1.25f : 1.05f;
            if (IsActive) {
                targetScale += 0.05f;
            }
            hoverScale = MathHelper.Lerp(hoverScale, targetScale, 0.15f);

            float targetGlow = IsActive ? 1f : 0.45f;
            if (IsHovered) {
                targetGlow += 0.35f;
            }
            glowIntensity = MathHelper.Lerp(glowIntensity, targetGlow, 0.1f);
            if (blinkTimer > 0f) {
                blinkTimer--;
            }
        }

        public void Draw(SpriteBatch spriteBatch, Vector2 center, float alpha) {
            if (SeaEye == null) {
                return;
            }
            int frameHeight = SeaEye.Height / 4;
            bool shouldBlink = blinkTimer > 0f && blinkTimer % 10 < 5;
            bool isCrashed = IsCrashed;
            int baseFrame = isCrashed ? 2 : 0;
            int frame = (IsActive && !shouldBlink) ? (baseFrame + 1) : baseFrame;
            Rectangle sourceRect = new Rectangle(0, frame * frameHeight, SeaEye.Width, frameHeight);
            Vector2 origin = new Vector2(SeaEye.Width / 2, frameHeight / 2);
            float scale = (EyeSize / SeaEye.Width) * hoverScale;

            if (IsActive) {
                float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.25f + 0.75f;
                Color haloColor;
                if (isCrashed) {
                    haloColor = new Color(255, 90, 90) * (alpha * glowIntensity * 0.5f * pulse);
                }
                else {
                    haloColor = new Color(120, 230, 255) * (alpha * glowIntensity * 0.5f * pulse);
                }
                for (int i = 0; i < 3; i++) {
                    float s = scale * (1.35f + i * 0.15f);
                    Color ring = haloColor * (0.6f - i * 0.18f);
                    spriteBatch.Draw(SeaEye, center, sourceRect, ring, 0f, origin, s, SpriteEffects.None, 0f);
                }
            }

            Color eyeColor = IsActive ? Color.White : new Color(120, 140, 160);
            if (isCrashed && IsActive) {
                eyeColor = Color.Lerp(eyeColor, new Color(255, 110, 110), 0.55f);
            }
            eyeColor *= alpha * (0.8f + glowIntensity * 0.2f);
            spriteBatch.Draw(SeaEye, center, sourceRect, eyeColor, 0f, origin, scale, SpriteEffects.None, 0f);
        }
    }
}
