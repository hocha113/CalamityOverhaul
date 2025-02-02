using CalamityMod.Items.LoreItems;
using CalamityMod.Items.Materials;
using CalamityOverhaul.Content.Tiles;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI.Chat;

namespace CalamityOverhaul.Content.Items.Tools
{
    internal class DarMatterGlobal : GlobalItem
    {
        public List<Item> dorpItems = [];
        public override bool InstancePerEntity => true;
        public override bool AppliesToEntity(Item entity, bool lateInstantiation) => entity.type == DarkMatterBall.ID;
        public override void SaveData(Item item, TagCompound tag) {
            if (item.type == DarkMatterBall.ID && dorpItems != null && dorpItems.Count > 0) {
                dorpItems.RemoveAll(item => item == null || item.type == ItemID.None);//清理
                if (dorpItems.Count > 0) {//清理后再判断一次
                    tag["DarkMatterBall_dorpItems"] = dorpItems;
                }
            }
        }
        public override void LoadData(Item item, TagCompound tag) {
            if (item.type == DarkMatterBall.ID) {
                if (!tag.TryGet("DarkMatterBall_dorpItems", out dorpItems)) {
                    dorpItems = [];//如果没有存档就初始化
                }
            }
        }
    }

    internal class DarMatterUI : UIHandle
    {
        public static DarMatterUI Instance => UIHandleLoader.GetUIHandleOfType<DarMatterUI>();
        public static DarMatterGlobal darMatterGlobal;
        public bool uiActive {
            get {
                if (Main.gameMenu || Main.HoverItem.type != DarkMatterBall.ID) {
                    return false;
                }

                if (Main.HoverItem.TryGetGlobalItem<DarMatterGlobal>(out var gItem)) {
                    darMatterGlobal = gItem;
                    return true;
                }
                else {
                    return false;
                }
            }
        }
        public override bool Active => uiActive || _sengs > 0;
        public List<DarMatterItemVidElement> darMatterItemVidElements = [];
        public float _sengs;
        public override void Update() {
            if (uiActive) {
                if (_sengs < 1) {
                    _sengs += 0.1f;
                }
            }
            else {
                if (_sengs > 0) {
                    _sengs -= 0.1f;
                }
            }

            _sengs = MathHelper.Clamp(_sengs, 0, 1f);

            darMatterItemVidElements.Clear();
            if (darMatterGlobal != null) {
                foreach (var inds in darMatterGlobal.dorpItems) {
                    DarMatterItemVidElement darMatterItemVidElement = new DarMatterItemVidElement();
                    darMatterItemVidElement.Item = inds;
                    darMatterItemVidElements.Add(darMatterItemVidElement);
                }
            }

            for (int i = 0; i < darMatterItemVidElements.Count; i++) {
                DarMatterItemVidElement darMatterItemVidElement = darMatterItemVidElements[i];
                darMatterItemVidElement.DrawPosition = DrawPosition + new Vector2(0, DarMatterItemVidElement.Height) * (2 + i) * _sengs;
                darMatterItemVidElement.Update();
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (darMatterItemVidElements.Count > 0) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value
                , DarkMatterBall.Contents.Value, DrawPosition.X, DrawPosition.Y + 36 * _sengs
                , Color.White * _sengs, Color.Black * _sengs, new Vector2(0.2f), 0.8f);
            }

            foreach (DarMatterItemVidElement darMatterItemVidElement in darMatterItemVidElements) {
                darMatterItemVidElement.Draw(spriteBatch);
            }
        }
    }

    internal class DarMatterItemVidElement : UIHandle
    {
        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        public static int Weith => 32;
        public static int Height => 32;
        private float _sengs;
        public Item Item = new Item();
        public override void Update() {
            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, Weith, Height);
            Rectangle mouseHit = new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1);
            hoverInMainPage = UIHitBox.Intersects(mouseHit);
            if (hoverInMainPage) {
                if (_sengs < 1f) {
                    _sengs += 0.08f;
                }
            }
            else {
                if (_sengs > 0f) {
                    _sengs -= 0.08f;
                }
            }

            _sengs = MathHelper.Clamp(_sengs, 0, 1f);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (Item.type <= ItemID.None) {
                return;
            }

            string text = Item.Name;
            if (Item.stack > 1) {
                text += " " + Item.stack.ToString();
            }
            Color barkColor = Color.Lerp(Color.AliceBlue, Color.Goldenrod, _sengs);
            Color centerColor = Color.Lerp(Color.Azure, Color.WhiteSmoke, _sengs);

            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 2, DrawPosition
                , Weith + (int)(_sengs * 40), Height, barkColor * 0.8f, centerColor * 0.2f, 1);

            float drawSize = VaultUtils.GetDrawItemSize(Item, Weith) * 1;
            Vector2 drawPos = DrawPosition + new Vector2(Weith, Height) / 2;
            Main.instance.LoadItem(Item.type);
            VaultUtils.SimpleDrawItem(spriteBatch, Item.type, drawPos, drawSize, 0, Color.White);

            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value
                , text, drawPos.X + 18, drawPos.Y - 8
                , Color.White, Color.Black, new Vector2(0.2f), 0.8f);
        }
    }

    internal class DarkMatterBall : ModItem, ICWRLoader
    {
        public override string Texture => CWRConstant.Item + "Tools/DarkMatter";
        internal static int ID { get; private set; }
        internal static Asset<Texture2D> DarkMatter { get; private set; }
        internal static Asset<Texture2D> Full { get; private set; }
        internal static LocalizedText Contents { get; private set; }
        public List<Item> DorpItems {
            get {
                if (Item.TryGetGlobalItem<DarMatterGlobal>(out var gItem)) {
                    return gItem.dorpItems;
                }
                return [];
            }
            set {
                if (Item.TryGetGlobalItem<DarMatterGlobal>(out var gItem)) {
                    value ??= []; // 避免 null 赋值
                    List<Item> itemList = new(); // 用于存储合并后的物品
                    foreach (var item in value) {
                        CWRUtils.MergeItemStacks(itemList, item);
                    }
                    gItem.dorpItems = itemList; // 更新物品列表
                }
            }
        }
        private bool deposit;
        void ICWRLoader.LoadAsset() {
            DarkMatter = CWRUtils.GetT2DAsset(CWRConstant.Item + "Tools/DarkMatter");
            Full = CWRUtils.GetT2DAsset(CWRConstant.Item + "Tools/Full");
        }
        void ICWRLoader.UnLoadData() {
            DarkMatter = null;
            Full = null;
        }
        public override ModItem Clone(Item newEntity) {
            DarkMatterBall ball = (DarkMatterBall)base.Clone(newEntity);
            ball.DorpItems = new List<Item>(DorpItems);
            return ball;
        }
        public override void SetStaticDefaults() {
            Contents = this.GetLocalization("Contents", () => "Contents:");
            Item.ResearchUnlockCount = 9999;
            ID = Type;
        }
        public override void SetDefaults() {
            Item.maxStack = 99;
            Item.width = 24;
            Item.height = 24;
            Item.rare = ItemRarityID.Purple;
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems_DarkMatterBall;
        }

        public static void DrawItemIcon(SpriteBatch spriteBatch, Vector2 position, int Type, float alp = 1) {
            spriteBatch.Draw(DarkMatter.Value, position, null, Color.White * alp, 0, DarkMatter.Value.Size() / 2, 1, SpriteEffects.None, 0);
            float sngs = Math.Abs(MathF.Sin(Main.GameUpdateCount * 0.01f));
            spriteBatch.Draw(Full.Value, position, null, Color.White * sngs * (0.5f + alp * 0.5f), 0, Full.Value.Size() / 2, 1, SpriteEffects.None, 0);
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position
            , Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            DrawItemIcon(spriteBatch, position, Type);
            return false;
        }

        public override bool CanRightClick() {
            LoadDorp();
            if (Main.mouseItem.type > ItemID.None && Main.mouseItem.type != ModContent.ItemType<DarkMatterBall>()) {//不要自己右键自己
                DorpItems.Add(Main.mouseItem.Clone());
                Main.mouseItem.TurnToAir();
                SoundEngine.PlaySound(SoundID.Grab);
                deposit = true;
                return true;
            }
            return DorpItems.Count > 0;
        }

        public void LoadDorp() {
            if (DorpItems == null) {
                DorpItems = [];
            }
        }

        public override bool ConsumeItem(Player player) => false;

        public override void RightClick(Player player) {
            if (deposit) {
                deposit = false;
                return;
            }
            LoadDorp();
            foreach (var item in DorpItems) {
                player.QuickSpawnItem(player.FromObjectGetParent(), item, item.stack);
            }
            DorpItems = [];
        }

        public override void OnStack(Item source, int numToTransfer) {
            DarkMatterBall darkMatterBall = (DarkMatterBall)source.ModItem;
            DorpItems.AddRange(darkMatterBall.DorpItems);
            LoadDorp();
        }

        public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset) {
            LoadDorp();
            //都判断一遍，防止改变了本地化后检测不到文本行
            if (line.Name == "Tooltip0" || line.Name == "Tooltip1" || line.Name == "Tooltip2" && line.Mod == "Terraria") {
                Vector2 basePosition = new Vector2(line.X, line.Y) + new Vector2(0, 10);
                DarMatterUI.Instance.DrawPosition = basePosition;
            }
            return true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<ShadowspecBar>(24)
                .AddIngredient<AuricBar>(20)
                .AddIngredient<MiracleMatter>(16)
                .AddIngredient<YharonSoulFragment>(8)
                .AddIngredient<LoreCynosure>()
                .AddBlockingSynthesisEvent()
                .AddTile(ModContent.TileType<TransmutationOfMatter>())
                .Register();
        }
    }
}
