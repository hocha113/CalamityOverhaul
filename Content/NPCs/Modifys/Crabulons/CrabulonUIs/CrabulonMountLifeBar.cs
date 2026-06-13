using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.Modifys.Crabulons.CrabulonUIs
{
    internal class CrabulonMountLifeBar : UIHandle
    {
        public bool Open {
            get {
                if (!player.TryGetOverride<CrabulonPlayer>(out var crabulonPlayer)) {
                    return false;
                }
                npc = null;
                if (crabulonPlayer.MountCrabulon != null) {
                    npc = crabulonPlayer.MountCrabulon.npc;
                    return true;
                }
                return false;
            }
        }
        public static readonly List<CrabulonLife> crabulonLives = [];
        public const int liveMargin = 4;
        public const int crabulonLiveCount = 20;
        public const int crabulonLiveColumn = 2;
        public const int crabulonLiveLine = crabulonLiveCount / crabulonLiveColumn;
        public override bool Active => Open || sengs > 0f;
        private float sengs;
        private NPC npc;
        private int oldLife = -1;
        private float waveShakeTime;

        public override void OnEnterWorld() {
            crabulonLives.Clear();
            for (int i = 0; i < crabulonLiveCount; i++) {
                crabulonLives.Add(new CrabulonLife() { index = i });
            }
        }

        public override void Update() {
            if (!Open) {
                if (sengs > 0f) {
                    sengs -= 0.1f;
                }
                oldLife = -1;
                return;
            }
            else {
                if (sengs < 1f) {
                    sengs += 0.1f;
                }
            }

            if (!npc.Alives()) {
                oldLife = -1;
                return;
            }

            if (oldLife == -1) {
                oldLife = npc.life;
            }

            if (npc.life < oldLife) {
                waveShakeTime = 40f;//受伤波浪 40 帧
            }
            oldLife = npc.life;

            if (waveShakeTime > 0) {
                waveShakeTime--;
            }

            bool isLowHealth = npc.life < npc.lifeMax * 0.5f;

            Vector2 lifeSize = CrabulonLife.Life.Size();

            Vector2 uiSize = new Vector2((lifeSize.X + liveMargin) * crabulonLiveLine, (lifeSize.Y + liveMargin) * crabulonLiveColumn);

            DrawPosition = new Vector2((int)(Main.screenWidth / 2 - uiSize.X / 2), (int)(Main.screenHeight / 2 + uiSize.X / 2 + Main.screenHeight / 10 * 1));

            UIHitBox = DrawPosition.GetRectangle(uiSize);

            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            DrawPosition.Y = MathHelper.Clamp(DrawPosition.Y, 0, UIHitBox.Y);

            for (int i = 0; i < crabulonLiveCount; i++) {
                var crabulonLive = crabulonLives[i];
                crabulonLive.DrawPosition = DrawPosition + CrabulonLife.Life.Size() / 2;
                crabulonLive.npc = npc;
                crabulonLive.DrawPosition.X += i % crabulonLiveLine * (lifeSize.X + liveMargin);
                crabulonLive.DrawPosition.Y += i / crabulonLiveLine * (lifeSize.Y + liveMargin);
                crabulonLive.sengs = sengs;
                crabulonLive.waveShakeTime = waveShakeTime;
                crabulonLive.isLowHealth = isLowHealth;
                crabulonLive.Update();
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (!npc.Alives()) {
                return;
            }

            foreach (var crabulonLive in crabulonLives) {
                crabulonLive.Draw(spriteBatch);
            }

            if (hoverInMainPage) {
                string content = $"{npc.life}/{npc.lifeMax}";
                float textScale = 1f;
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(content) * textScale;
                Vector2 drawPos = MousePosition + new Vector2(0, 36);
                Utils.DrawBorderStringFourWay(Main.spriteBatch, FontAssets.MouseText.Value, content
                            , drawPos.X, drawPos.Y, Color.White, Color.Black, new Vector2(0.3f), textScale);
            }
        }
    }

    internal class CrabulonLife : UIHandle
    {
        [VaultLoaden(CWRConstant.UI + "CrabulonLife")]
        public static Asset<Texture2D> Life = null;
        public override LayersModeEnum LayersMode => LayersModeEnum.None;

        public int lifeValue;
        public int index;
        public NPC npc;

        internal float sengs;
        internal float waveShakeTime;
        internal bool isLowHealth;

        private float shakeTime;
        private float dynamicScale = 1f;
        private float dynamicRotation;
        private Vector2 damageShakeOffset = Vector2.Zero;
        private Vector2 waveShakeOffset = Vector2.Zero;

        public override void Update() {
            if (npc == null || !npc.active) {
                return;
            }

            waveShakeOffset = Vector2.Zero;

            int maxLifePerUnit = npc.lifeMax / CrabulonMountLifeBar.crabulonLiveCount;
            if (maxLifePerUnit <= 0) {
                return;
            }

            int newLifeValue = (int)MathHelper.Clamp(npc.life - index * maxLifePerUnit, 0, maxLifePerUnit);

            if (newLifeValue < lifeValue) {
                shakeTime = 20f;
            }

            if (shakeTime > 0) {
                shakeTime--;
                float intensity = shakeTime / 20f;
                damageShakeOffset = Main.rand.NextVector2Circular(intensity * 4f, intensity * 4f);
                dynamicRotation = Main.rand.NextFloat(-0.2f, 0.2f) * intensity;
            }
            else {
                damageShakeOffset = Vector2.Zero;
                dynamicRotation = 0f;
            }

            lifeValue = newLifeValue;

            float waveDelay = 1.5f;
            float timeAfterWaveReach = waveShakeTime - index * waveDelay;
            if (timeAfterWaveReach > 0) {
                float maxIntensityTime = 10f;
                float intensity = 1f - Math.Abs(timeAfterWaveReach - maxIntensityTime) / maxIntensityTime;
                intensity = MathHelper.Clamp(intensity, 0f, 1f);
                if (intensity > 0) {
                    waveShakeOffset.X += Main.rand.NextFloat(-2f, 2f) * intensity;
                    waveShakeOffset.Y += Main.rand.NextFloat(-2f, 2f) * intensity;
                }
            }

            if (isLowHealth) {
                float pulseSpeed = 10f;
                float pulseAmplitude = 2.5f;
                float delayFactor = 0.4f;
                waveShakeOffset.Y += (float)Math.Sin(Main.GameUpdateCount * (pulseSpeed / 60f) + index * delayFactor) * pulseAmplitude;
            }

            float lifePercent = (float)lifeValue / maxLifePerUnit;

            if (lifePercent > 0 && lifePercent < 0.35f) {
                float pulseSpeed = 12f;
                float pulseIntensity = 0.12f;
                dynamicScale = 1f + (float)Math.Sin(Main.GameUpdateCount * (pulseSpeed / 60f)) * pulseIntensity;
            }
            else {
                dynamicScale = 1f;
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (npc == null || !npc.active) {
                return;
            }

            int maxLifePerUnit = npc.lifeMax / CrabulonMountLifeBar.crabulonLiveCount;
            if (maxLifePerUnit <= 0) {
                return;
            }

            float fillRatio = (float)lifeValue / maxLifePerUnit;

            if (fillRatio <= 0) {
                return;
            }

            Color drawColor = Color.White * fillRatio;
            drawColor.A = (byte)(255 * (0.2f + fillRatio * 0.8f));

            float finalScale = 0.5f + fillRatio * dynamicScale * 0.5f;
            Vector2 finalDrawPosition = DrawPosition + damageShakeOffset + waveShakeOffset;

            spriteBatch.Draw(Life.Value, finalDrawPosition, null, drawColor * sengs, dynamicRotation, Life.Size() / 2, finalScale, SpriteEffects.None, 0);
        }
    }

    internal class CrabulonFriendBossBar : ModBossBar//驯服态隐藏 Boss 血条
    {
        public override bool PreDraw(SpriteBatch spriteBatch, NPC npc, ref BossBarDrawParams drawParams) {
            if (npc.TryGetOverride<ModifyCrabulon>(out var modifyCrabulon)) {
                if (modifyCrabulon.FeedValue > 0f) {
                    return false;
                }
            }
            return true;
        }
    }
}