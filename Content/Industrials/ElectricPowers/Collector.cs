using CalamityMod.Items.Materials;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers
{
    internal class Collector : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/Collector";
        internal static LocalizedText Text1;
        internal static LocalizedText Text2;
        internal static LocalizedText Text3;
        public override void SetStaticDefaults() {
            Text1 = this.GetLocalization(nameof(Text1), () => "Excessive Quantity!");
            Text2 = this.GetLocalization(nameof(Text2), () => "There are no boxes around!");
            Text3 = this.GetLocalization(nameof(Text3), () => "Lack of Electricity!");
        }
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 2, 40, 0);
            Item.rare = ItemRarityID.LightRed;
            Item.createTile = ModContent.TileType<CollectorTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 800;
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<DubiousPlating>(15).
                AddIngredient<MysteriousCircuitry>(20).
                AddRecipeGroup(CWRRecipes.TungstenBarGroup, 8).
                AddIngredient(ItemID.Hook, 3).
                AddTile(TileID.Anvils).
                Register();
        }
    }

    internal class CollectorTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/CollectorTile";
        [VaultLoaden(CWRConstant.Asset + "ElectricPowers/CollectorStartTile")]
        public static Asset<Texture2D> startAsset = null;
        [VaultLoaden(CWRConstant.Asset + "ElectricPowers/CollectorStartTileGlow")]
        public static Asset<Texture2D> startGlowAsset = null;
        [VaultLoaden(CWRConstant.Asset + "ElectricPowers/CollectorTileGlow")]
        public static Asset<Texture2D> tileGlowAsset = null;
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            AddMapEntry(new Color(67, 72, 81), VaultUtils.GetLocalizedItemName<Collector>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
            TileObjectData.newTile.Width = 3;
            TileObjectData.newTile.Height = 5;
            TileObjectData.newTile.Origin = new Point16(1, 3);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16, 16];
            TileObjectData.newTile.StyleWrapLimit = 36;
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile
                | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.addTile(Type);
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Electric);
            return false;
        }

        public override bool CanDrop(int i, int j) => false;

        public override bool RightClick(int i, int j) {
            if (TileProcessorLoader.AutoPositionGetTP(i, j, out CollectorTP collector)) {
                collector.RightClick(Main.LocalPlayer);
            }
            return base.RightClick(i, j);
        }

        public override void MouseOver(int i, int j) => Main.LocalPlayer.SetMouseOverByTile(ModContent.ItemType<Collector>());

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out CollectorTP collector)) {
                return false;
            }

            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            frameYPos += collector.frame * 18 * 5;
            Texture2D tex = collector.workState ? TextureAssets.Tile[Type].Value : startAsset.Value;
            Texture2D glow = collector.workState ? tileGlowAsset.Value : startGlowAsset.Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);
            if (!t.IsHalfBlock && t.Slope == 0) {
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
                spriteBatch.Draw(glow, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , Color.White, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            else if (t.IsHalfBlock) {
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
                spriteBatch.Draw(glow, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , Color.White, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            return false;
        }
    }

    internal class CollectorTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<CollectorTile>();
        public override int TargetItem => ModContent.ItemType<Collector>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 800;
        internal const int maxFindChestMode = 600;
        internal const int killerArmDistance = 2400;
        public Vector2 ArmPos => CenterInWorld + new Vector2(0, 14);
        private int textIdleTime;
        internal int frame;
        internal bool workState;
        internal bool BatteryPrompt;
        internal Item ItemFilter;
        internal int TagItemSign;
        internal int dontSpawnArmTime;
        internal int consumeUE = 8;
        internal int ArmIndex0 = -1;
        internal int ArmIndex1 = -1;
        internal int ArmIndex2 = -1;
        internal float hoverSengs;
        public override void SetBattery() {
            ItemFilter = new Item();
            //给予更多的绘制扩宽，因为爪手依赖TP的绘制，避免超出屏幕后爪手消失
            DrawExtendMode = 2200;
        }

        public override void SendData(ModPacket data) {
            base.SendData(data);
            ItemIO.Send(ItemFilter, data);
            data.Write(TagItemSign);
            data.Write(BatteryPrompt);
            data.Write(workState);
            data.Write(ArmIndex0);
            data.Write(ArmIndex1);
            data.Write(ArmIndex2);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            ItemFilter = ItemIO.Receive(reader);
            TagItemSign = reader.ReadInt32();
            BatteryPrompt = reader.ReadBoolean();
            workState = reader.ReadBoolean();
            ArmIndex0 = reader.ReadInt32();
            ArmIndex1 = reader.ReadInt32();
            ArmIndex2 = reader.ReadInt32();
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);

            ItemFilter ??= new Item();
            tag["_ItemFilter"] = ItemIO.Save(ItemFilter);

            string result;
            //因为对于模组来讲，物品的ID是动态的，为了避免模组变动导致的ID偏移问题，这里存储物品的内部名
            if (TagItemSign < ItemID.Count) {
                result = TagItemSign.ToString();
            }
            else {
                result = ItemLoader.GetItem(TagItemSign).FullName;
            }
            tag["_TagItemFullName"] = result;
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);

            if (tag.TryGet<TagCompound>("_ItemFilter", out var value)) {
                ItemFilter = ItemIO.Load(value);
            }
            else {
                ItemFilter = new Item();
            }

            if (tag.TryGet("_TagItemFullName", out string fullName)) {
                TagItemSign = VaultUtils.GetItemTypeFromFullName(fullName);
            }
            else {
                TagItemSign = ItemID.None;
            }
        }

        private void FindFrame() {
            int maxFrame = workState ? 7 : 24;
            if (!workState && frame == 23) {
                frame = 0;//立刻让帧归零防止越界
                workState = true;
                if (Main.dedServ) {
                    SendData();
                }
                SoundEngine.PlaySound(CWRSound.CollectorStart, PosInWorld);
            }
            VaultUtils.ClockFrame(ref frame, 5, maxFrame - 1);
        }

        internal static void CheckArm(ref int armIndex, int armID, Vector2 armPos) {
            if (armIndex < 0) {
                return;
            }

            Projectile projectile = Main.projectile.FindByIdentity(armIndex);
            if (!projectile.Alives() || projectile.type != armID) {
                armIndex = -1;
                return;
            }
        }

        internal void RightClick(Player player) {
            Item item = player.GetItem();
            if (!item.Alives()) {
                if (TagItemSign != ItemID.None) {
                    SoundEngine.PlaySound(CWRSound.Select with { Pitch = 0.2f });
                    TagItemSign = ItemID.None;
                }
                return;
            }
            if (TagItemSign > ItemID.None && TagItemSign == item.type) {
                SoundEngine.PlaySound(CWRSound.Select with { Pitch = 0.2f });
                TagItemSign = ItemID.None;
                return;
            }
            SoundEngine.PlaySound(CWRSound.Select with { Pitch = -0.2f });
            TagItemSign = item.type;

            if (TagItemSign == ModContent.ItemType<ItemFilter>()) {
                ItemFilter = item.Clone();
                var data = ItemFilter.GetGlobalItem<ItemFilterData>().Items.ToHashSet();
                ItemFilter.GetGlobalItem<ItemFilterData>().Items = data;//这一个来回用于切断和原物品的过滤内容引用粘连
            }

            SendData();
        }

        internal Chest FindChest(Item item) {
            Chest chest = Position.FindClosestChest(maxFindChestMode, true, (Chest c) => c.CanItemBeAddedToChest(item));
            if (chest == null && textIdleTime <= 0) {
                CombatText.NewText(HitBox, Color.YellowGreen, Collector.Text2.Value);
                textIdleTime = 300;
                //生成一个效果环
                for (int i = 0; i < 220; i++) {
                    Vector2 spwanPos = PosInWorld + VaultUtils.RandVr(maxFindChestMode, maxFindChestMode + 1);
                    int dust = Dust.NewDust(spwanPos, 2, 2, DustID.OrangeTorch, 0, 0);
                    Main.dust[dust].noGravity = true;
                }
            }
            return chest;
        }

        public override void UpdateMachine() {
            FindFrame();
            consumeUE = 8;
            if (!workState) {
                return;
            }

            if (HoverTP) {
                if (hoverSengs < 1f) {
                    hoverSengs += 0.1f;
                }
            }
            else {
                if (hoverSengs > 0f) {
                    hoverSengs -= 0.1f;
                }
            }

            if (textIdleTime > 0) {
                textIdleTime--;
            }
            if (dontSpawnArmTime > 0) {
                dontSpawnArmTime--;
            }

            if (VaultUtils.CountProjectilesOfID<CollectorArm>() > 300) {
                if (textIdleTime <= 0) {
                    CombatText.NewText(HitBox, Color.YellowGreen, Collector.Text1.Value);
                    textIdleTime = 300;
                }
                return;
            }

            if (ArmPos.FindClosestPlayer(killerArmDistance) != null && dontSpawnArmTime <= 0 && !VaultUtils.isClient) {
                bool doNet = false;

                CheckArm(ref ArmIndex0, ModContent.ProjectileType<CollectorArm>(), ArmPos);
                if (ArmIndex0 == -1) {
                    ArmIndex0 = Projectile.NewProjectileDirect(this.FromObjectGetParent(), ArmPos
                        , Vector2.Zero, ModContent.ProjectileType<CollectorArm>(), 0, 0, -1, ai0: 0, ai1: 0).identity;
                    doNet = true;
                }

                CheckArm(ref ArmIndex1, ModContent.ProjectileType<CollectorArm>(), ArmPos);
                if (ArmIndex1 == -1) {
                    ArmIndex1 = Projectile.NewProjectileDirect(this.FromObjectGetParent(), ArmPos
                        , Vector2.Zero, ModContent.ProjectileType<CollectorArm>(), 0, 0, -1, ai0: 0, ai1: 1).identity;
                    doNet = true;
                }

                CheckArm(ref ArmIndex2, ModContent.ProjectileType<CollectorArm>(), ArmPos);
                if (ArmIndex2 == -1) {
                    ArmIndex2 = Projectile.NewProjectileDirect(this.FromObjectGetParent(), ArmPos
                        , Vector2.Zero, ModContent.ProjectileType<CollectorArm>(), 0, 0, -1, ai0: 0, ai1: 2).identity;
                    doNet = true;
                }

                if (doNet) {
                    SendData();
                }
            }

            BatteryPrompt = MachineData.UEvalue < consumeUE;
            if (BatteryPrompt) {
                if (textIdleTime <= 0) {
                    CombatText.NewText(HitBox, Color.YellowGreen, Collector.Text3.Value);
                    textIdleTime = 300;
                }
            }
        }

        public override void PreTileDraw(SpriteBatch spriteBatch) {
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type != ModContent.ProjectileType<CollectorArm>()) {
                    continue;
                }
                if ((proj.ai[1] == 0 && ArmIndex0 == proj.identity)
                    || (proj.ai[1] == 1 && ArmIndex1 == proj.identity)
                    || (proj.ai[1] == 2 && ArmIndex2 == proj.identity)) {
                    ((CollectorArm)proj.ModProjectile).DoDraw(Lighting.GetColor(proj.Center.ToTileCoordinates()));
                }
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            //首先，绘制当前设置的目标物品图标
            if (TagItemSign > ItemID.None) {
                VaultUtils.SimpleDrawItem(Main.spriteBatch, TagItemSign
                    , CenterInWorld - Main.screenPosition + new Vector2(0, 32)
                    , itemWidth: 32, 0, 0, Lighting.GetColor(Position.ToPoint()));
            }

            //如果设置的目标物品是“物品过滤器”，并且鼠标正在悬停(hoverSengs > 0)
            //则以动画形式将过滤器内的物品环绕展开
            if (TagItemSign == ModContent.ItemType<ItemFilter>() && hoverSengs > 0.01f) {
                int[] filterItems = [.. ItemFilter.GetGlobalItem<ItemFilterData>().Items];
                if (filterItems.Length > 0) {
                    const float maxRadius = 150f;//定义展开的最大半径
                    float currentRadius = maxRadius * hoverSengs;//根据hoverSengs计算当前半径
                    float angleIncrement = MathHelper.TwoPi / filterItems.Length;//计算每个物品之间的角度间隔

                    Vector2 drawCenter = CenterInWorld - Main.screenPosition + new Vector2(0, 32);//中心点与目标物品图标一致

                    for (int i = 0; i < filterItems.Length; i++) {
                        if (filterItems[i] <= ItemID.None) continue;//跳过无效物品ID

                        //计算物品环绕排列的位置，-MathHelper.PiOver2是为了让第一个物品在正上方
                        float currentAngle = angleIncrement * i - MathHelper.PiOver2;
                        Vector2 offset = new Vector2((float)Math.Cos(currentAngle), (float)Math.Sin(currentAngle)) * currentRadius;
                        Vector2 itemPos = drawCenter + offset;

                        //物品也使用hoverSengs来控制淡入和缩放效果，使其动画更平滑
                        Color drawColor = VaultUtils.MultiStepColorLerp(hoverSengs, Lighting.GetColor(Position.ToPoint()), Color.White);
                        float scale = hoverSengs * 1.25f;

                        VaultUtils.SafeLoadItem(filterItems[i]);
                        VaultUtils.SimpleDrawItem(Main.spriteBatch, filterItems[i], itemPos, itemWidth: 32, scale, 0, drawColor);
                    }
                }
            }

            //最后绘制机器的充能条
            DrawChargeBar();
        }
    }

    internal class CollectorArm : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        [VaultLoaden("CalamityOverhaul/Assets/ElectricPowers/MechanicalArm")]
        private static Asset<Texture2D> arm = null;//手臂的体节纹理
        [VaultLoaden("CalamityOverhaul/Assets/ElectricPowers/MechanicalClamp")]
        private static Asset<Texture2D> clamp = null;//手臂的夹子纹理
        [VaultLoaden("CalamityOverhaul/Assets/ElectricPowers/MechanicalClampGlow")]
        private static Asset<Texture2D> clampGlow = null;//手臂的夹子的光效纹理
        internal CollectorTP collectorTP;
        internal Vector2 startPos;//记录这个弹幕的起点位置
        private Item graspItem;
        private bool spawn;
        internal Chest Chest;
        //不重要的东西的列表，比如小心心物品
        private readonly static HashSet<int> unimportances = [ItemID.Heart, ItemID.CandyCane, ItemID.CandyApple, ItemID.Star, ItemID.SoulCake];
        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 4000;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 120;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.WriteVector2(startPos);
            graspItem ??= new Item();
            ItemIO.Send(graspItem, writer, true);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            startPos = reader.ReadVector2();
            graspItem = ItemIO.Receive(reader, true);
        }

        internal Item FindItem() {
            Item item = null;
            float maxFindSQ = 4000000;
            int itemFilter = ModContent.ItemType<ItemFilter>();
            foreach (var i in Main.ActiveItems) {
                if (i.IsAir || !i.active) {
                    continue;
                }

                if (unimportances.Contains(i.type)) {
                    continue;
                }

                if (i.CWR().TargetByCollector >= 0 && i.CWR().TargetByCollector != Projectile.identity) {
                    continue;
                }

                if (collectorTP.TagItemSign == itemFilter) {
                    if (!collectorTP.ItemFilter.GetGlobalItem<ItemFilterData>().Items.Contains(i.type)) {
                        continue;
                    }
                }
                else if (collectorTP.TagItemSign > ItemID.None && i.type != collectorTP.TagItemSign) {
                    continue;
                }

                if (collectorTP.FindChest(i) == null) {
                    continue;
                }

                float newFindSQ = i.Center.DistanceSQ(Projectile.Center);
                if (newFindSQ < maxFindSQ) {
                    item = i;
                    maxFindSQ = newFindSQ;
                }
            }
            return item;
        }

        private static void CheckCoins(Chest chest) {
            //使用 long 类型来存储总价值，防止钱太多导致整数溢出
            long totalValue = 0;

            for (int i = 0; i < chest.item.Length; i++) {
                Item item = chest.item[i];
                if (item != null && !item.IsAir && item.IsACoin) {
                    int value = 1;
                    if (item.type == ItemID.SilverCoin) {
                        value = 100;
                    }
                    if (item.type == ItemID.GoldCoin) {
                        value = 10000;
                    }
                    if (item.type == ItemID.PlatinumCoin) {
                        value = 1000000;
                    }
                    totalValue += (long)value * item.stack;
                    //从箱子中移除旧的钱币
                    item.TurnToAir();
                }
            }

            //如果总价值为0，就没必要继续了
            if (totalValue <= 0) {
                return;
            }

            //从价值最高的铂金币开始往下换算
            if (totalValue >= 1000000) {
                int platinumCoins = (int)(totalValue / 1000000);
                chest.AddItem(new Item(ItemID.PlatinumCoin, platinumCoins));
                totalValue %= 1000000;
            }
            if (totalValue >= 10000) {
                int goldCoins = (int)(totalValue / 10000);
                chest.AddItem(new Item(ItemID.GoldCoin, goldCoins));
                totalValue %= 10000;
            }
            if (totalValue >= 100) {
                int silverCoins = (int)(totalValue / 100);
                chest.AddItem(new Item(ItemID.SilverCoin, silverCoins));
                totalValue %= 100;
            }
            if (totalValue > 0) {
                int copperCoins = (int)totalValue;
                chest.AddItem(new Item(ItemID.CopperCoin, copperCoins));
            }
        }

        public override void AI() {
            if (!spawn) {
                if (!VaultUtils.isClient) {
                    startPos = Projectile.Center;
                    Projectile.netUpdate = true;
                }
                spawn = true;
                Projectile.ai[2] = -1;//使用 ai[2] 存储目标物品的ID, -1代表无目标
            }

            if (!TileProcessorLoader.AutoPositionGetTP(startPos.ToTileCoordinates16(), out collectorTP)) {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;
            startPos = collectorTP.ArmPos;

            if (startPos.FindClosestPlayer(CollectorTP.killerArmDistance) == null) {
                collectorTP.dontSpawnArmTime = 60;
                Projectile.Kill();
                return;
            }

            //使用 localAI[0] 作为计时器
            Projectile.localAI[0]++;

            //状态 2: 携带物品前往箱子
            if (Projectile.ai[0] == 2) {
                if (graspItem == null || graspItem.type == ItemID.None) { //如果手上的物品没了, 重置
                    Projectile.ai[0] = 0;
                    return;
                }

                //检查箱子是否有效
                if (Chest == null || !Main.chest.Contains(Chest)) {
                    //箱子失效, 扔掉物品并重置
                    if (!VaultUtils.isClient) {
                        VaultUtils.SpwanItem(Projectile.GetSource_DropAsItem(), Projectile.Hitbox, graspItem);
                    }
                    graspItem.TurnToAir();
                    Projectile.ai[0] = 0;
                    Projectile.netUpdate = true;
                    return;
                }

                //移动到箱子并存入 (这段逻辑与你原来基本一致)
                Vector2 chestPos = new Vector2(Chest.x, Chest.y) * 16 + new Vector2(8, 8);
                float speed = 12 + Projectile.To(chestPos).Length() / 90f;
                Projectile.ChasingBehavior(chestPos, speed); //可以适当提高速度
                graspItem.Center = Projectile.Center;

                if (Projectile.Hitbox.Intersects(Chest.GetPoint16().ToWorldCoordinates().GetRectangle(32))) {
                    Chest.eatingAnimationTime = 20;
                    Chest.AddItem(graspItem, true);
                    CheckCoins(Chest);
                    graspItem.TurnToAir();
                    if (VaultUtils.isServer) {
                        NetMessage.SendData(MessageID.SyncItem, -1, -1, null, graspItem.whoAmI);
                    }
                    Projectile.ai[0] = 0; //任务完成，返回状态0
                    Projectile.netUpdate = true;
                }
                return;
            }

            //状态 1: 锁定目标，前往抓取
            if (Projectile.ai[0] == 1) {
                int targetWhoAmI = (int)Projectile.ai[2];
                if (targetWhoAmI < 0 || targetWhoAmI >= Main.maxItems) { //ID无效
                    Projectile.ai[0] = 0; //重置
                    return;
                }

                Item targetItem = Main.item[targetWhoAmI];

                //验证目标物品是否仍然有效
                if (!targetItem.active || targetItem.type == ItemID.None || targetItem.CWR().TargetByCollector != -1 && targetItem.CWR().TargetByCollector != Projectile.identity) {
                    Projectile.ai[0] = 0;//物品消失或被其他抓手锁定，放弃目标，返回状态0
                    Projectile.ai[2] = -1;
                    return;
                }

                float speed = 8 + Projectile.To(targetItem.Center).Length() / 90f;
                //飞向目标
                Projectile.ChasingBehavior(targetItem.Center, speed);
                Projectile.EntityToRot(Projectile.velocity.ToRotation(), 0.1f);

                //到达并抓取
                if (Projectile.Distance(targetItem.Center) < 32) {
                    graspItem = targetItem.Clone();
                    targetItem.TurnToAir();

                    if (VaultUtils.isServer) {
                        NetMessage.SendData(MessageID.SyncItem, -1, -1, null, targetItem.whoAmI);
                    }

                    //抓取成功后，再次确认箱子信息并存起来，然后切换到状态2
                    Chest = collectorTP.FindChest(graspItem);
                    graspItem.CWR().TargetByCollector = Projectile.identity;
                    Projectile.ai[0] = 2;//切换到状态2
                    Projectile.ai[2] = -1;//清除目标ID
                    Projectile.netUpdate = true;
                    SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.6f, Pitch = -0.1f }, Projectile.Center);
                }
                return;
            }
            //状态 0: 待机与搜索新目标
            else if (Projectile.ai[0] == 0) {
                //每 30 帧搜索一次，并且能量充足
                if (Projectile.localAI[0] >= 30 && collectorTP.MachineData.UEvalue >= collectorTP.consumeUE) {
                    Projectile.localAI[0] = 0; //重置计时器

                    Item foundItem = FindItem(); //昂贵操作只在这里被限时调用

                    if (foundItem != null) {
                        //在锁定目标前，先确认有地方放
                        var potentialChest = collectorTP.FindChest(foundItem);
                        if (potentialChest != null) {
                            //锁定目标，并切换到状态1
                            foundItem.CWR().TargetByCollector = Projectile.identity; //标记为自己的目标
                            Projectile.ai[2] = foundItem.whoAmI;
                            Projectile.ai[0] = 1;
                            Projectile.netUpdate = true;

                            //消耗能量
                            if (!VaultUtils.isClient) {
                                collectorTP.MachineData.UEvalue -= collectorTP.consumeUE;
                                collectorTP.SendData();
                            }
                        }
                    }
                }

                //待机状态下回到初始位置
                Vector2 offset = new Vector2(0, -120);
                if (Projectile.ai[1] == 1) {
                    offset = new Vector2(120, -20);
                }
                if (Projectile.ai[1] == 2) {
                    offset = new Vector2(-120, -20);
                }
                Projectile.ChasingBehavior(startPos + offset, 8);
                Projectile.EntityToRot(new Vector2(0, 1).ToRotation(), 0.1f);
            }
        }

        internal void DoDraw(Color lightColor) {
            if (startPos == Vector2.Zero) {
                return;
            }

            if (collectorTP?.BatteryPrompt == true) {
                lightColor.R /= 2;
                lightColor.G /= 2;
                lightColor.B /= 2;
                lightColor.A = 255;
            }

            Texture2D tex = arm.Value;
            Vector2 start = startPos;
            Vector2 end = Projectile.Center;

            //动态控制点偏移
            float dist = Vector2.Distance(start, end);
            float bendHeight = MathHelper.Clamp(dist * 0.5f, 40f, 200f);
            Vector2 midControl = (start + end) / 2 + new Vector2(0, -bendHeight);

            //估算真实曲线长度
            int sampleCount = 50;
            float curveLength = 0f;
            Vector2 prev = start;
            for (int i = 1; i <= sampleCount; i++) {
                float t = i / (float)sampleCount;
                Vector2 a = Vector2.Lerp(start, midControl, t);
                Vector2 b = Vector2.Lerp(midControl, end, t);
                Vector2 point = Vector2.Lerp(a, b, t);
                curveLength += Vector2.Distance(prev, point);
                prev = point;
            }

            float segmentLength = tex.Height / 2;
            int segmentCount = Math.Max(2, (int)(curveLength / segmentLength));
            Vector2[] points = new Vector2[segmentCount + 1];

            //构建点位
            for (int i = 0; i <= segmentCount; i++) {
                float t = i / (float)segmentCount;
                Vector2 pos = Vector2.Lerp(
                    Vector2.Lerp(start, midControl, t),
                    Vector2.Lerp(midControl, end, t),
                    t
                );
                points[i] = pos;
            }

            float clampRot = Projectile.rotation;

            for (int i = 0; i < segmentCount; i++) {
                Vector2 pos = points[i];
                Vector2 next = points[i + 1];
                Vector2 direction = next - pos;
                Color color = Lighting.GetColor((pos / 16).ToPoint());
                float rotation = direction.ToRotation() + MathHelper.PiOver2;
                if (i == segmentCount - 1) {
                    clampRot = direction.ToRotation();
                }
                Main.spriteBatch.Draw(tex, pos - Main.screenPosition, null, color, rotation
                    , new Vector2(tex.Width / 2f, tex.Height), 1f, SpriteEffects.None, 0f);
            }

            Main.spriteBatch.Draw(clamp.Value, Projectile.Center - Main.screenPosition
                , clamp.Value.GetRectangle((graspItem == null || graspItem.IsAir) ? 0 : 1, 2)
                , lightColor, clampRot + MathHelper.PiOver2
                , clamp.Value.GetOrig(2), 1f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(clampGlow.Value, Projectile.Center - Main.screenPosition
                , clampGlow.Value.GetRectangle((graspItem == null || graspItem.IsAir) ? 0 : 1, 2)
                , Color.White, clampRot + MathHelper.PiOver2
                , clampGlow.Value.GetOrig(2), 1f, SpriteEffects.None, 0f);

            if (graspItem != null && !graspItem.IsAir) {
                VaultUtils.SimpleDrawItem(Main.spriteBatch, graspItem.type
                    , Projectile.Center - Main.screenPosition, 1f
                    , clampRot + MathHelper.PiOver2, lightColor);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
