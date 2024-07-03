using CalamityOverhaul.Content.UIs.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.UIs.MainMenuOverUIs
{
    internal class AcknowledgmentUI : BaseMainMenuOverUI
    {
        internal class ProjItem
        {
            int index;
            int timeLeft;
            int startTime;
            int alp;
            bool active = true;
            float size;
            string text;
            Texture2D texture;
            Color color;
            Vector2 position;
            Vector2 velocity;
            public ProjItem(int index, int timeLeft, float size, int alp, Color color
                , Vector2 position, Vector2 velocity, string text, Texture2D texture, int startTime) {
                this.index = index;
                this.timeLeft = timeLeft;
                this.size = size;
                this.alp = alp;
                this.color = color;
                this.position = position;
                this.velocity = velocity;
                this.text = text;
                this.texture = texture;
                this.startTime = startTime;
            }

            public void AI(float sengs) {
                if (--startTime > 0) {
                    return;
                }
                position += velocity;
                timeLeft--;
                if (timeLeft <= 0) {
                    active = false;
                }
            }

            public void Draw(SpriteBatch spriteBatch, float sengs) {
                if (--startTime > 0) {
                    return;
                }
            }
        }

        private float _sengs;
        internal bool _active;
        internal static AcknowledgmentUI Instance { get; private set; }
        internal List<ProjItem> projectiles = new List<ProjItem>();
        public bool OnActive() => _active || _sengs > 0;
        public override bool CanLoad() => false;
        public override void Load() {
            Instance = this;
            _sengs = 0;
        }
        public override void UnLoad() {
            Instance = null;
            _sengs = 0;
        }
        public override void Initialize() {
            if (_active) {
                if (_sengs < 1) {
                    _sengs += 0.04f;
                }
            }
            else {
                if (_sengs > 0) {
                    _sengs -= 0.04f;
                }
            }
        }
        public override void Update(GameTime gameTime) {
            Initialize();
            foreach (ProjItem projItem in projectiles) {
                projItem.AI(_sengs);
            }
        }
        public override void Draw(SpriteBatch spriteBatch) {
            foreach (ProjItem projItem in projectiles) {
                projItem.Draw(spriteBatch, _sengs);
            }
        }
    }
}
