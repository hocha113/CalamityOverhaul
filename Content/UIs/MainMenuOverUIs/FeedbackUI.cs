using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs.MainMenuOverUIs
{
    internal class FeedbackUI : UIHandle, ICWRLoader
    {
        internal static FeedbackUI Instance { get; private set; }
        private static Asset<Texture2D> githubOAC;
        private static Asset<Texture2D> steamOAC;
        private const float githubSiz1 = 0.001f;
        private const float githubSiz2 = 0.05f;
        private int Time;
        private bool old_onGithub;
        private bool old_onSteam;
        internal float _sengs;
        internal bool _active;
        private bool OnGithub => MousePosition.Distance(GithubCenter) < githubOAC.Width() * githubSiz2 / 2f;
        private bool OnSteam => MousePosition.Distance(SteamCenter) < steamOAC.Width() * githubSiz2 / 2f;
        private float GithubSiz => float.Lerp(githubSiz1, githubSiz2, _sengs);
        private static Vector2 GithubPos1 => new Vector2(Main.screenWidth - 60, 60);
        private static Vector2 GithubPos2 => new Vector2(Main.screenWidth - 140, 60);
        private Vector2 GithubPos => Vector2.Lerp(GithubPos1, GithubPos2, _sengs);
        private Vector2 GithubCenter => GithubPos + new Vector2(githubOAC.Width(), githubOAC.Height()) / 2 * GithubSiz;
        private static Vector2 SteamPos1 => new Vector2(Main.screenWidth - 80, 60);
        private static Vector2 SteamPos2 => new Vector2(Main.screenWidth - 200, 60);
        private Vector2 SteamPos => Vector2.Lerp(SteamPos1, SteamPos2, _sengs);
        private Vector2 SteamCenter => SteamPos + new Vector2(steamOAC.Width(), steamOAC.Height()) / 2 * GithubSiz;
        public override LayersModeEnum LayersMode => LayersModeEnum.Mod_MenuLoad;
        public override bool Active => CWRLoad.OnLoadContentBool;
        public bool OnActive() => _active || _sengs > 0;
        void ICWRLoader.LoadAsset() {
            githubOAC = CWRUtils.GetT2DAsset(CWRConstant.UI + "GithubOAC");
            steamOAC = CWRUtils.GetT2DAsset(CWRConstant.UI + "SteamOAC");
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

        public void Initialize() {
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

        public override void Update() {
            Initialize();

            if (_sengs >= 1 && githubOAC != null && steamOAC != null) {
                if (!old_onSteam && OnSteam || !old_onGithub && OnGithub) {
                    SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.6f, Volume = 0.6f });
                }

                old_onSteam = OnSteam;
                old_onGithub = OnGithub;

                if (keyLeftPressState == KeyPressState.Pressed) {
                    if (OnGithub) {
                        SoundEngine.PlaySound(SoundID.MenuTick);
                        (CWRConstant.githubUrl + "/issues/new").WebRedirection(true);
                    }
                    else if (OnSteam) {
                        SoundEngine.PlaySound(SoundID.MenuTick);
                        CWRConstant.steamFeedback.WebRedirection(true);
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

            Time++;
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (!OnActive()) {
                return;
            }

            if (githubOAC == null || steamOAC == null) {
                _active = false;
                return;
            }
            //运行环境比较敏感，为了防止玩家在卸载模组时还要和UI进行交互，这里判断一下资源是否已经被释放
            if (CWRAsset.Placeholder_White == null || CWRAsset.Placeholder_White.IsDisposed) {
                _active = false;
                return;
            }

            Color color = VaultUtils.MultiStepColorLerp(Math.Abs(MathF.Sin(Time * 0.035f)), Color.Gold, Color.Green);

            spriteBatch.Draw(CWRAsset.Placeholder_White.Value, Vector2.Zero
                , new Rectangle(0, 0, Main.screenWidth, Main.screenHeight)
                , Color.Black * _sengs * 0.85f, 0f, Vector2.Zero, 1, SpriteEffects.None, 0);

            spriteBatch.Draw(githubOAC.Value, GithubPos, null
                , (OnGithub ? color : Color.White) * _sengs, 0f, Vector2.Zero, GithubSiz, SpriteEffects.None, 0);
            spriteBatch.Draw(steamOAC.Value, SteamPos, null
                , (OnSteam ? color : Color.White) * _sengs, 0f, Vector2.Zero, GithubSiz, SpriteEffects.None, 0);
        }
    }
}
