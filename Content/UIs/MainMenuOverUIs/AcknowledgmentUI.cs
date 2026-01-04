using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye;
using InnoVault.GameSystem;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs.MainMenuOverUIs
{
    internal class AcknowledgmentUI : UIHandle, ICWRLoader, IUpdateAudio
    {
        internal static string ArtistText => $" [{CWRLocText.GetTextValue("IconUI_Text3")}]";
        internal static string CodeAssistanceText => $" [{CWRLocText.GetTextValue("IconUI_Text4")}]";
        internal static string MusicianText => $" [{CWRLocText.GetTextValue("IconUI_Text9")}]";
        internal static string DonorText => $" [{CWRLocText.GetTextValue("IconUI_Text5")}]";
        internal static string BalanceTesterText => $" [{CWRLocText.GetTextValue("IconUI_Text6")}]";
        internal static string[] names = [];
        private int musicFade50;
        private float _sengs;
        internal bool _active;
        internal static AcknowledgmentUI Instance;
        [VaultLoaden("CalamityOverhaul/IntactLogo")]
        private static Asset<Texture2D> Logo = null;
        internal List<ProjItem> projectiles = [];
        internal List<EffectEntity> effectEntities = [];
        internal List<NPCGhostItem> npcGhostItem = [];
        private static Vector2 ItemPos => new(Main.screenWidth / 2, Main.screenHeight - 60);
        private const int projTimer = 4600;
        public override LayersModeEnum LayersMode => LayersModeEnum.Mod_MenuLoad;
        public override bool Active => CWRLoad.OnLoadContentBool;

        internal class NPCGhostItem : ProjItem
        {
            private float rotation;
            private int frameConter;
            private int frameIndex;

            public NPCGhostItem(
                int index, int timeLeft, float size, int alp, Color color,
                Vector2 position, Vector2 velocity, string text, Texture2D texture, int startTime)
                : base(index, timeLeft, size, alp, color, position, velocity, text, texture, startTime) {

            }

            public override void AI(float sengs) {
                if (--startTime > 0) {
                    return;
                }

                position += velocity;
                rotation = velocity.ToRotation() - MathHelper.PiOver2;
                timeLeft--;

                if (timeLeft > 60) {
                    alp = Math.Min(alp + 5, 255);
                }
                else {
                    alp = Math.Max(alp - 4, 0);
                }

                if (timeLeft <= 0 || alp <= 0) {
                    active = false;
                }

                if (++frameConter > 5) {
                    if (++frameIndex > 3) {
                        frameIndex = 0;
                    }
                    frameConter = 0;
                }
            }

            public override void Draw(SpriteBatch spriteBatch, float sengs) {
                if (startTime > 0) {
                    return;
                }

                //用主材质或替代材质
                Texture2D tex = index == 0 ? TwinsAIController.SpazmatismAsset.Value : TwinsAIController.RetinazerAsset.Value;
                if (index == 2) {
                    tex = TwinsAIController.SpazmatismAltAsset.Value;
                }
                if (index == 3) {
                    tex = TwinsAIController.RetinazerAltAsset.Value;
                }
                Rectangle frameRect = tex.GetRectangle(frameIndex, 4);
                SpriteEffects effects = velocity.X >= 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
                Vector2 origin = frameRect.Size() / 2f;
                float drawRot = rotation + MathHelper.PiOver2;
                Color color = Color.White * (alp / 255f) * sengs;
                //残影轨迹模拟
                float trailOpacity = 0.2f;
                float trailScale = size * 0.7f;
                for (int i = 0; i < 5; i++) {
                    Vector2 offset = new Vector2(i * -velocity.X * 2f, i * -velocity.Y * 2f);
                    Color trailColor = color * trailOpacity;
                    spriteBatch.Draw(tex, position + offset, frameRect, trailColor,
                        drawRot, origin, trailScale, effects, 0f);
                    trailOpacity *= 0.75f;
                    trailScale *= 0.95f;
                }

                //主体绘制
                Color colorFinal = color;
                spriteBatch.Draw(tex, position, frameRect, colorFinal,
                    drawRot, origin, size, effects, 0f);
            }
        }

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
            private Item Item {
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
                List<ItemOverride> list = [.. ItemOverride.Instances.Where(inds => inds.Mod == CWRMod.Instance)];
                int id = list[Main.rand.Next(list.Count)].TargetID;
                Main.instance.LoadItem(id);
                return id;
            }

            public override void AI(float sengs) {
                if (--startTime > 0) {
                    return;
                }

                if (timeLeft > 60) {
                    alp = Math.Min(255, alp + 5);
                }
                else {
                    alp = Math.Max(0, alp - 4);
                }

                //波动 + 曲线偏移轨迹
                float wave = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + sengs + itemID) * 0.5f;
                float curve = (float)Math.Cos(Main.GlobalTimeWrappedHourly * 1.2f + sengs + itemID) * 0.3f;
                Vector2 drift = new Vector2(curve, wave) * 0.8f;

                //旋转渐变
                rotSpeed += (Main.rand.NextFloat() - 0.5f) * 0.002f;
                rotSpeed = MathHelper.Clamp(rotSpeed, -0.03f, 0.03f);
                rotation += rotSpeed;

                //漂浮
                position += velocity + drift;

                //透明度闪动
                if (timeLeft % 20 == 0 && timeLeft < 60) {
                    alp -= Main.rand.Next(5, 15);
                }

                //生命周期终止
                timeLeft--;
                if (timeLeft <= 0 || alp <= 0) {
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
                if (Item != null) {
                    rectangle = Main.itemAnimations[Item.type] != null ?
                        Main.itemAnimations[Item.type].GetFrame(TextureAssets.Item[Item.type].Value)
                        : TextureAssets.Item[Item.type].Value.Frame(1, 1, 0, 0);
                }
                if (rectangle.HasValue) {
                    orig = rectangle.Value.Size() / 2;
                }
                spriteBatch.Draw(texture, position, rectangle, newColor, rotation, orig, size, SpriteEffects.None, 0);
            }
        }

        public static bool OnActive() {
            if (Instance == null) {
                return false;
            }
            return Instance._active || Instance._sengs > 0;
        }
        public override bool CanLoad() => true;
        void ICWRLoader.SetupData() => LoadName();
        public override void SetStaticDefaults() {
            Instance = UIHandleLoader.GetUIHandleOfType<AcknowledgmentUI>();
            _sengs = 0;
        }
        private static void LoadName() {
            names = [
            "[icon]",
            "雾梯" + ArtistText,
            "Cyrilly" + CodeAssistanceText,
            "瓶中微光" + CodeAssistanceText,
            "Monomon" + CodeAssistanceText,
            "Ryusa" + MusicianText,
            "像樱花一样飘散吧" + BalanceTesterText,
            "洛千希" + BalanceTesterText,
            "闪耀£星辰" + BalanceTesterText,
            "蒹葭" + BalanceTesterText,
            "悬剑" + BalanceTesterText,
            "CataStrophe" + BalanceTesterText,
            "啊,胖子" + DonorText,
            "Reficul" + DonorText,
            "星星之火" + DonorText,
            "摸鱼的龙虾" + DonorText,
            "众星环绕" + DonorText,
            "respect" + DonorText,
            "鱼过海洋" + DonorText,
            "猫猫爱睡觉觉" + DonorText,
            "浮云落日" + DonorText,
            "生物音素" + DonorText,
            "快乐肥宅橘九" + DonorText,
            "半生浮云半生闲" + DonorText,
            "阿萨德沃荣托" + DonorText,
            "冰冷小龙" + DonorText,
            "心酱" + DonorText,
            "LEI雷克斯" + DonorText,
            "尼古丁真" + DonorText,
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
            "Sodayo 的 Live" + DonorText,
            "我能看看你的小学吗" + DonorText,
            "烂柯棋缘" + DonorText,
            "华屋丘墟" + DonorText,
            "易燃易爆品daze" + DonorText,
        ];
        }

        void IUpdateAudio.DecideMusic() {
            if (!Main.gameMenu || !OnActive()) {
                return;
            }

            int targetID = MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/ED_WEH");
            for (int i = 0; i < Main.musicFade.Length; i++) {
                if (i == targetID) {
                    continue;
                }
                Main.musicFade[i] = (musicFade50 / 120f);
            }
            Main.newMusic = targetID;
        }

        public override void UnLoad() {
            Instance = null;
            _sengs = 0;
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
                        ProjItem proj = new ProjItem(i, projTimer, 1, 0, Color.White, ItemPos, new Vector2(0, -1), textContent, pts, i * 90);
                        projectiles.Add(proj);
                    }
                }

                //更新 projItem
                foreach (ProjItem projItem in projectiles) {
                    if (projItem.index >= 0 && projItem.index < names.Length) {
                        projItem.text = names[projItem.index];
                    }
                    if (projItem.active) {
                        continue;
                    }

                    projItem.color.R -= 25;
                    projItem.timeLeft = projTimer;
                    projItem.position = ItemPos;
                    projItem.alp = 0;
                    projItem.active = true;
                }

                if (effectEntities.Count < 10) {
                    for (int i = 0; i < 10; i++) {
                        int id = EffectEntity.SpwanItemID();
                        Texture2D effectValue = TextureAssets.Item[id].Value;
                        EffectEntity effect = new EffectEntity(i, 390, 1, 0, Color.White,
                            new Vector2(Main.rand.Next(Main.screenWidth), Main.rand.Next(Main.screenHeight)),
                            new Vector2(0, -1), names[i], effectValue, i * 90, 0, 0, id, Main.rand.NextFloat(-0.03f, 0.03f));
                        effectEntities.Add(effect);
                    }
                }

                //更新 effectEntities
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

                //初始化 NPCGhostItem
                if (npcGhostItem.Count < 1) {
                    Texture2D tex = TextureAssets.Npc[NPCID.Spazmatism].Value;
                    Texture2D alt = TextureAssets.NpcHeadBoss[18].Value; //示例替代图
                    Vector2 npcStartPosition;
                    Vector2 npcVelocity;
                    if (Main.rand.NextBool()) {
                        npcStartPosition = new Vector2(Main.screenWidth + 100, Main.rand.Next(150, Main.screenHeight - 150));
                        npcVelocity = new Vector2(-Main.rand.NextFloat(1f, 2f), Main.rand.NextFloat(-0.3f, 0.3f));
                    }
                    else {
                        npcStartPosition = new Vector2(-100, Main.rand.Next(150, Main.screenHeight - 150));
                        npcVelocity = new Vector2(Main.rand.NextFloat(1f, 2f), Main.rand.NextFloat(-0.3f, 0.3f));
                    }
                    NPCGhostItem ghost = new NPCGhostItem(
                        index: Main.rand.Next(4),
                        timeLeft: Main.rand.Next(1400, 1600),
                        size: 1.2f,
                        alp: 0,
                        color: Color.White,
                        position: npcStartPosition,
                        velocity: npcVelocity,
                        text: "npc",
                        texture: null,
                        startTime: Main.rand.Next(120)
                    );
                    npcGhostItem.Add(ghost);
                }

                //更新 NPCGhostItem
                foreach (NPCGhostItem ghost in npcGhostItem) {
                    if (ghost.active) {
                        continue;
                    }

                    ghost.index = Main.rand.Next(4);
                    ghost.startTime = Main.rand.Next(60, 360);
                    ghost.timeLeft = Main.rand.Next(1400, 1600);
                    ghost.alp = 0;
                    ghost.active = true;
                    if (Main.rand.NextBool()) {
                        ghost.position = new Vector2(Main.screenWidth + 100, Main.rand.Next(150, Main.screenHeight - 150));
                        ghost.velocity = new Vector2(-Main.rand.NextFloat(1f, 2f), Main.rand.NextFloat(-0.3f, 0.3f));
                    }
                    else {
                        ghost.position = new Vector2(-100, Main.rand.Next(150, Main.screenHeight - 150));
                        ghost.velocity = new Vector2(Main.rand.NextFloat(1f, 2f), Main.rand.NextFloat(-0.3f, 0.3f));
                    }
                    ghost.text = "npc";
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
            foreach (NPCGhostItem npc in npcGhostItem) {
                npc.AI(_sengs);
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
            foreach (NPCGhostItem npc in npcGhostItem) {
                npc.Draw(spriteBatch, _sengs);
            }
            foreach (ProjItem projItem in projectiles) {
                projItem.Draw(spriteBatch, _sengs);
            }
        }
    }
}
