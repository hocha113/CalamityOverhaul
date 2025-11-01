using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV
{
    /// <summary>
    /// �ɾ���ʾ��ϵͳ������Ļ��൯����ʾ
    /// </summary>
    internal class AchievementToast : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.HalibutText.ADV";

        public static AchievementToast Instance => UIHandleLoader.GetUIHandleOfType<AchievementToast>();

        /// <summary>
        /// �ɾ���ʾ���ö��
        /// </summary>
        public enum ToastStyle
        {
            Ocean,      //������
            Brimstone   //��ǻ���
        }

        /// <summary>
        /// �ɾ�������
        /// </summary>
        public class Achievement
        {
            public int IconItemID { get; set; } = ItemID.None;
            public Texture2D CustomIcon { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public ToastStyle Style { get; set; } = ToastStyle.Ocean;
            public Action OnComplete { get; set; }
        }

        #region �����ֶ�
        private readonly Queue<Achievement> achievementQueue = new();
        private Achievement currentAchievement;

        //����״̬��
        private enum AnimationState
        {
            SlideIn,    //����
            Hold,       //ͣ��չʾ
            Celebrate,  //��ף����
            SlideOut    //����
        }
        private AnimationState currentState = AnimationState.SlideIn;
        private int stateTimer = 0;

        //��������
        private const int SlideInDuration = 30;      //����ʱ��(֡)
        private const int HoldDuration = 60;        //ͣ��ʱ��(֡)
        private const int CelebrateDuration = 45;    //��ף����ʱ��(֡)
        private const int SlideOutDuration = 25;     //����ʱ��(֡)

        private float slideProgress = 0f;            //�������� 0-1
        private float celebrateScale = 1f;           //��ף����
        private float alpha = 1f;                    //͸����

        //������
        private const float PanelWidth = 320f;
        private const float PanelHeight = 100f;
        private const float OffscreenX = -PanelWidth - 50f;  //��Ļ�����λ��
        private const float OnscreenX = 20f;                 //��Ļ����ʾλ��
        private float ScreenY => Main.screenHeight / 2f - PanelHeight / 2f; //��ֱ����

        //����ϵͳ
        private readonly List<CelebrationParticle> particles = new();
        private int particleSpawnTimer = 0;

        //��񶯻����� - ����
        private float oceanWavePhase = 0f;
        private float oceanPulse = 0f;
        private readonly List<MiniStar> miniStars = new();

        //��񶯻����� - ��ǻ�
        private float flameTimer = 0f;
        private float emberGlowTimer = 0f;
        private readonly List<MiniEmber> miniEmbers = new();

        //���ػ��ı�
        protected static LocalizedText AchievementUnlocked;
        #endregion

        public override bool Active => currentAchievement != null || achievementQueue.Count > 0 || slideProgress > 0.01f;

        public override void SetStaticDefaults() {
            AchievementUnlocked = this.GetLocalization(nameof(AchievementUnlocked), () => "�ɾʹ��!");
        }

        #region ����API
        ///<summary>
        ///��ʾһ���ɾ���ʾ
        ///</summary>
        public static void ShowAchievement(int iconItemID, string title, string description, ToastStyle style = ToastStyle.Ocean, Action onComplete = null) {
            var achievement = new Achievement {
                IconItemID = iconItemID,
                Title = title,
                Description = description,
                Style = style,
                OnComplete = onComplete
            };
            Instance.achievementQueue.Enqueue(achievement);
        }

        ///<summary>
        ///��ʾ�Զ���ͼ��ĳɾ���ʾ
        ///</summary>
        public static void ShowAchievement(Texture2D customIcon, string title, string description, ToastStyle style = ToastStyle.Ocean, Action onComplete = null) {
            var achievement = new Achievement {
                CustomIcon = customIcon,
                Title = title,
                Description = description,
                Style = style,
                OnComplete = onComplete
            };
            Instance.achievementQueue.Enqueue(achievement);
        }
        #endregion

        #region �����߼�
        public override void Update() {
            //������ʱ������
            oceanWavePhase += 0.025f;
            oceanPulse += 0.015f;
            flameTimer += 0.05f;
            emberGlowTimer += 0.04f;

            if (oceanWavePhase > MathHelper.TwoPi) oceanWavePhase -= MathHelper.TwoPi;
            if (oceanPulse > MathHelper.TwoPi) oceanPulse -= MathHelper.TwoPi;
            if (flameTimer > MathHelper.TwoPi) flameTimer -= MathHelper.TwoPi;
            if (emberGlowTimer > MathHelper.TwoPi) emberGlowTimer -= MathHelper.TwoPi;

            //���û�е�ǰ�ɾ͵������У���ʼ��һ��
            if (currentAchievement == null && achievementQueue.Count > 0) {
                StartNext();
                return;
            }

            if (currentAchievement == null) {
                return;
            }

            UpdateAnimation();
            UpdateParticles();
            UpdateStyleParticles();
        }

        private void StartNext() {
            currentAchievement = achievementQueue.Dequeue();
            currentState = AnimationState.SlideIn;
            stateTimer = 0;
            slideProgress = 0f;
            alpha = 1f;
            particles.Clear();
            miniStars.Clear();
            miniEmbers.Clear();

            //���ųɾ���Ч
            SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Volume = 0.6f, Pitch = 0.3f });
        }

        private void UpdateAnimation() {
            stateTimer++;

            switch (currentState) {
                case AnimationState.SlideIn:
                    UpdateSlideIn();
                    break;
                case AnimationState.Hold:
                    UpdateHold();
                    break;
                case AnimationState.Celebrate:
                    UpdateCelebrate();
                    break;
                case AnimationState.SlideOut:
                    UpdateSlideOut();
                    break;
            }
        }

        private void UpdateSlideIn() {
            float t = stateTimer / (float)SlideInDuration;
            t = CWRUtils.EaseOutCubic(t);
            slideProgress = t;

            if (stateTimer >= SlideInDuration) {
                currentState = AnimationState.Hold;
                stateTimer = 0;
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.4f, Pitch = -0.1f });
            }
        }

        private void UpdateHold() {
            if (stateTimer >= HoldDuration) {
                currentState = AnimationState.Celebrate;
                stateTimer = 0;
            }
        }

        private void UpdateCelebrate() {
            float t = stateTimer / (float)CelebrateDuration;
            float pulse = (float)Math.Sin(t * MathHelper.TwoPi * 2f);
            celebrateScale = 1f + pulse * 0.08f;

            //������ף����
            particleSpawnTimer++;
            if (particleSpawnTimer >= 3) {
                particleSpawnTimer = 0;
                Vector2 panelCenter = GetCurrentPanelCenter();
                particles.Add(new CelebrationParticle(panelCenter, currentAchievement.Style));
            }

            if (stateTimer >= CelebrateDuration) {
                currentState = AnimationState.SlideOut;
                stateTimer = 0;
                celebrateScale = 1f;
            }
        }

        private void UpdateSlideOut() {
            float t = stateTimer / (float)SlideOutDuration;
            t = CWRUtils.EaseInCubic(t);
            slideProgress = 1f - t;
            alpha = 1f - t * 0.5f;

            if (stateTimer >= SlideOutDuration) {
                currentAchievement?.OnComplete?.Invoke();
                currentAchievement = null;
                currentState = AnimationState.SlideIn;
                stateTimer = 0;
            }
        }

        private void UpdateParticles() {
            for (int i = particles.Count - 1; i >= 0; i--) {
                if (particles[i].Update()) {
                    particles.RemoveAt(i);
                }
            }
        }

        private void UpdateStyleParticles() {
            if (currentAchievement == null) return;

            Vector2 panelCenter = GetCurrentPanelCenter();

            if (currentAchievement.Style == ToastStyle.Ocean) {
                //������С����
                if (currentState == AnimationState.Celebrate && Main.rand.NextBool(4)) {
                    miniStars.Add(new MiniStar(panelCenter + new Vector2(Main.rand.NextFloat(-140f, 140f), Main.rand.NextFloat(-40f, 40f))));
                }
                for (int i = miniStars.Count - 1; i >= 0; i--) {
                    if (miniStars[i].Update()) {
                        miniStars.RemoveAt(i);
                    }
                }
            }
            else if (currentAchievement.Style == ToastStyle.Brimstone) {
                //��ǻ���С���
                if (currentState == AnimationState.Celebrate && Main.rand.NextBool(3)) {
                    miniEmbers.Add(new MiniEmber(panelCenter + new Vector2(Main.rand.NextFloat(-140f, 140f), 45f)));
                }
                for (int i = miniEmbers.Count - 1; i >= 0; i--) {
                    if (miniEmbers[i].Update()) {
                        miniEmbers.RemoveAt(i);
                    }
                }
            }
        }

        private Vector2 GetCurrentPanelCenter() {
            float x = MathHelper.Lerp(OffscreenX, OnscreenX, slideProgress) + PanelWidth / 2f;
            return new Vector2(x, ScreenY + PanelHeight / 2f);
        }
        #endregion

        #region �����߼�
        public override void Draw(SpriteBatch spriteBatch) {
            if (currentAchievement == null || slideProgress <= 0.01f) return;

            float x = MathHelper.Lerp(OffscreenX, OnscreenX, slideProgress);
            Vector2 panelPos = new Vector2(x, ScreenY);
            Rectangle panelRect = new Rectangle((int)panelPos.X, (int)panelPos.Y, (int)(PanelWidth * celebrateScale), (int)(PanelHeight * celebrateScale));

            //���ݷ�����
            if (currentAchievement.Style == ToastStyle.Ocean) {
                DrawOceanStyle(spriteBatch, panelRect);
            }
            else {
                DrawBrimstoneStyle(spriteBatch, panelRect);
            }

            //��������
            DrawContent(spriteBatch, panelRect);

            //��������
            foreach (var particle in particles) {
                particle.Draw(spriteBatch, alpha);
            }
            foreach (var star in miniStars) {
                star.Draw(spriteBatch, alpha * 0.8f);
            }
            foreach (var ember in miniEmbers) {
                ember.Draw(spriteBatch, alpha * 0.9f);
            }
        }

        #region ���������
        private void DrawOceanStyle(SpriteBatch spriteBatch, Rectangle rect) {
            Texture2D px = VaultAsset.placeholder2.Value;

            //���䱳��
            int segments = 20;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                Rectangle r = new(rect.X, y1, rect.Width, Math.Max(1, y2 - y1));

                Color deep = new Color(5, 20, 35);
                Color mid = new Color(10, 50, 80);
                Color bright = new Color(20, 100, 140);

                float wave = (float)Math.Sin(oceanPulse + t * 2f) * 0.5f + 0.5f;
                Color c = Color.Lerp(Color.Lerp(deep, mid, wave), bright, t * 0.6f);
                c *= alpha;

                spriteBatch.Draw(px, r, new Rectangle(0, 0, 1, 1), c);
            }

            //������
            DrawOceanWaves(spriteBatch, rect);

            //�߿�
            Color edge = new Color(70, 180, 230) * alpha;
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.7f);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            spriteBatch.Draw(px, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);

            //����װ��
            DrawOceanCorner(spriteBatch, new Vector2(rect.X + 10, rect.Y + 10), alpha * 0.9f);
            DrawOceanCorner(spriteBatch, new Vector2(rect.Right - 10, rect.Y + 10), alpha * 0.9f);
        }

        private void DrawOceanWaves(SpriteBatch sb, Rectangle rect) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int bands = 3;
            for (int i = 0; i < bands; i++) {
                float t = i / (float)bands;
                float y = rect.Y + 15 + t * (rect.Height - 30);
                float amp = 4f;
                int segments = 30;
                Vector2 prev = Vector2.Zero;
                for (int s = 0; s <= segments; s++) {
                    float p = s / (float)segments;
                    float waveY = y + (float)Math.Sin(oceanWavePhase * 2f + p * MathHelper.TwoPi + t) * amp;
                    Vector2 point = new(rect.X + 8 + p * (rect.Width - 16), waveY);
                    if (s > 0) {
                        Vector2 diff = point - prev;
                        float len = diff.Length();
                        if (len > 0.01f) {
                            float rot = diff.ToRotation();
                            Color c = new Color(40, 130, 180) * (alpha * 0.1f);
                            sb.Draw(px, prev, new Rectangle(0, 0, 1, 1), c, rot, Vector2.Zero, new Vector2(len, 1.5f), SpriteEffects.None, 0f);
                        }
                    }
                    prev = point;
                }
            }
        }

        private static void DrawOceanCorner(SpriteBatch sb, Vector2 pos, float a) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float size = 4f;
            Color c = new Color(150, 230, 255) * a;
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.25f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.8f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.25f), SpriteEffects.None, 0f);
        }
        #endregion

        #region ��ǻ������
        private void DrawBrimstoneStyle(SpriteBatch spriteBatch, Rectangle rect) {
            Texture2D px = VaultAsset.placeholder2.Value;

            //���䱳��
            int segments = 25;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                Rectangle r = new(rect.X, y1, rect.Width, Math.Max(1, y2 - y1));

                Color deep = new Color(30, 8, 8);
                Color mid = new Color(90, 20, 15);
                Color hot = new Color(150, 45, 25);

                float flameWave = (float)Math.Sin(flameTimer * 0.7f + t * 2.5f) * 0.5f + 0.5f;
                Color c = Color.Lerp(Color.Lerp(deep, mid, flameWave), hot, t * 0.6f);
                c *= alpha;

                spriteBatch.Draw(px, r, new Rectangle(0, 0, 1, 1), c);
            }

            //��������
            float pulse = (float)Math.Sin(emberGlowTimer * 2f) * 0.5f + 0.5f;
            Color pulseColor = new Color(130, 30, 20) * (alpha * 0.2f * pulse);
            spriteBatch.Draw(px, rect, new Rectangle(0, 0, 1, 1), pulseColor);

            //�߿�
            Color edge = Color.Lerp(new Color(190, 70, 40), new Color(255, 150, 80), pulse) * alpha;
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), edge);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), edge * 0.75f);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.9f);
            spriteBatch.Draw(px, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.9f);

            //�������
            DrawFlameCorner(spriteBatch, new Vector2(rect.X + 12, rect.Y + 12), alpha * 0.95f);
            DrawFlameCorner(spriteBatch, new Vector2(rect.Right - 12, rect.Y + 12), alpha * 0.95f);
        }

        private static void DrawFlameCorner(SpriteBatch sb, Vector2 pos, float a) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float size = 5f;
            Color c = new Color(255, 160, 80) * a;
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f), new Vector2(size * 1.1f, size * 0.28f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.85f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size * 1.1f, size * 0.28f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.7f, MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.8f, size * 0.22f), SpriteEffects.None, 0f);
        }
        #endregion

        private void DrawContent(SpriteBatch spriteBatch, Rectangle rect) {
            var font = FontAssets.MouseText.Value;
            Vector2 iconPos = new Vector2(rect.X + 20, rect.Y + rect.Height / 2f);

            //����ͼ��
            if (currentAchievement.CustomIcon != null) {
                Texture2D icon = currentAchievement.CustomIcon;
                float iconScale = Math.Min(60f / icon.Width, 60f / icon.Height);
                spriteBatch.Draw(icon, iconPos, null, Color.White * alpha, 0f, icon.Size() / 2f, iconScale * celebrateScale, SpriteEffects.None, 0f);
            }
            else if (currentAchievement.IconItemID != ItemID.None) {
                Main.instance.LoadItem(currentAchievement.IconItemID);
                Texture2D itemTex = TextureAssets.Item[currentAchievement.IconItemID].Value;
                Rectangle itemFrame = Main.itemAnimations[currentAchievement.IconItemID] != null
                    ? Main.itemAnimations[currentAchievement.IconItemID].GetFrame(itemTex)
                    : itemTex.Frame();
                float itemScale = Math.Min(60f / itemFrame.Width, 60f / itemFrame.Height);
                spriteBatch.Draw(itemTex, iconPos, itemFrame, Color.White * alpha, 0f, itemFrame.Size() / 2f, itemScale * celebrateScale * 1.2f, SpriteEffects.None, 0f);
            }

            //��������
            Vector2 textStart = new Vector2(rect.X + 90, rect.Y + 20);
            Color titleColor = currentAchievement.Style == ToastStyle.Brimstone
                ? new Color(255, 230, 200) * alpha
                : new Color(220, 240, 255) * alpha;
            Color descColor = titleColor * 0.85f;

            //����
            string titleText = currentAchievement.Title ?? AchievementUnlocked.Value;
            Vector2 titleSize = font.MeasureString(titleText) * 0.9f;
            Utils.DrawBorderString(spriteBatch, titleText, textStart, titleColor, 0.9f);

            //����
            if (!string.IsNullOrEmpty(currentAchievement.Description)) {
                Vector2 descPos = textStart + new Vector2(0, titleSize.Y + 5);
                Utils.DrawBorderString(spriteBatch, currentAchievement.Description, descPos, descColor, 0.7f);
            }
        }
        #endregion

        #region ������
        private class CelebrationParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Size;
            public Color Color;
            public float Rotation;
            public float RotationSpeed;

            public CelebrationParticle(Vector2 center, ToastStyle style) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float speed = Main.rand.NextFloat(2f, 5f);
                Position = center + angle.ToRotationVector2() * Main.rand.NextFloat(20f, 40f);
                Velocity = angle.ToRotationVector2() * speed;
                Life = 0f;
                MaxLife = Main.rand.NextFloat(40f, 70f);
                Size = Main.rand.NextFloat(2f, 4f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
                RotationSpeed = Main.rand.NextFloat(-0.1f, 0.1f);

                Color = style == ToastStyle.Ocean
                    ? Main.rand.Next(new Color[] { new Color(100, 200, 255), new Color(150, 230, 255), Color.Cyan })
                    : Main.rand.Next(new Color[] { new Color(255, 150, 80), new Color(255, 200, 100), Color.Orange });
            }

            public bool Update() {
                Life++;
                Position += Velocity;
                Velocity *= 0.95f;
                Velocity.Y += 0.15f;//����
                Rotation += RotationSpeed;
                return Life >= MaxLife;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D px = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin((1f - t) * MathHelper.Pi);
                Color drawColor = Color * (fade * alpha);
                sb.Draw(px, Position, new Rectangle(0, 0, 1, 1), drawColor, Rotation, new Vector2(0.5f), Size, SpriteEffects.None, 0f);
            }
        }

        private class MiniStar
        {
            public Vector2 Position;
            public float Life;
            public float MaxLife;
            public float Seed;

            public MiniStar(Vector2 pos) {
                Position = pos;
                Life = 0f;
                MaxLife = Main.rand.NextFloat(30f, 60f);
                Seed = Main.rand.NextFloat(10f);
            }

            public bool Update() {
                Life++;
                return Life >= MaxLife;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D px = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * MathHelper.Pi) * alpha;
                float scale = 1.5f + (float)Math.Sin((Life + Seed * 20f) * 0.12f) * 0.8f;
                Color c = Color.Gold * (0.5f * fade);
                sb.Draw(px, Position, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f), new Vector2(scale, scale * 0.25f), SpriteEffects.None, 0f);
                sb.Draw(px, Position, new Rectangle(0, 0, 1, 1), c * 0.8f, MathHelper.PiOver2, new Vector2(0.5f), new Vector2(scale, scale * 0.25f), SpriteEffects.None, 0f);
            }
        }

        private class MiniEmber
        {
            public Vector2 Position;
            public float Life;
            public float MaxLife;
            public float Size;
            public float RiseSpeed;

            public MiniEmber(Vector2 pos) {
                Position = pos;
                Life = 0f;
                MaxLife = Main.rand.NextFloat(40f, 80f);
                Size = Main.rand.NextFloat(1.5f, 3f);
                RiseSpeed = Main.rand.NextFloat(0.5f, 1.2f);
            }

            public bool Update() {
                Life++;
                Position.Y -= RiseSpeed;
                return Life >= MaxLife;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D px = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin((1f - t) * MathHelper.Pi);
                Color c = Color.Lerp(new Color(255, 180, 90), new Color(255, 90, 50), t) * (fade * alpha * 0.7f);
                sb.Draw(px, Position, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f), Size, SpriteEffects.None, 0f);
            }
        }
        #endregion
    }
}
