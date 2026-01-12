using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Throwers
{
    /// <summary>
    /// 投掷者机器处理器
    /// 负责存储物品并按设定参数投掷出去
    /// </summary>
    internal class ThrowerTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<ThrowerTile>();
        public override int TargetItem => ModContent.ItemType<Thrower>();
        public override bool ReceivedEnergy => true;
        public override bool CanDrop => false;
        public override float MaxUEValue => 500;

        #region 常量

        //每次投掷消耗的能量
        internal const int ConsumeUE = 2;
        //最大存储槽位(5列4行)
        internal const int MaxSlots = 20;

        #endregion

        #region 字段

        //存储的物品
        internal List<Item> StoredItems = [];

        //投掷参数
        internal bool IsThrowing;
        internal float ThrowSpeed = 8f;
        internal float ThrowAngle;
        internal int ThrowInterval = 30;
        //弹药发射模式(如果物品是弹药则发射对应弹幕)
        internal bool AmmoShootMode = true;

        //状态
        internal int ThrowTimer;
        internal int TextIdleTime;
        internal bool IsWorking;
        internal float GlowIntensity;

        //投掷方向(以角度存储，0为右，90为上)
        internal float ThrowDirection;

        #endregion

        #region 属性

        public Vector2 LaunchPosition => CenterInWorld + new Vector2(0, -8);

        #endregion

        public override void SetBattery() {
            StoredItems = [];
            ThrowDirection = -45f;
        }

        public override void Initialize() {
            StoredItems ??= [];
        }

        #region 数据同步

        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(IsThrowing);
            data.Write(ThrowSpeed);
            data.Write(ThrowAngle);
            data.Write(ThrowInterval);
            data.Write(ThrowDirection);
            data.Write(AmmoShootMode);

            data.Write(StoredItems.Count);
            foreach (var item in StoredItems) {
                if (item == null) {
                    ItemIO.Send(new Item(), data, true);
                }
                else {
                    ItemIO.Send(item, data, true);
                }
            }
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            IsThrowing = reader.ReadBoolean();
            ThrowSpeed = reader.ReadSingle();
            ThrowAngle = reader.ReadSingle();
            ThrowInterval = reader.ReadInt32();
            ThrowDirection = reader.ReadSingle();
            AmmoShootMode = reader.ReadBoolean();

            int count = reader.ReadInt32();
            StoredItems.Clear();
            for (int i = 0; i < count; i++) {
                Item item = ItemIO.Receive(reader, true);
                StoredItems.Add(item);
            }
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            try {
                tag["_IsThrowing"] = IsThrowing;
                tag["_ThrowSpeed"] = ThrowSpeed;
                tag["_ThrowAngle"] = ThrowAngle;
                tag["_ThrowInterval"] = ThrowInterval;
                tag["_ThrowDirection"] = ThrowDirection;
                tag["_AmmoShootMode"] = AmmoShootMode;

                List<TagCompound> itemTags = [];
                foreach (var item in StoredItems) {
                    if (item == null) {
                        itemTags.Add(ItemIO.Save(new Item()));
                    }
                    else {
                        itemTags.Add(ItemIO.Save(item));
                    }
                }
                tag["_StoredItems"] = itemTags;
            } catch (Exception ex) {
                VaultMod.Instance.Logger.Error($"ThrowerTP.SaveData Error: {ex.Message}");
            }
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            try {
                if (tag.TryGet("_IsThrowing", out bool throwing)) {
                    IsThrowing = throwing;
                }
                if (tag.TryGet("_ThrowSpeed", out float speed)) {
                    ThrowSpeed = speed;
                }
                if (tag.TryGet("_ThrowAngle", out float angle)) {
                    ThrowAngle = angle;
                }
                if (tag.TryGet("_ThrowInterval", out int interval)) {
                    ThrowInterval = interval;
                }
                if (tag.TryGet("_ThrowDirection", out float direction)) {
                    ThrowDirection = direction;
                }
                if (tag.TryGet("_AmmoShootMode", out bool ammoMode)) {
                    AmmoShootMode = ammoMode;
                }

                if (tag.TryGet("_StoredItems", out List<TagCompound> itemTags)) {
                    StoredItems.Clear();
                    foreach (var itemTag in itemTags) {
                        StoredItems.Add(ItemIO.Load(itemTag));
                    }
                }
            } catch (Exception ex) {
                VaultMod.Instance.Logger.Error($"ThrowerTP.LoadData Error: {ex.Message}");
            }
        }

        #endregion

        #region UI交互

        public void OpenUI(Player player) {
            ThrowerUI.Instance?.Initialize(this);
        }

        /// <summary>
        /// 尝试添加物品到存储
        /// </summary>
        public bool TryAddItem(Item item) {
            if (item == null || item.IsAir) {
                return false;
            }

            //尝试堆叠到现有物品
            foreach (var stored in StoredItems) {
                if (stored.type == item.type && stored.stack < stored.maxStack) {
                    int addAmount = Math.Min(item.stack, stored.maxStack - stored.stack);
                    stored.stack += addAmount;
                    item.stack -= addAmount;
                    if (item.stack <= 0) {
                        item.TurnToAir();
                        return true;
                    }
                }
            }

            //添加新槽位
            if (StoredItems.Count < MaxSlots && item.stack > 0) {
                StoredItems.Add(item.Clone());
                item.TurnToAir();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 获取下一个要投掷的物品(不移除)
        /// </summary>
        private Item GetNextThrowItem() {
            for (int i = 0; i < StoredItems.Count; i++) {
                if (StoredItems[i] != null && !StoredItems[i].IsAir) {
                    return StoredItems[i];
                }
            }
            return null;
        }

        /// <summary>
        /// 消耗一个物品用于投掷
        /// </summary>
        private Item ConsumeOneItem() {
            for (int i = 0; i < StoredItems.Count; i++) {
                if (StoredItems[i] != null && !StoredItems[i].IsAir) {
                    Item result = StoredItems[i].Clone();
                    result.stack = 1;

                    StoredItems[i].stack--;
                    if (StoredItems[i].stack <= 0) {
                        StoredItems.RemoveAt(i);
                    }
                    return result;
                }
            }
            return null;
        }

        #endregion

        #region 更新逻辑

        public override void UpdateMachine() {
            if (TextIdleTime > 0) {
                TextIdleTime--;
            }

            //更新发光强度
            if (IsWorking) {
                GlowIntensity = Math.Min(1f, GlowIntensity + 0.05f);
            }
            else {
                GlowIntensity = Math.Max(0f, GlowIntensity - 0.05f);
            }

            //检查能量
            if (MachineData.UEvalue < ConsumeUE) {
                IsWorking = false;
                if (IsThrowing && TextIdleTime <= 0) {
                    CombatText.NewText(HitBox, new Color(255, 180, 100), Thrower.NoEnergyText.Value);
                    TextIdleTime = 180;
                }
                return;
            }

            //检查是否有物品可投掷
            Item nextItem = GetNextThrowItem();
            if (nextItem == null) {
                IsWorking = false;
                if (IsThrowing && TextIdleTime <= 0) {
                    CombatText.NewText(HitBox, new Color(255, 180, 100), Thrower.NoItemText.Value);
                    TextIdleTime = 180;
                }
                return;
            }

            //检查是否开启投掷
            if (!IsThrowing) {
                IsWorking = false;
                return;
            }

            IsWorking = true;

            //投掷计时
            if (++ThrowTimer >= ThrowInterval) {
                ThrowTimer = 0;
                PerformThrow();
            }
        }

        /// <summary>
        /// 执行投掷
        /// </summary>
        private void PerformThrow() {
            if (VaultUtils.isClient) {
                return;
            }

            Item throwItem = ConsumeOneItem();
            if (throwItem == null) {
                return;
            }

            float rate = MathHelper.Max(ThrowSpeed * 0.2f, 1f);
            //消耗能量
            MachineData.UEvalue -= ConsumeUE * rate;

            //计算投掷速度向量
            float radians = MathHelper.ToRadians(ThrowDirection);
            Vector2 velocity = new Vector2((float)Math.Cos(radians), (float)Math.Sin(radians)) * ThrowSpeed;

            //添加随机散布
            velocity = velocity.RotatedByRandom(MathHelper.ToRadians(ThrowAngle));

            //检查是否是弹药并且开启了弹药发射模式
            if (AmmoShootMode && IsAmmoItem(throwItem, out int projectileType)) {
                //发射弹幕而不是投掷物品
                ShootProjectile(projectileType, velocity);
            }
            else {
                //普通投掷物品
                ThrowItemEntity(throwItem, velocity);
            }

            //播放音效
            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.3f, Volume = 0.5f }, LaunchPosition);

            //粒子效果
            SpawnThrowParticles(velocity);

            SendData();
        }

        /// <summary>
        /// 检查物品是否是弹药并获取对应的弹幕类型
        /// </summary>
        private static bool IsAmmoItem(Item item, out int projectileType) {
            projectileType = 0;
            if (item == null || item.IsAir) {
                return false;
            }

            //检查物品是否有弹药属性
            if (item.ammo > 0 && item.shoot > 0) {
                projectileType = item.shoot;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 发射弹幕
        /// </summary>
        private void ShootProjectile(int projectileType, Vector2 velocity) {
            //使用投掷力度作为伤害
            int damage = (int)ThrowSpeed;

            int projIndex = Projectile.NewProjectile(
                this.FromObjectGetParent(),
                LaunchPosition,
                velocity,
                projectileType,
                damage,
                2f,
                Main.myPlayer
            );

            if (projIndex >= 0 && projIndex < Main.maxProjectiles) {
                Main.projectile[projIndex].friendly = true;
                Main.projectile[projIndex].hostile = false;

                if (VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, projIndex);
                }
            }
        }

        /// <summary>
        /// 投掷物品实体
        /// </summary>
        private void ThrowItemEntity(Item throwItem, Vector2 velocity) {
            int itemIndex = Item.NewItem(
                this.FromObjectGetParent(),
                LaunchPosition,
                throwItem.type,
                1,
                false,
                throwItem.prefix
            );

            if (itemIndex >= 0 && itemIndex < Main.maxItems) {
                Main.item[itemIndex].velocity = velocity;
                Main.item[itemIndex].noGrabDelay = 30;

                if (VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, itemIndex);
                }
            }
        }

        /// <summary>
        /// 生成投掷粒子效果
        /// </summary>
        private void SpawnThrowParticles(Vector2 velocity) {
            if (VaultUtils.isServer) {
                return;
            }

            for (int i = 0; i < 5; i++) {
                Vector2 dustVel = velocity * 0.3f + Main.rand.NextVector2Circular(2f, 2f);
                Dust dust = Dust.NewDustDirect(LaunchPosition, 8, 8, DustID.Smoke, dustVel.X, dustVel.Y, 100, default, 1f);
                dust.noGravity = true;
            }
        }

        #endregion

        #region 销毁处理

        public override void MachineKill() {
            if (VaultUtils.isClient) {
                return;
            }

            //掉落所有存储的物品
            foreach (var item in StoredItems) {
                if (item != null && !item.IsAir) {
                    VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, item);
                }
            }
            StoredItems.Clear();

            //掉落机器本身(带能量)
            Item throwerItem = new Item(ModContent.ItemType<Thrower>());
            throwerItem.CWR().UEValue = MachineData.UEvalue;
            int type = Item.NewItem(new EntitySource_WorldEvent(), HitBox, throwerItem);
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type);
            }
        }

        #endregion

        #region 绘制

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();

            //绘制投掷方向指示器
            if (IsWorking || ThrowerUI.Instance?.Station == this) {
                DrawDirectionIndicator(spriteBatch);
            }
        }

        /// <summary>
        /// 绘制方向指示器
        /// </summary>
        private void DrawDirectionIndicator(SpriteBatch spriteBatch) {
            if (Thrower.InputArrow == null) {
                return;
            }

            float radians = MathHelper.ToRadians(ThrowDirection);
            Vector2 drawPos = LaunchPosition - Main.screenPosition + radians.ToRotationVector2() * 24f;

            //使用InputArrow纹理绘制方向箭头
            var arrowTex = Thrower.InputArrow.Value;
            Color arrowColor = IsWorking ? new Color(255, 180, 100) * GlowIntensity : new Color(200, 150, 100) * 0.6f;

            spriteBatch.Draw(
                arrowTex,
                drawPos,
                null,
                arrowColor,
                radians,
                arrowTex.Size() / 2f,
                0.8f,
                SpriteEffects.None,
                0f
            );
        }

        #endregion
    }
}
