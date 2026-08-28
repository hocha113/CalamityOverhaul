using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.TeleportStations
{
    /// <summary>
    /// 传送站TP:世界上的同类站点互为目的地(枚举 TP_InWorld 即得站表,零额外存储)。
    /// 传送为交互客户端权威:本机校验扣电并推送,再走原版 TeleportEntity 广播,
    /// 位置归属本就是拥有端,无越权面
    /// </summary>
    internal class TeleportStationTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<TeleportStationTile>();
        public override int TargetItem => ModContent.ItemType<TeleportStation>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 800;

        #region 常量

        //费用:10 + 0.02 × 距离(格),封顶 200
        internal const float BaseCost = 10f;
        internal const float CostPerTile = 0.02f;
        internal const float CostCap = 200f;
        //目的站待机门槛,断电站不能当免费落点
        internal const float ArrivalReserveUE = 10f;
        //站名长度上限(字符)
        internal const int MaxNameLength = 20;

        #endregion

        #region 字段与属性

        /// <summary>玩家起的站名,空串表示未命名;UI 编辑为客户端权威推送</summary>
        internal string StationName = "";

        internal float GlowIntensity;

        /// <summary>余辉总帧数,传送双端共用</summary>
        internal const int AfterglowFrames = 45;

        /// <summary>门户功率档(纯客户端表现):1=可出发 0.42=仅可接收 0=熄灭,平滑爬档</summary>
        internal float PortalPower;
        /// <summary>传送余辉计时,门户环过亮渐落</summary>
        internal int Afterglow;
        /// <summary>出发涟漪计时(帧,减到 0)</summary>
        internal int departRing;
        /// <summary>到达涟漪计时(帧,减到 0)</summary>
        internal int arriveRing;

        /// <summary>列表显示名:未命名时回退成带坐标的占位名</summary>
        internal string ShowName => string.IsNullOrWhiteSpace(StationName)
            ? TeleportStation.UnnamedText.Format(Position.X, Position.Y)
            : StationName;

        #endregion

        #region 数据同步与存档

        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(StationName ?? "");
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            StationName = reader.ReadString();
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            tag["_StationName"] = StationName ?? "";
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            if (tag.TryGet("_StationName", out string name)) {
                StationName = name;
            }
        }

        #endregion

        #region 站表与费用

        /// <summary>收集世界内全部活跃传送站;枚举 TP_InWorld,拆掉的站自然消失</summary>
        internal static void CollectStations(List<TeleportStationTP> result) {
            result.Clear();
            foreach (var tp in InnoVault.TileProcessors.TileProcessorLoader.TP_InWorld) {
                if (tp is TeleportStationTP station && station.Active) {
                    result.Add(station);
                }
            }
        }

        /// <summary>按距离计费:10 + 0.02 UE/格,封顶 200</summary>
        internal static float TeleportCost(TeleportStationTP from, TeleportStationTP to) {
            float tiles = from.CenterInWorld.Distance(to.CenterInWorld) / 16f;
            return Math.Min(BaseCost + tiles * CostPerTile, CostCap);
        }

        /// <summary>落点:拱门内金托盘中心,玩家脚底贴机身底线</summary>
        internal Vector2 ArrivalPositionFor(Player player)
            => new(CenterInWorld.X - player.width / 2f, PosInWorld.Y + Height - player.height);

        #endregion

        #region 传送执行(交互客户端)

        /// <summary>
        /// 把本地玩家送往目标站。本机校验起点电量与目的站待机电,
        /// 扣电推送后本地 Teleport,再发原版 TeleportEntity 包让全端回放
        /// </summary>
        internal bool TryTeleportLocalPlayer(TeleportStationTP target) {
            if (target == null || target == this || !target.Active) {
                return false;
            }

            Player player = Main.LocalPlayer;
            float cost = TeleportCost(this, target);

            if (MachineData.UEvalue < cost) {
                CombatText.NewText(HitBox, Color.DimGray, TeleportStation.NoEnergyText.Value);
                SoundEngine.PlaySound(SoundID.MenuClose);
                return false;
            }
            if (target.MachineData.UEvalue < ArrivalReserveUE) {
                CombatText.NewText(HitBox, Color.DimGray, TeleportStation.TargetNoEnergyText.Value);
                SoundEngine.PlaySound(SoundID.MenuClose);
                return false;
            }

            //起点站扣电:客户端权威 TP 编辑 + 推送,是框架接受的取舍
            MachineData.UEvalue -= cost;
            SendData();

            Vector2 arrive = target.ArrivalPositionFor(player);
            player.Teleport(arrive, TeleportationStyleID.TeleportationPylon);
            if (VaultUtils.isClient) {
                //number=0 的玩家传送包:服务器回放后转发旁观端,特效各端自播
                NetMessage.SendData(MessageID.TeleportEntity, -1, -1, null, 0,
                    player.whoAmI, arrive.X, arrive.Y, TeleportationStyleID.TeleportationPylon);
            }
            return true;
        }

        #endregion

        #region 更新与交互

        public override void UpdateMachine() {
            bool powered = MachineData.UEvalue >= BaseCost;
            GlowIntensity = powered
                ? Math.Min(1f, GlowIntensity + 0.03f)
                : Math.Max(0f, GlowIntensity - 0.03f);
            //门户功率三档:可出发满亮/仅可接收半暗/断电熄灭,状态一眼可读
            float tier = powered ? 1f : (MachineData.UEvalue >= ArrivalReserveUE ? 0.42f : 0f);
            PortalPower = MathHelper.Lerp(PortalPower, tier, 0.06f);

            if (Afterglow > 0) {
                Afterglow--;
            }
            if (departRing > 0) {
                departRing--;
            }
            if (arriveRing > 0) {
                arriveRing--;
            }

            //待机微光尘:拱门内偶发一粒上飘光屑,通电才有
            if (!VaultUtils.isServer && InScreen && PortalPower > 0.55f && Rand.NextBool(46)) {
                Defer(() => {
                    Vector2 pos = new(CenterInWorld.X + Main.rand.NextFloat(-22f, 22f), PosInWorld.Y + Height - 26f);
                    PRTLoader.NewParticle<PRT_Light>(pos, new Vector2(0f, -Main.rand.NextFloat(0.4f, 0.9f)),
                        TeleportStation.Tint, Main.rand.NextFloat(0.06f, 0.12f))?.Configure(26, 0.6f);
                });
            }
        }

        public void RightClickByTile() {
            var ui = UIHandleLoader.GetUIHandleOfType<TeleportStationUI>();
            ui?.Interactive(this);
        }

        #endregion

        #region 传送演出(纯客户端,由 TeleportWatcher 凭真实传送事件触发)

        /// <summary>本机屏内粗判(余量 900px),演出粒子屏外不发;柱/涟漪计时照设,走近仍能看到尾段</summary>
        private bool NearLocalScreen()
            => VaultUtils.IsPointOnScreen(CenterInWorld - Main.screenPosition, 900);

        /// <summary>出发拍:光柱收束吞没 + 台面涟漪 + 向心吸入光尘;双端余辉</summary>
        internal void PlayDepartFX(Player player) {
            Afterglow = AfterglowFrames;
            departRing = 30;

            Vector2 platform = new(CenterInWorld.X, PosInWorld.Y + Height - 10f);
            SvcColumnFX.Push(platform, 132f, 44f,
                SvcColumnFX.CyanBright, SvcColumnFX.CyanMain, SvcColumnFX.CyanDeep, 34, 0f);
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.4f, Pitch = -0.35f }, platform);
            if (!NearLocalScreen()) {
                return;
            }

            //向心吸入:光屑从柱侧被卷进吞没点
            for (int i = 0; i < 8; i++) {
                Vector2 pos = platform + new Vector2(Main.rand.NextFloat(-38f, 38f), -Main.rand.NextFloat(6f, 88f));
                Vector2 pull = (new Vector2(platform.X, pos.Y - 14f) - pos) * 0.09f;
                PRTLoader.NewParticle<PRT_Light>(pos, pull, TeleportStation.Tint,
                    Main.rand.NextFloat(0.08f, 0.16f))?.Configure(20, 0.8f);
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_GraniteVolt>(platform + Main.rand.NextVector2Circular(20f, 6f),
                    Main.rand.NextVector2Circular(1.5f, 1f), TeleportStation.Tint,
                    Main.rand.NextFloat(0.2f, 0.34f))?.Configure(Main.rand.Next(3, 6));
            }
        }

        /// <summary>到达拍:光柱自上吐出 + 双环涟漪错拍 + 落地尘光;双端余辉</summary>
        internal void PlayArriveFX(Player player) {
            Afterglow = AfterglowFrames;
            arriveRing = 38;

            Vector2 platform = new(CenterInWorld.X, PosInWorld.Y + Height - 10f);
            SvcColumnFX.Push(platform, 150f, 40f,
                SvcColumnFX.CyanBright, SvcColumnFX.CyanMain, SvcColumnFX.CyanDeep, 30, 1f);
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.42f, Pitch = 0.25f }, platform);
            if (!NearLocalScreen()) {
                return;
            }

            //落地尘光:自台面上飘
            for (int i = 0; i < 10; i++) {
                Vector2 pos = platform + new Vector2(Main.rand.NextFloat(-26f, 26f), -Main.rand.NextFloat(0f, 8f));
                Vector2 vel = new(Main.rand.NextFloat(-1.1f, 1.1f), -Main.rand.NextFloat(0.8f, 2.6f));
                PRTLoader.NewParticle<PRT_Light>(pos, vel, TeleportStation.Tint,
                    Main.rand.NextFloat(0.09f, 0.18f))?.Configure(24, 0.85f);
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_GraniteVolt>(platform + Main.rand.NextVector2Circular(22f, 5f),
                    Main.rand.NextVector2Circular(2f, 1.2f), TeleportStation.Tint,
                    Main.rand.NextFloat(0.22f, 0.38f))?.Configure(Main.rand.Next(3, 6));
            }
        }

        #endregion

        #region 绘制

        /// <summary>台面涟漪:出发单环收拢语气,到达双环错拍扩散;贴地椭圆压扁</summary>
        public override void Draw(SpriteBatch spriteBatch) {
            if (Main.dedServ) {
                return;
            }
            Vector2 platform = new(CenterInWorld.X, PosInWorld.Y + Height - 10f);
            Color bright = new(210, 255, 246);
            Color main = TeleportStation.Tint;
            Color deep = new(16, 88, 80);

            if (departRing > 0) {
                float t = 1f - departRing / 30f;
                float r = MathHelper.Lerp(10f, 48f, 1f - (1f - t) * (1f - t));
                ShockRingDraw.Draw(spriteBatch, platform, r, 7f, bright, main, deep,
                    (1f - t) * 0.85f, squish: 0.35f, timeSeed: Position.X * 0.13f);
            }
            if (arriveRing > 0) {
                float t = 1f - arriveRing / 38f;
                float r1 = MathHelper.Lerp(12f, 62f, 1f - (1f - t) * (1f - t));
                ShockRingDraw.Draw(spriteBatch, platform, r1, 8f, bright, main, deep,
                    (1f - t) * 0.9f, squish: 0.35f, timeSeed: Position.X * 0.17f);
                //第二道错拍 8 帧
                float t2 = MathHelper.Clamp((38f - arriveRing - 8f) / 30f, 0f, 1f);
                if (t2 > 0f && t2 < 1f) {
                    float r2 = MathHelper.Lerp(8f, 44f, 1f - (1f - t2) * (1f - t2));
                    ShockRingDraw.Draw(spriteBatch, platform, r2, 6f, bright, main, deep,
                        (1f - t2) * 0.6f, squish: 0.35f, timeSeed: Position.X * 0.29f);
                }
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();

        #endregion
    }
}
