using CalamityOverhaul.Content.UIs.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;

namespace CalamityOverhaul.Content.UIs.MainMenuOverUIs
{
    internal class OpenUI : BaseMainMenuOverUI, ILoader
    {
        private float _sengs;
        internal bool _active;
        static Asset<Texture2D> githubOAC;
        static Asset<Texture2D> steamOAC;
        public bool OnActive() {
            return _active || _sengs > 0;
        }
        void ILoader.LoadAsset() {
            githubOAC = CWRUtils.GetT2DAsset(CWRConstant.UI + "GithubOAC");
            steamOAC = CWRUtils.GetT2DAsset(CWRConstant.UI + "SteamOAC");
        }
        public override void Load() {
            _sengs = 0;
        }
        public override void UnLoad() {
            _sengs = 0;
            steamOAC.Dispose();
            githubOAC.Dispose();
        }

        public override void Initialize() {
            if (_active) {
                if (_sengs < 1) {
                    _sengs += 0.01f;
                }
            }
            else {
                if (_sengs > 0) {
                    _sengs -= 0.01f;
                }
            }
        }

        public override void Update(GameTime gameTime) {
            Initialize();
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (!OnActive()) {
                return;
            }

            spriteBatch.Draw(githubOAC.Value, DrawPos + new Vector2(0, 5), null
                , Color.White * _sengs, 0f, Vector2.Zero, 0.56f, SpriteEffects.None, 0);
        }
    }
}
