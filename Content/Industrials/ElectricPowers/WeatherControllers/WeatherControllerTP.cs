using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.WeatherControllers
{
    /// <summary>
    /// 天气控制机TP:右键消耗 500 UE 求雨,雨天使用则止雨。
    /// 天气是服务器权威世界状态,使用走 <see cref="IndustrialServiceNet"/> 的
    /// 请求-校验-广播流;只干预降雨,风不受控(原版没有干净的写入口)。<br/>
    /// 视觉:求雨=顶部云种升空炸云+雾向机身聚拢(聚云);止雨=雾自机身推散+
    /// 暖白扩散环(散云)——相反的事件给相反的视觉语言。
    /// 雨天待机时机顶滴水,接 <see cref="Main.raining"/> 真实世界状态
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
        /// <summary>上次演出是求雨还是止雨,决定余辉窗口的粒子与灯色语言</summary>
        internal bool LastWantRain;
        /// <summary>止雨暖白扩散环计时(帧,减到 0)</summary>
        internal int clearRing;

        public override void UpdateMachine() {
            if (CeremonyFlash > 0) {
                CeremonyFlash--;
            }
            if (clearRing > 0) {
                clearRing--;
            }
            bool charged = MachineData.UEvalue >= ToggleCost;
            GlowIntensity = charged
                ? MathHelper.Min(1f, GlowIntensity + 0.03f)
                : MathHelper.Max(0f, GlowIntensity - 0.03f);
            if (charged) {
                //待机动画,复用热能电池六帧表
                VaultUtils.ClockFrame(ref frame, 5, 5);
            }

            if (VaultUtils.isServer || !InScreen) {
                return;
            }

            //求雨余辉窗口:雾丝持续向机顶聚拢,聚云的"承"
            if (CeremonyFlash > 20 && LastWantRain && CeremonyFlash % 7 == 0) {
                Defer(() => {
                    Vector2 target = new(CenterInWorld.X, PosInWorld.Y - 26f);
                    Vector2 pos = target + Main.rand.NextVector2CircularEdge(52f, 30f);
                    PRTLoader.NewParticle<PRT_SvcCloud>(pos, (target - pos) * 0.03f,
                        new Color(172, 196, 232), Main.rand.NextFloat(0.16f, 0.28f))?.Configure(52);
                });
            }

            //雨天待机:机顶挂雨滴落,机器淋在真实天气里
            if (Main.raining && Rand.NextBool(26)) {
                Defer(() => {
                    Vector2 pos = PosInWorld + new Vector2(Main.rand.NextFloat(4f, Width - 4f), Main.rand.NextFloat(2f));
                    Dust dust = Dust.NewDustPerfect(pos, DustID.Rain, new Vector2(0f, Main.rand.NextFloat(1.5f, 3f)));
                    dust.noGravity = false;
                    dust.scale = Main.rand.NextFloat(0.8f, 1.1f);
                });
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

        /// <summary>
        /// 各端演出,由广播回执调用。求雨与止雨是相反的视觉语言:
        /// 聚云=云种升空炸开+雾向心收拢+冷蓝;散云=雾径向推散+暖白环+光感
        /// </summary>
        internal void PlayCeremony(bool wantRain) {
            CeremonyFlash = 90;
            LastWantRain = wantRain;
            if (VaultUtils.isServer) {
                return;
            }

            Vector2 top = new(CenterInWorld.X, PosInWorld.Y + 2f);
            //演出粒子屏外不发;止雨环走 clearRing 计时,TP 入屏才画
            bool onScreen = VaultUtils.IsPointOnScreen(CenterInWorld - Main.screenPosition, 900);
            if (wantRain) {
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.6f, Pitch = -0.3f }, CenterInWorld);
                SoundEngine.PlaySound(SoundID.Item66 with { Volume = 0.4f, Pitch = -0.2f }, CenterInWorld);
                if (!onScreen) {
                    return;
                }

                //云种三连发:错帧升空,顶点炸开成云——"起"
                for (int i = 0; i < 3; i++) {
                    Vector2 vel = new(Main.rand.NextFloat(-0.7f, 0.7f), -Main.rand.NextFloat(5.6f, 6.8f));
                    PRTLoader.NewParticle<PRT_SvcCloudSeed>(top + new Vector2(Main.rand.NextFloat(-8f, 8f), 0f),
                        vel, new Color(235, 245, 255), 0.5f)
                        ?.Configure(Main.rand.Next(24, 30), i * 9, new Color(176, 198, 230));
                }
                //机身雾环绕:向心聚拢起手
                for (int i = 0; i < 8; i++) {
                    Vector2 target = new(CenterInWorld.X, PosInWorld.Y - 20f);
                    Vector2 pos = target + Main.rand.NextVector2CircularEdge(64f, 36f);
                    PRTLoader.NewParticle<PRT_SvcCloud>(pos, (target - pos) * 0.045f,
                        new Color(168, 192, 228), Main.rand.NextFloat(0.2f, 0.34f))?.Configure(Main.rand.Next(50, 76));
                }
            }
            else {
                SoundEngine.PlaySound(SoundID.Item54 with { Volume = 0.7f }, CenterInWorld);
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.35f, Pitch = 0.55f }, CenterInWorld);
                clearRing = 30;
                if (!onScreen) {
                    return;
                }

                //散云:雾自机身径向推开,越飘越薄——与聚云相反的速度场
                for (int i = 0; i < 12; i++) {
                    float ang = MathHelper.TwoPi * i / 12f + Main.rand.NextFloat(0.4f);
                    Vector2 dir = ang.ToRotationVector2();
                    Vector2 pos = CenterInWorld + dir * Main.rand.NextFloat(8f, 20f);
                    Vector2 vel = dir * Main.rand.NextFloat(1.6f, 3.2f) - new Vector2(0f, 0.5f);
                    PRTLoader.NewParticle<PRT_SvcCloud>(pos, vel, new Color(228, 226, 214),
                        Main.rand.NextFloat(0.22f, 0.38f))?.Configure(Main.rand.Next(44, 66), 0.0034f);
                }
                //拨云见日的光感:两粒暖白光尘上飘
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_Light>(top + Main.rand.NextVector2Circular(12f, 4f),
                        new Vector2(0f, -Main.rand.NextFloat(0.6f, 1.2f)), new Color(255, 240, 200),
                        Main.rand.NextFloat(0.12f, 0.2f))?.Configure(30, 0.8f);
                }
            }
        }

        /// <summary>止雨暖白扩散环:云被推开的波前,画在实体批内</summary>
        public override void Draw(SpriteBatch spriteBatch) {
            if (Main.dedServ || clearRing <= 0) {
                return;
            }
            float t = 1f - clearRing / 30f;
            float r = MathHelper.Lerp(14f, 72f, 1f - (1f - t) * (1f - t));
            ShockRingDraw.Draw(spriteBatch, CenterInWorld, r, 9f,
                new Color(255, 246, 224), new Color(232, 220, 190), new Color(96, 84, 60),
                (1f - t) * 0.7f, squish: 0.8f, timeSeed: Position.X * 0.19f);
        }

        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();
    }
}
