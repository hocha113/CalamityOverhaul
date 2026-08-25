using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Sundials
{
    /// <summary>
    /// 电动日晷TP:储满 1000 UE 右键即可把时间快进到黎明,不碰原版七天冷却。
    /// 时间是服务器权威世界状态,使用走 <see cref="IndustrialServiceNet"/> 的
    /// 请求-校验-广播流;本机只做前置校验给即时反馈
    /// </summary>
    internal class ElectricSundialTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<ElectricSundialTile>();
        public override int TargetItem => ModContent.ItemType<ElectricSundial>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => SkipCost;

        /// <summary>单次快进的电费</summary>
        internal const float SkipCost = 1000f;

        internal float GlowIntensity;
        /// <summary>使用演出的金光余辉(帧)</summary>
        internal int CeremonyFlash;

        public override void UpdateMachine() {
            if (CeremonyFlash > 0) {
                CeremonyFlash--;
            }
            //辉光随充能比例爬升,充满后满亮示意可用
            float ratio = MathHelper.Clamp(MachineData.UEvalue / MaxUEValue, 0f, 1f);
            GlowIntensity = MathHelper.Lerp(GlowIntensity, ratio, 0.05f);
        }

        /// <summary>交互客户端右键入口:本机前置校验后向服务器发请求</summary>
        internal void RequestSkip() {
            if (Main.IsFastForwardingTime()) {
                CombatText.NewText(HitBox, Color.DimGray, ElectricSundial.BusyText.Value);
                SoundEngine.PlaySound(SoundID.MenuClose);
                return;
            }
            if (MachineData.UEvalue < SkipCost) {
                CombatText.NewText(HitBox, Color.DimGray, ElectricSundial.NoEnergyText.Value);
                SoundEngine.PlaySound(SoundID.MenuClose);
                return;
            }
            IndustrialServiceNet.RequestTimeSkip(this);
        }

        /// <summary>各端演出:金尘迸发 + 余辉,由广播回执调用</summary>
        internal void PlayCeremony() {
            CeremonyFlash = 90;
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.8f, Pitch = 0.2f }, CenterInWorld);
            for (int i = 0; i < 26; i++) {
                Vector2 dustVel = (MathHelper.TwoPi * i / 26f).ToRotationVector2() * Main.rand.NextFloat(1.5f, 5f);
                Dust dust = Dust.NewDustDirect(HitBox.TopLeft(), HitBox.Width, HitBox.Height,
                    DustID.GoldFlame, dustVel.X, dustVel.Y - 2f, 100, default, 1.2f);
                dust.noGravity = true;
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();
    }
}
