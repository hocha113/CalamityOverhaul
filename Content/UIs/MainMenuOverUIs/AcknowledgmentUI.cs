using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs.MainMenuOverUIs
{
    internal class AcknowledgmentUI : UIHandle, ICWRLoader
    {
        internal static string textElement1 => $" [{CWRLocText.GetTextValue("IconUI_Text3")}]";
        internal static string textElement2 => $" [{CWRLocText.GetTextValue("IconUI_Text4")}]";
        internal static string textElement3 => $" [{CWRLocText.GetTextValue("IconUI_Text5")}]";
        internal static string textElement4 => $" [{CWRLocText.GetTextValue("IconUI_Text6")}]";
        internal static string[] names = [];
        public override LayersModeEnum LayersMode => LayersModeEnum.Mod_MenuLoad;
        public override bool Active => CWRLoad.OnLoadContentBool;
        internal class ProjItem
        {
            public int index;
            public int timeLeft;
            public int startTime;
            public int alp;
            public bool active = true;
            public float size;
            public string text;
            public Texture2D texture;
            public Color color;
            public Vector2 position;
            public Vector2 velocity;
            public Vector2 textSize;
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

            public virtual void AI(float sengs) {
                if (--startTime > 0) {
                    return;
                }
                textSize = FontAssets.MouseText.Value.MeasureString(text);
                if (alp < 255 && timeLeft % 2 == 0) {
                    alp++;
                }
                position += velocity;
                timeLeft--;
                if (timeLeft <= 0) {
                    active = false;
                }
            }

            public virtual void Draw(SpriteBatch spriteBatch, float sengs) {
                if (--startTime > 0 || position.Y < -200) {
                    return;
                }

                float textAlp = sengs * (alp / 255f);
                if (index == 0) {
                    spriteBatch.Draw(Logo.Value, position, null, Color.White * textAlp, 0f
                        , new Vector2(Logo.Size().X / 2, Logo.Size().Y), 1, SpriteEffects.None, 0);
                    return;
                }
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, text
                    , position.X - textSize.X / 2, position.Y, color * textAlp, Color.Black * textAlp, new Vector2(0.2f), 1);
            }
        }

        internal class EffectEntity : ProjItem
        {
            protected int ai0;
            private float rotation;
            public float rotSpeed;
            public int itemID;

            private Item item {
                get {
                    Item inds = new Item();
                    inds.SetDefaults(itemID);
                    return inds;
                }
            }
            public EffectEntity(int index, int timeLeft, float size, int alp, Color color
                , Vector2 position, Vector2 velocity, string text, Texture2D texture, int startTime
                , int ai0, float rotation, int itemID, float rotSpeed)
                : base(index, timeLeft, size, alp, color, position, velocity, text, texture, startTime) {
                this.ai0 = ai0;
                this.rotation = rotation;
                this.itemID = itemID;
                this.rotSpeed = rotSpeed;
            }

            public static int SpwanItemID() {
                int id = CWRMod.RItemInstances[Main.rand.Next(CWRMod.RItemInstances.Count)].TargetID;
                Main.instance.LoadItem(id);
                return id;
            }

            public override void AI(float sengs) {
                if (--startTime > 0) {
                    return;
                }

                if (timeLeft > 60) {
                    if (alp < 255) {
                        alp++;
                    }
                }
                else {
                    if (alp > 0) {
                        alp -= 4;
                    }
                }

                position += velocity;
                rotation += rotSpeed;
                timeLeft--;
                if (timeLeft <= 0) {
                    active = false;
                }
            }

            public override void Draw(SpriteBatch spriteBatch, float sengs) {
                if (--startTime > 0) {
                    return;
                }
                Rectangle? rectangle = null;
                Vector2 orig = texture.Size() / 2;
                Color newColor = color * sengs * (alp / 255f);
                if (item != null) {
                    rectangle = Main.itemAnimations[item.type] != null ?
                        Main.itemAnimations[item.type].GetFrame(TextureAssets.Item[item.type].Value)
                        : TextureAssets.Item[item.type].Value.Frame(1, 1, 0, 0);
                }
                if (rectangle.HasValue) {
                    orig = rectangle.Value.Size() / 2;
                }
                spriteBatch.Draw(texture, position, rectangle, newColor, rotation, orig, size, SpriteEffects.None, 0);
            }
        }

        private int musicFade50;
        private float _sengs;
        internal bool _active;
        internal static AcknowledgmentUI Instance { get; private set; }
        private static Asset<Texture2D> Logo;
        internal List<ProjItem> projectiles = [];
        internal List<EffectEntity> effectEntities = [];

        private Vector2 itemPos => new Vector2(Main.screenWidth / 2, Main.screenHeight - 60);
        public bool OnActive() => _active || _sengs > 0;
        public override bool CanLoad() => true;
        public override void Load() {
            Instance = this;
            _sengs = 0;
            On_Main.UpdateAudio_DecideOnTOWMusic += DecideOnTOWMusicEvent;
            On_Main.UpdateAudio_DecideOnNewMusic += DecideOnNewMusicEvent;
        }
        void ICWRLoader.LoadAsset() => Logo = CWRUtils.GetT2DAsset("CalamityOverhaul/IntactLogo");
        void ICWRLoader.SetupData() => LoadName();
        private void LoadName() {
            names = [
            "[icon]",
            "雾梯" + textElement1,
            "子离似槜" + textElement1,
            "Cyrilly" + textElement2,
            "瓶中微光" + textElement2,
            "Monomon" + textElement2,
            "像樱花一样飘散吧" + textElement4,
            "洛千希" + textElement4,
            "闪耀£星辰" + textElement4,
            "蒹葭" + textElement4,
            "悬剑" + textElement4,
            "CataStrophe" + textElement4,
            "摸鱼的龙虾" + textElement3,
            "啊,胖子" + textElement3,
            "星星之火" + textElement3,
            "众星环绕" + textElement3,
            "respect" + textElement3,
            "鱼过海洋" + textElement3,
            "浮云落日" + textElement3,
            "生物音素" + textElement3,
            "快乐肥宅橘九" + textElement3,
            "半生浮云半生闲" + textElement3,
            "吐司" + textElement3,
            "冰冷小龙" + textElement3,
            "心酱" + textElement3,
            "天空之城" + textElement3,
            "Svetlana" + textElement3,
            "Murainm" + textElement3,
            "Sergei" + textElement3,
            "森林之心" + textElement3,
            "流浪者" + textElement3,
            "黑夜之光" + textElement3,
            "秋叶" + textElement3,
            "青空" + textElement3,
            "月光下的影子" + textElement3,
            "逐风者" + textElement3,
            "Ivan" + textElement3,
            "Olga" + textElement3,
            "Alexander" + textElement3,
            "Natalia" + textElement3,
            "Dmitry" + textElement3,
            "悠然见南山" + textElement3,
            "星河影" + textElement3,
            "ShadowHunter" + textElement3,
            "MysticWarrior" + textElement3,
            "StormBringer" + textElement3,
            "IceQueen" + textElement3,
            "Yelena" + textElement3,
            "Viktor" + textElement3,
            "白日梦想家" + textElement3,
            "追梦少年" + textElement3,
            "PhoenixRising" + textElement3,
            "DragonSlayer" + textElement3,
            "Vladislav" + textElement3,
            "Anastasia" + textElement3,
            "行者无疆" + textElement3,
            "蓝色星辰" + textElement3,
            "BlazeKnight" + textElement3,
            "ThunderGod" + textElement3,
            "StarLord" + textElement3,
            "天涯海角" + textElement3,
            "梦幻旅人" + textElement3,
            "风中的歌" + textElement3,
            "花间一壶酒" + textElement3,
            "凌云壮志" + textElement3,
            "Maxim" + textElement3,
            "Nikolai" + textElement3,
            "Tatiana" + textElement3,
            "寂静春天Ogger1943" + textElement3,
            "无尽之海" + textElement3,
            "Yuri" + textElement3,
            "Sasha" + textElement3,
            "苍穹之翼" + textElement3,
            "淮海不是明月" + textElement3,
            "剑心" + textElement3,
            "Ekaterina" + textElement3,
            "Mikhail" + textElement3,
            "Igor" + textElement3,
            "Lyudmila" + textElement3,
            "Artem" + textElement3,
            "Katerina" + textElement3,
            "Oleg" + textElement3,
            "烂柯棋缘" + textElement3,
            "华屋丘墟" + textElement3,
        ];
        }
        private void ToMusicFunc() {
            if (Main.gameMenu && OnActive()) {
                int targetID = MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/ED_WEH");
                for (int i = 0; i < Main.musicFade.Length; i++) {
                    if (i == targetID) {
                        continue;
                    }
                    Main.musicFade[i] = (musicFade50 / 120f);
                }
                Main.newMusic = targetID;
            }
        }
        private void DecideOnTOWMusicEvent(On_Main.orig_UpdateAudio_DecideOnTOWMusic orig, Main self) {
            orig.Invoke(self);
            ToMusicFunc();
        }
        private void DecideOnNewMusicEvent(On_Main.orig_UpdateAudio_DecideOnNewMusic orig, Main self) {
            orig.Invoke(self);
            ToMusicFunc();
        }

        public override void UnLoad() {
            Instance = null;
            Logo = null;
            _sengs = 0;
            On_Main.UpdateAudio_DecideOnTOWMusic -= DecideOnTOWMusicEvent;
            On_Main.UpdateAudio_DecideOnNewMusic -= DecideOnNewMusicEvent;
            names = null;
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

            try {
                LoadName();
                if (projectiles.Count < 50) {
                    Texture2D pts = CWRUtils.GetT2DValue(CWRConstant.Placeholder);
                    for (int i = 0; i < names.Length; i++) {
                        string textContent = names[i];
                        ProjItem proj = new ProjItem(i, 4500, 1, 0, Color.White, itemPos, new Vector2(0, -1), textContent, pts, i * 90);
                        projectiles.Add(proj);
                    }
                }
                if (effectEntities.Count < 10) {
                    Texture2D effectValue;
                    for (int i = 0; i < 10; i++) {
                        int id = EffectEntity.SpwanItemID();
                        effectValue = TextureAssets.Item[id].Value;
                        EffectEntity effect = new EffectEntity(i, 390, 1, 0, Color.White
                            , new Vector2(Main.rand.Next(Main.screenWidth), Main.rand.Next(Main.screenHeight))
                            , new Vector2(0, -1), names[i], effectValue, i * 90, 0, 0, id, Main.rand.NextFloat(-0.03f, 0.03f));
                        effectEntities.Add(effect);
                    }
                }

                foreach (ProjItem projItem in projectiles) {
                    if (projItem.index >= 0 && projItem.index < names.Length) {
                        projItem.text = names[projItem.index];
                    }
                    if (projItem.active) {
                        continue;
                    }
                    projItem.color.R -= 25;
                    projItem.timeLeft = 4500;
                    projItem.position = itemPos;
                    projItem.alp = 0;
                    projItem.active = true;
                }
                foreach (EffectEntity effect in effectEntities) {
                    if (effect.active) {
                        continue;
                    }
                    int id = EffectEntity.SpwanItemID();
                    Texture2D effectValue = TextureAssets.Item[id].Value;
                    effect.itemID = id;
                    effect.texture = effectValue;
                    effect.text = names[effect.index];
                    effect.position = new Vector2(Main.rand.Next(Main.screenWidth), Main.rand.Next(Main.screenHeight));
                    effect.rotSpeed = Main.rand.NextFloat(-0.03f, 0.03f);
                    effect.timeLeft = 360;
                    effect.velocity = new Vector2(0, -Main.rand.NextFloat(0.8f, 1.2f));
                    effect.alp = 0;
                    effect.active = true;
                }
            } catch {
                _sengs = 0;
                _active = false;
            }
        }
        public override void Update() {
            if (!OnActive()) {
                if (musicFade50 < 120) {
                    musicFade50++;
                }
                return;
            }

            if (musicFade50 > 0) {
                musicFade50--;
            }

            Initialize();

            foreach (ProjItem projItem in projectiles) {
                projItem.AI(_sengs);
            }
            foreach (EffectEntity effect in effectEntities) {
                effect.AI(_sengs);
            }

            //int mouS = DownStartL();
            int mouS = (int)keyLeftPressState;

            if (mouS == 1) {
                if (_sengs >= 1) {
                    SoundEngine.PlaySound(SoundID.MenuClose);
                    _active = false;
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
        }
        public override void Draw(SpriteBatch spriteBatch) {
            if (!OnActive()) {
                return;
            }
            spriteBatch.Draw(CWRUtils.GetT2DAsset(CWRConstant.Placeholder2).Value, Vector2.Zero
                , new Rectangle(0, 0, Main.screenWidth, Main.screenHeight)
                , Color.Black * _sengs * 0.85f, 0f, Vector2.Zero, 1, SpriteEffects.None, 0);
            foreach (EffectEntity effect in effectEntities) {
                effect.Draw(spriteBatch, _sengs);
            }
            foreach (ProjItem projItem in projectiles) {
                projItem.Draw(spriteBatch, _sengs);
            }
        }
    }
}
