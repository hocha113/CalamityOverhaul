using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.WeatherControllers
{
    /// <summary>
    /// 天气控制机TP:右键消耗 500 UE 求雨,雨天使用则止雨。
    /// 天气是服务器权威世界状态,使用走 <see cref="IndustrialServiceNet"/> 的
    /// 请求-校验-广播流;只干预降雨,风不受控(原版没有干净的写入口)
    /// </summary>
    internal class WeatherControllerTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<WeatherControllerTile>();
        public override int TargetItem => ModContent.ItemType<WeatherController>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 1000;

        /// <summary>单次求雨/止雨的电费</summary>
        internal const float ToggleCost = 500f;

        internal int frame;
        internal float GlowIntensity;
        /// <summary>使用演出的余辉(帧)</summary>
        internal int CeremonyFlash;

        public override void UpdateMachine() {
            if (CeremonyFlash > 0) {
                CeremonyFlash--;
            }
            bool charged = MachineData.UEvalue >= ToggleCost;
            GlowIntensity = charged
                ? MathHelper.Min(1f, GlowIntensity + 0.03f)
                : MathHelper.Max(0f, GlowIntensity - 0.03f);
            if (charged) {
                //待机动画,复用热能电池六帧表
                VaultUtils.ClockFrame(ref frame, 5, 5);
            }
        }

        /// <summary>交互客户端右键入口:按当前天气决定求雨还是止雨,本机前置校验后发请求</summary>
        internal void RequestToggle() {
            if (MachineData.UEvalue < ToggleCost) {
                CombatText.NewText(HitBox, Color.DimGray, WeatherController.NoEnergyText.Value);
                SoundEngine.PlaySound(SoundID.MenuClose);
                return;
            }
            IndustrialServiceNet.RequestWeatherSet(this, !Main.raining);
        }

        /// <summary>各端演出:雨云尘 + 余辉,由广播回执调用</summary>
        internal void PlayCeremony(bool wantRain) {
            CeremonyFlash = 90;
            if (VaultUtils.isServer) {
                return;
            }
            if (wantRain) {
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.6f, Pitch = -0.3f }, CenterInWorld);
            }
            else {
                SoundEngine.PlaySound(SoundID.Item54 with { Volume = 0.7f }, CenterInWorld);
            }
            for (int i = 0; i < 24; i++) {
                Vector2 dustVel = new(Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-4f, -1f));
                Dust dust = Dust.NewDustDirect(HitBox.TopLeft(), HitBox.Width, 8,
                    wantRain ? DustID.Rain : DustID.Cloud, dustVel.X, dustVel.Y, 100, default, 1.1f);
                dust.noGravity = true;
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();
    }
}
