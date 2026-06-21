using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.StarStream
{
    internal sealed class StarStreamPanelState
    {
        /// <summary>对齐 ADV DialogueBoxBase 正文折行留白（PanelWidth - Padding×2 - 24）。</summary>
        public const int TextWrapInset = 24;

        private const float SideMargin = 30f;
        private const float ParticleMargin = 30f;

        public float StarFlowTimer;
        public float NebulaPulseTimer;
        public float ConstellationPhase;
        public float AuroraTimer;
        public float ShimmerTimer;

        private readonly List<StarStreamPRT> starStreams = [];
        private readonly List<StarDustPRT> starDusts = [];
        private readonly List<StarStreamDataStream> dataStreams = [];

        private int starStreamSpawnTimer;
        private int starDustSpawnTimer;
        private int dataStreamSpawnTimer;

        public void AdvanceTimers(bool includeAurora = true, bool includeStarFlow = true) {
            if (includeStarFlow) {
                StarFlowTimer = SkinAnimUtil.WrapTimer(StarFlowTimer, 0.04f);
            }
            NebulaPulseTimer = SkinAnimUtil.WrapTimer(NebulaPulseTimer, 0.022f);
            ConstellationPhase = SkinAnimUtil.WrapTimer(ConstellationPhase, 0.012f);
            if (includeAurora) {
                AuroraTimer = SkinAnimUtil.WrapTimer(AuroraTimer, 0.018f);
            }
            ShimmerTimer = SkinAnimUtil.WrapTimer(ShimmerTimer, 0.035f);
        }

        public void Reset() {
            StarFlowTimer = 0f;
            NebulaPulseTimer = 0f;
            ConstellationPhase = 0f;
            AuroraTimer = 0f;
            ShimmerTimer = 0f;
            starStreams.Clear();
            starDusts.Clear();
            dataStreams.Clear();
            starStreamSpawnTimer = 0;
            starDustSpawnTimer = 0;
            dataStreamSpawnTimer = 0;
        }

        public void UpdateDialogue(Rectangle panelRect, bool active) {
            AdvanceTimers();

            starStreamSpawnTimer++;
            if (active && starStreamSpawnTimer >= 14 && starStreams.Count < 18) {
                starStreamSpawnTimer = 0;
                Vector2 panelPos = panelRect.Location.ToVector2();
                Vector2 panelSize = new(panelRect.Width, panelRect.Height);
                Vector2 pos = panelPos + new Vector2(
                    Main.rand.NextFloat(SideMargin, panelSize.X - SideMargin),
                    Main.rand.NextFloat(30f, panelSize.Y - 20f));
                starStreams.Add(new StarStreamPRT(pos));
            }

            Vector2 origin = panelRect.Location.ToVector2();
            Vector2 size = new(panelRect.Width, panelRect.Height);
            for (int i = starStreams.Count - 1; i >= 0; i--) {
                if (starStreams[i].Update(origin, size)) {
                    starStreams.RemoveAt(i);
                }
            }

            starDustSpawnTimer++;
            if (active && starDustSpawnTimer >= 28 && starDusts.Count < 10) {
                starDustSpawnTimer = 0;
                float scaleW = Main.UIScale;
                float left = panelRect.X + SideMargin * scaleW;
                float right = panelRect.Right - SideMargin * scaleW;
                Vector2 start = new(
                    Main.rand.NextFloat(left, right),
                    panelRect.Y + Main.rand.NextFloat(40f, panelRect.Height - 30f));
                starDusts.Add(new StarDustPRT(start));
            }

            for (int i = starDusts.Count - 1; i >= 0; i--) {
                if (starDusts[i].Update(origin, size)) {
                    starDusts.RemoveAt(i);
                }
            }
        }

        public void UpdateChoice(Rectangle panelRect, bool active) {
            AdvanceTimers(includeAurora: false);

            dataStreamSpawnTimer++;
            if (active && dataStreamSpawnTimer >= 16 && dataStreams.Count < 12) {
                dataStreamSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(panelRect.X + 20f, panelRect.Right - 20f);
                Vector2 startPos = new(xPos, panelRect.Y + Main.rand.NextFloat(20f, panelRect.Height - 20f));
                dataStreams.Add(new StarStreamDataStream(startPos));
            }

            for (int i = dataStreams.Count - 1; i >= 0; i--) {
                if (dataStreams[i].Update(panelRect)) {
                    dataStreams.RemoveAt(i);
                }
            }
        }

        public void UpdatePopup(Rectangle panelRect, bool active, float panelAlpha) {
            AdvanceTimers();

            Vector2 panelPos = panelRect.Location.ToVector2();
            Vector2 panelSize = new(panelRect.Width, panelRect.Height);

            starStreamSpawnTimer++;
            if (active && panelAlpha > 0.6f && starStreamSpawnTimer >= 14 && starStreams.Count < 15) {
                starStreamSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(panelPos.X + ParticleMargin, panelPos.X + panelSize.X - ParticleMargin);
                Vector2 startPos = new(xPos, panelPos.Y + Main.rand.NextFloat(20f, panelSize.Y - 20f));
                starStreams.Add(new StarStreamPRT(startPos));
            }

            for (int i = starStreams.Count - 1; i >= 0; i--) {
                if (starStreams[i].Update(panelPos, panelSize)) {
                    starStreams.RemoveAt(i);
                }
            }

            starDustSpawnTimer++;
            if (active && panelAlpha > 0.6f && starDustSpawnTimer >= 30 && starDusts.Count < 8) {
                starDustSpawnTimer = 0;
                Vector2 startPos = new(
                    Main.rand.NextFloat(panelPos.X + 12f, panelPos.X + panelSize.X - 12f),
                    Main.rand.NextFloat(panelPos.Y + 12f, panelPos.Y + panelSize.Y - 12f));
                starDusts.Add(new StarDustPRT(startPos));
            }

            for (int i = starDusts.Count - 1; i >= 0; i--) {
                if (starDusts[i].Update(panelPos, panelSize)) {
                    starDusts.RemoveAt(i);
                }
            }
        }

        public void DrawStreamParticles(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch, float alpha, float streamAlpha = 0.7f, float dustAlpha = 0.8f) {
            foreach (StarDustPRT dust in starDusts) {
                dust.Draw(spriteBatch, alpha * dustAlpha);
            }
            foreach (StarStreamPRT stream in starStreams) {
                stream.Draw(spriteBatch, alpha * streamAlpha);
            }
        }

        public void DrawDataStreams(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch, float alpha) {
            foreach (StarStreamDataStream stream in dataStreams) {
                stream.Draw(spriteBatch, alpha * 0.8f);
            }
        }

        internal sealed class StarStreamDataStream
        {
            public Vector2 Pos;
            public float Size;
            public float Life;
            public float MaxLife;
            public float Seed;
            public Vector2 Velocity;
            public float Rotation;

            public StarStreamDataStream(Vector2 start) {
                Pos = start;
                Size = Main.rand.NextFloat(1.5f, 3.5f);
                Life = 0f;
                MaxLife = Main.rand.NextFloat(80f, 140f);
                Seed = Main.rand.NextFloat(10f);
                Velocity = new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-0.5f, -0.15f));
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }

            public bool Update(Rectangle bounds) {
                Life++;
                Rotation += 0.018f;
                Pos += Velocity;
                Velocity.Y -= 0.008f;
                Velocity.X += (float)System.Math.Sin(Life * 0.05f + Seed) * 0.02f;

                if (Life >= MaxLife || Pos.X < bounds.X - 30 || Pos.X > bounds.Right + 30 ||
                    Pos.Y < bounds.Y - 30 || Pos.Y > bounds.Bottom + 30) {
                    return true;
                }
                return false;
            }

            public void Draw(Microsoft.Xna.Framework.Graphics.SpriteBatch sb, float alpha) {
                Microsoft.Xna.Framework.Graphics.Texture2D px = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)System.Math.Sin(t * MathHelper.Pi) * alpha;
                float scale = Size * (0.6f + (float)System.Math.Sin((Life + Seed * 30f) * 0.07f) * 0.4f);

                Color gold = new Color(255, 210, 100) * (0.85f * fade);
                sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), gold, Rotation, new Vector2(0.5f, 0.5f), new Vector2(scale * 2.5f, scale * 0.35f), Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0f);
                sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), gold * 0.8f, Rotation + MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(scale * 2.5f, scale * 0.35f), Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0f);

                Color warm = new Color(255, 240, 200) * (0.4f * fade);
                sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), warm, 0f, new Vector2(0.5f), new Vector2(scale * 0.5f), Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0f);
            }
        }
    }
}
