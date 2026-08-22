using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Suppressors
{
    /// <summary>
    /// 宁静力场发生器TP:开启且有电时持续耗电,压制半径内玩家的自然刷怪。<br/>
    /// 刷怪压制本身发生在 <see cref="PacifierSpawnSuppression"/>(服务器逻辑),
    /// 本TP只维护开关/电量状态并靠周期锚定同步
    /// </summary>
    internal class PacifierTowerTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<PacifierTowerTile>();
        public override int TargetItem => ModContent.ItemType<PacifierTower>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 800;

        /// <summary>压制半径(像素)</summary>
        internal const float SuppressRadius = 2000f;
        /// <summary>运转时每帧耗电</summary>
        internal const float ConsumePerTick = 0.4f;

        /// <summary>力场开关,右键/电线切换</summary>
        internal bool Enabled = true;
        /// <summary>本 tick 力场是否实际运转(开启且有电)</summary>
        internal bool SuppressActive { get; private set; }

        internal float GlowIntensity;
        private int textIdleTime;
        private int ambienceTimer;

        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(Enabled);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            Enabled = reader.ReadBoolean();
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            tag["_Enabled"] = Enabled;
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            if (tag.TryGet("_Enabled", out bool enabled)) {
                Enabled = enabled;
            }
        }

        /// <summary>右键/电线开关;此路径只在交互客户端执行,SendData 即传播机制</summary>
        public void ToggleField() {
            Enabled = !Enabled;
            SendData();
            ToggleEffect();
        }

        internal void ToggleEffect() {
            if (VaultUtils.isServer) {
                return;
            }

            for (int i = 0; i < 16; i++) {
                Dust dust = Dust.NewDustDirect(PosInWorld, Width, Height, DustID.JungleSpore);
                dust.noGravity = true;
                dust.velocity = Main.rand.NextVector2Circular(1.5f, 1.5f);
            }
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.6f }, CenterInWorld);
            CombatText.NewText(HitBox, PacifierTower.Tint,
                Enabled ? PacifierTower.FieldOnText.Value : PacifierTower.FieldOffText.Value);
        }

        public override void UpdateMachine() {
            if (textIdleTime > 0) {
                textIdleTime--;
            }

            bool running = false;
            if (Enabled) {
                if (MachineData.UEvalue >= ConsumePerTick) {
                    MachineData.UEvalue -= ConsumePerTick;
                    running = true;
                }
                else if (textIdleTime <= 0) {
                    //并行阶段CombatText生成延迟到主线程执行(串行阶段立即执行)
                    Defer(() => CombatText.NewText(HitBox, PacifierTower.Tint, PacifierTower.NoEnergyText.Value));
                    textIdleTime = 300;
                }
            }

            SuppressActive = running;
            GlowIntensity = running
                ? Math.Min(1f, GlowIntensity + 0.03f)
                : Math.Max(0f, GlowIntensity - 0.03f);

            //运转氛围:塔顶缓慢上飘的安神微光
            if (running && !VaultUtils.isServer && ++ambienceTimer >= 24) {
                ambienceTimer = 0;
                Vector2 spawnPos = PosInWorld + new Vector2(Rand.Next(Width), Rand.Next(12));
                Defer(() => {
                    Dust dust = Dust.NewDustPerfect(spawnPos, DustID.JungleSpore, new Vector2(0, -0.6f), 120, default, 0.8f);
                    dust.noGravity = true;
                    dust.fadeIn = 0.6f;
                });
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();
        }
    }
}
