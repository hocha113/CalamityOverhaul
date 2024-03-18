using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using static Humanizer.In;

namespace CalamityOverhaul.Content
{
    public class CWRPlayer : ModPlayer
    {
        /// <summary>
        /// 是否初始化加载，用于角色文件的第一次创建
        /// </summary>
        public bool InitialCreation;
        /// <summary>
        /// 圣物的装备等级，这个字段决定了玩家会拥有什么样的弹幕效果
        /// </summary>
        public int TheRelicLuxor = 0;
        /// <summary>
        /// 是否装备制动器
        /// </summary>
        public bool LoadMuzzleBrake;
        /// <summary>
        /// 装备的制动器等级
        /// </summary>
        public int LoadMuzzleBrakeLevel;
        /// <summary>
        /// 应力缩放
        /// </summary>
        public float PressureIncrease;
        /// <summary>
        /// 摄像头位置额外矫正值
        /// </summary>
        public Vector2 OffsetScreenPos;
        //未使用的，这个属性属于一个未完成的UI
        public int CompressorPanelID = -1;
        /// <summary>
        /// 是否开启超级合成台
        /// </summary>
        public bool SupertableUIStartBool;
        /// <summary>
        /// 玩家是否坐在大排档塑料椅子之上
        /// </summary>
        public bool InFoodStallChair;
        /// <summary>
        /// 玩家是否装备休谟稳定器
        /// </summary>
        public bool EndlessStabilizerBool;
        /// <summary>
        /// 玩家是否手持鬼妖
        /// </summary>
        public bool HeldMurasamaBool;
        /// <summary>
        /// 玩家是否正在进行终结技
        /// </summary>
        public bool EndSkillEffectStartBool;
        /// <summary>
        /// 升龙技冷却时间
        /// </summary>
        public int RisingDragonCoolDownTime;
        public int SafeHeldProjIndex;
        /// <summary>
        /// 是否受伤
        /// </summary>
        public bool OnHit;
        public bool HeldRangedBool;
        public bool HeldFeederGunBool;
        public bool HeldGunBool;
        public bool HeldBowBool;
        #region 网络同步
        public bool DompBool;
        public bool RecoilAccelerationAddBool;
        public Vector2 RecoilAccelerationValue;
        #endregion

        #region Buff
        public bool TyrantsFuryBuffBool;
        public bool FlintSummonBool;
        #endregion

        public override void Initialize() {
            TheRelicLuxor = 0;
            PressureIncrease = 1;
            SafeHeldProjIndex = -1;
            OnHit = false;           
            LoadMuzzleBrake = false;
            InitialCreation = true;
            HeldRangedBool = false;
            HeldFeederGunBool = false;
            HeldGunBool = false;
            HeldBowBool = false;
        }

        public override void ResetEffects() {
            OffsetScreenPos = Vector2.Zero;
            TheRelicLuxor = 0;
            LoadMuzzleBrakeLevel = 0;
            PressureIncrease = 1;
            SafeHeldProjIndex = -1;
            OnHit = false;
            InFoodStallChair = false;
            EndlessStabilizerBool = false;
            HeldMurasamaBool = false;
            EndSkillEffectStartBool = false;
            LoadMuzzleBrake = false;
            TyrantsFuryBuffBool = false;
            FlintSummonBool = false;
            HeldRangedBool = false;
            HeldFeederGunBool = false;
            HeldGunBool = false;
            HeldBowBool = false;
        }

        public override void SaveData(TagCompound tag) {
            tag.Add("_InitialCreation", InitialCreation);
        }

        public override void LoadData(TagCompound tag) {
            InitialCreation = tag.GetBool("_InitialCreation");
        }

        public override void OnEnterWorld() {
            if (CWRServerConfig.Instance.ForceReplaceResetContent) {
                CWRUtils.Text(CWRMod.RItemIndsDict.Count + CWRLocText.GetTextValue("OnEnterWorld_TextContent"), Color.GreenYellow);
            }
            if (InitialCreation) {
                for (int i = 0; i < Player.inventory.Length; i++) {
                    if (Player.inventory[i].type == ItemID.CopperAxe) {
                        Player.inventory[i] = new Item(ModContent.ItemType<PebbleAxe>());
                    }
                    if (Player.inventory[i].type == ItemID.CopperPickaxe) {
                        Player.inventory[i] = new Item(ModContent.ItemType<PebblePick>());
                    }
                    if (Player.inventory[i].type is ItemID.CopperBroadsword
                        or ItemID.CopperShortsword) {
                        Player.inventory[i] = new Item(ModContent.ItemType<PebbleSpear>());
                    }
                }
                InitialCreation = false;
            }
            RecipeErrorFullUI.Instance.eyEBool = true;
        }

        public override void OnHurt(Player.HurtInfo info) {
            OnHit = true;
        }

        public override void PostUpdate() {
            if (Player.sitting.TryGetSittingBlock(Player, out Tile t)) {
                if (t.TileType == CWRIDs.FoodStallChairTile) {
                    InFoodStallChair = true;
                    Main.raining = true;
                    Main.maxRaining = 0.99f;
                    Main.cloudAlpha = 0.99f;
                    Main.windSpeedTarget = 0.8f;
                    float sengs = Math.Abs(MathF.Sin(Main.GameUpdateCount * 0.05f));
                    Lighting.AddLight(Player.Center, new Color(Main.DiscoB, Main.DiscoG, 220 + (sengs * 30)).ToVector3() * sengs * 113);
                    PunchCameraModifier modifier2 = new(Player.Center, new Vector2(0, Main.rand.NextFloat(-2, 2)), 2f, 3f, 2, 1000f, FullName);
                    Main.instance.CameraModifiers.Add(modifier2);
                }
            }
            if (DompBool) {
                $"{Player.name}成功进行网络同步".Domp();
                DompBool = false;
            }
            if (RecoilAccelerationAddBool) {
                Player.velocity += RecoilAccelerationValue;
                RecoilAccelerationAddBool = false;
            }
        }

        internal void HandleRecoilAcceleration(BinaryReader reader) {
            RecoilAccelerationAddBool = reader.ReadBoolean();
            RecoilAccelerationValue.X = reader.ReadSingle();
            RecoilAccelerationValue.Y = reader.ReadSingle();
            if (Main.netMode == NetmodeID.Server) {
                SyncRecoilAcceleration(true);
            }
        }

        public void SyncRecoilAcceleration(bool server) {
            ModPacket packet = Mod.GetPacket(256);
            packet.Write((byte)CWRMessageType.RecoilAcceleration);
            packet.Write(Player.whoAmI);
            packet.Write(RecoilAccelerationAddBool);
            packet.Write(RecoilAccelerationValue.X);
            packet.Write(RecoilAccelerationValue.Y);
            Player.SendPacket(packet, server);
        }

        internal void HandleDomp(BinaryReader reader) {
            DompBool = reader.ReadBoolean();
            if (Main.netMode == NetmodeID.Server) {
                SyncDomp(true);
            }
        }

        public void SyncDomp(bool server) {
            ModPacket packet = Mod.GetPacket(256);
            packet.Write((byte)CWRMessageType.DompBool);
            packet.Write(Player.whoAmI);
            packet.Write(DompBool);
            Player.SendPacket(packet, server);
        }

        public override void ModifyScreenPosition() {
            Main.screenPosition += OffsetScreenPos;
        }

        public override IEnumerable<Item> AddStartingItems(bool mediumCoreDeath) {
            yield return new Item(ModContent.ItemType<PebbleSpear>());
            yield return new Item(ModContent.ItemType<PebblePick>());
            yield return new Item(ModContent.ItemType<PebbleAxe>());
        }

        public override void ModifyStartingInventory(IReadOnlyDictionary<string, List<Item>> itemsByMod, bool mediumCoreDeath) {
            if (!mediumCoreDeath) {
                _ = itemsByMod["Terraria"].RemoveAll(item => item.type == ItemID.CopperAxe);
                _ = itemsByMod["Terraria"].RemoveAll(item => item.type == ItemID.CopperShortsword);
                _ = itemsByMod["Terraria"].RemoveAll(item => item.type == ItemID.CopperPickaxe);
            }
        }

        /// <summary>
        /// 尝试获取玩家当前持有的类型为 T 的投射物实例。
        /// </summary>
        /// <typeparam name="T">要获取的投射物实例的类型。</typeparam>
        /// <param name="result">方法返回时，如果找到且成功获取到类型为 T 的投射物实例，则包含该实例；否则为 null。</param>
        /// <returns>如果找到并成功获取到类型为 T 的投射物实例，则为 true；否则为 false。</returns>
        internal bool TryGetHeldProjInds<T>(out T result) where T : class {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                // 检查投射物是否处于激活状态，是否属于玩家所有，并且是否隐藏
                if (!p.active || p.owner != Player.whoAmI || !p.hide) {
                    continue; // 如果当前投射物不符合条件，则跳过并检查下一个投射物
                }
                if (p.ModProjectile as T != null) {
                    Player.heldProj = p.whoAmI;
                    T instance = p.ModProjectile as T;
                    if (instance != null) {
                        result = instance;
                        return true;
                    }
                }
            }
            result = null;
            return false;
        }
        /// <summary>
        /// 获取玩家所手持的BaseHeldRanged实例
        /// </summary>
        /// <param name="baseGun"></param>
        /// <returns>如果玩家没有手持BaseGun或者发生了其他非法情况，返回<see langword="false"/></returns>
        internal bool TryGetInds_BaseHeldRanged(out BaseHeldRanged baseranged) {
            return TryGetHeldProjInds(out baseranged);
        }
        /// <summary>
        /// 获取玩家所手持的<see cref="BaseGun"/>实例
        /// </summary>
        /// <param name="baseGun"></param>
        /// <returns>如果玩家没有手持<see cref="BaseGun"/>或者发生了其他非法情况，返回<see langword="false"/></returns>
        internal bool TryGetInds_BaseGun(out BaseGun baseGun) {
            return TryGetHeldProjInds(out baseGun);
        }
        /// <summary>
        /// 获取玩家所手持的<see cref="BaseFeederGun"/>实例
        /// </summary>
        /// <param name="baseGun"></param>
        /// <returns>如果玩家没有手持<see cref="BaseFeederGun"/>或者发生了其他非法情况，返回<see langword="false"/></returns>
        internal bool TryGetInds_BaseFeederGun(out BaseFeederGun baseFeederGun) {
            return TryGetHeldProjInds(out baseFeederGun);
        }
    }
}
