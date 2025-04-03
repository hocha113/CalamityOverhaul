using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RemakeItems.Core;
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
        internal static string ArtistText => $" [{CWRLocText.GetTextValue("IconUI_Text3")}]";
        internal static string CodeAssistanceText => $" [{CWRLocText.GetTextValue("IconUI_Text4")}]";
        internal static string DonorText => $" [{CWRLocText.GetTextValue("IconUI_Text5")}]";
        internal static string BalanceTesterText => $" [{CWRLocText.GetTextValue("IconUI_Text6")}]";
        internal static string[] names = [];
        private int musicFade50;
        private float _sengs;
        internal bool _active;
        internal static AcknowledgmentUI Instance { get; private set; }
        private static Asset<Texture2D> Logo;
        internal List<ProjItem> projectiles = [];
        internal List<EffectEntity> effectEntities = [];
        private Vector2 itemPos => new Vector2(Main.screenWidth / 2, Main.screenHeight - 60);
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
                int id = ItemOverride.Instances[Main.rand.Next(ItemOverride.Instances.Count)].TargetID;
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
            "雾梯" + ArtistText,
            "Cyrilly" + CodeAssistanceText,
            "瓶中微光" + CodeAssistanceText,
            "Monomon" + CodeAssistanceText,
            "像樱花一样飘散吧" + BalanceTesterText,
            "洛千希" + BalanceTesterText,
            "闪耀£星辰" + BalanceTesterText,
            "蒹葭" + BalanceTesterText,
            "悬剑" + BalanceTesterText,
            "CataStrophe" + BalanceTesterText,
            "啊,胖子" + DonorText,
            "星星之火" + DonorText,
            "摸鱼的龙虾" + DonorText,
            "众星环绕" + DonorText,
            "respect" + DonorText,
            "鱼过海洋" + DonorText,
            "浮云落日" + DonorText,
            "生物音素" + DonorText,
            "快乐肥宅橘九" + DonorText,
            "半生浮云半生闲" + DonorText,
            "阿萨德沃荣托" + DonorText,
            "冰冷小龙" + DonorText,
            "心酱" + DonorText,
            "圣盗杰布微明" + DonorText,
            "柳冠希" + DonorText,
            "天空之城" + DonorText,
            "Svetlana" + DonorText,
            "Murainm" + DonorText,
            "Sergei" + DonorText,
            "森林之心" + DonorText,
            "流浪者" + DonorText,
            "黑夜之光" + DonorText,
            "秋叶" + DonorText,
            "青空" + DonorText,
            "月光下的影子" + DonorText,
            "逐风者" + DonorText,
            "Ivan" + DonorText,
            "Olga" + DonorText,
            "Alexander" + DonorText,
            "Natalia" + DonorText,
            "Dmitry" + DonorText,
            "悠然见南山" + DonorText,
            "星河影" + DonorText,
            "ShadowHunter" + DonorText,
            "MysticWarrior" + DonorText,
            "StormBringer" + DonorText,
            "无形剑" + DonorText,
            "Сырныйбарон336" + DonorText,
            "IceQueen" + DonorText,
            "Yelena" + DonorText,
            "Viktor" + DonorText,
            "白日梦想家" + DonorText,
            "追梦少年" + DonorText,
            "PhoenixRising" + DonorText,
            "DragonSlayer" + DonorText,
            "Vladislav" + DonorText,
            "Anastasia" + DonorText,
            "行者无疆" + DonorText,
            "蓝色星辰" + DonorText,
            "BlazeKnight" + DonorText,
            "ThunderGod" + DonorText,
            "StarLord" + DonorText,
            "天涯海角" + DonorText,
            "梦幻旅人" + DonorText,
            "风中的歌" + DonorText,
            "花间一壶酒" + DonorText,
            "凌云壮志" + DonorText,
            "Maxim" + DonorText,
            "Nikolai" + DonorText,
            "Tatiana" + DonorText,
            "寂静春天Ogger1943" + DonorText,
            "无尽之海" + DonorText,
            "Yuri" + DonorText,
            "Sasha" + DonorText,
            "苍穹之翼" + DonorText,
            "淮海不是明月" + DonorText,
            "剑心" + DonorText,
            "Ekaterina" + DonorText,
            "Mikhail" + DonorText,
            "Igor" + DonorText,
            "龙辰" + DonorText,
            "Lyudmila" + DonorText,
            "Artem" + DonorText,
            "Katerina" + DonorText,
            "Oleg" + DonorText,
            "Fwoer'Vmoerd" + DonorText,
            "苍穹彼岸offest" + DonorText,
            "Кот Пельмень" + DonorText,
            "我能看看你的小学吗" + DonorText,
            "烂柯棋缘" + DonorText,
            "华屋丘墟" + DonorText,
            "易燃易爆品daze" + DonorText,
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
            //运行环境比较敏感，为了防止玩家在卸载模组时还要和UI进行交互，这里判断一下资源是否已经被释放
            if (CWRAsset.Placeholder_White == null || CWRAsset.Placeholder_White.IsDisposed) {
                _active = false;
                return;
            }

            spriteBatch.Draw(CWRAsset.Placeholder_White.Value, Vector2.Zero
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
