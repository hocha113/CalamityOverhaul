using CalamityOverhaul.Content.UIs.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.MainMenuOverUIs
{
    internal class OpenUI : BaseMainMenuOverUI, ILoader
    {
        private float _sengs;
        internal bool _active;
        internal static OpenUI Instance { get; private set; }
        static Asset<Texture2D> githubOAC;
        static Asset<Texture2D> steamOAC;
        Vector2 githubPos1 => new Vector2(Main.screenWidth - 80, 20);
        Vector2 githubPos2 => new Vector2(Main.screenWidth - 140, 30);
        Vector2 githubPos => Vector2.Lerp(githubPos1, githubPos2, _sengs);
        Vector2 githubCenter => githubPos + new Vector2(githubOAC.Width(), githubOAC.Height()) / 2 * githubSiz;
        float githubSiz1 => 0.001f;
        float githubSiz2 => 0.05f;
        bool old_onGithub;
        bool old_onSteam;
        bool onGithub => MouPos.Distance(githubCenter) < githubOAC.Width() * githubSiz2 / 2f;
        bool onSteam => MouPos.Distance(steamCenter) < steamOAC.Width() * githubSiz2 / 2f;
        float githubSiz => float.Lerp(githubSiz1, githubSiz2, _sengs);
        Vector2 steamPos1 => new Vector2(Main.screenWidth - 80, 20);
        Vector2 steamPos2 => new Vector2(Main.screenWidth - 200, 30);
        Vector2 steamPos => Vector2.Lerp(steamPos1, steamPos2, _sengs);
        Vector2 steamCenter => steamPos + new Vector2(steamOAC.Width(), steamOAC.Height()) / 2 * githubSiz;
        public bool OnActive() => _active || _sengs > 0;
        void ILoader.LoadAsset() {
            githubOAC = CWRUtils.GetT2DAsset(CWRConstant.UI + "GithubOAC", true);
            steamOAC = CWRUtils.GetT2DAsset(CWRConstant.UI + "SteamOAC", true);
        }
        public override void Load() {
            Instance = this;
            _sengs = 0;
        }
        public override void UnLoad() {
            Instance = null;
            _sengs = 0;
            steamOAC = null;
            githubOAC = null;
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

            if ((!old_onSteam && onSteam || !old_onGithub && onGithub) && _sengs >= 1) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.6f, Volume = 0.6f });
            }

            old_onSteam = onSteam;
            old_onGithub = onGithub;

            int mouS = DownStartL();
            if (mouS == 1) {
                if (_sengs >= 1) {
                    if (onGithub) {
                        SoundEngine.PlaySound(SoundID.MenuTick);
                        (CWRConstant.githubUrl + "/issues/new").WebRedirection(true);
                    }
                    else if (onSteam) {
                        SoundEngine.PlaySound(SoundID.MenuTick);
                        "https://steamcommunity.com/workshop/filedetails/discussion/3161388997/4331980200220181304/".WebRedirection(true);
                    }
                    else {
                        SoundEngine.PlaySound(SoundID.MenuClose);
                        _active = false;
                    }
                }
            }

            if (OnActive()) {
                KeyboardState currentKeyState = Main.keyState;
                KeyboardState previousKeyState = Main.oldKeyState;
                if (currentKeyState.IsKeyDown(Keys.Escape) && !previousKeyState.IsKeyDown(Keys.Escape)) {
                    SoundEngine.PlaySound(SoundID.MenuClose);
                    _active = false;
                }
            }

            time++;
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (!OnActive()) {
                return;
            }

            Color color = CWRUtils.MultiStepColorLerp(Math.Abs(MathF.Sin(time * 0.035f)), Color.Gold, Color.Green);

            spriteBatch.Draw(CWRUtils.GetT2DAsset(CWRConstant.Placeholder2).Value, Vector2.Zero
                , new Rectangle(0, 0, Main.screenWidth, Main.screenHeight)
                , Color.Black * _sengs * 0.85f, 0f, Vector2.Zero, 1, SpriteEffects.None, 0);

            spriteBatch.Draw(githubOAC.Value, githubPos, null
                , (onGithub ? color : Color.White)  * _sengs, 0f, Vector2.Zero, githubSiz, SpriteEffects.None, 0);
            spriteBatch.Draw(steamOAC.Value, steamPos, null
                , (onSteam ? color : Color.White) * _sengs, 0f, Vector2.Zero, githubSiz, SpriteEffects.None, 0);
        }
    }
}
