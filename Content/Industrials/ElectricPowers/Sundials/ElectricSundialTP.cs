using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.ElectricPowers.TeleportStations;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
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
    /// 请求-校验-广播流;本机只做前置校验给即时反馈。<br/>
    /// 视觉:机顶悬浮金环表盘,指针随真实时刻缓走;启动演出=表盘加速旋转+
    /// 向天金色光信号+金尘喷发;快进期间(读 <see cref="Main.IsFastForwardingTime"/>
    /// 真实世界旗标)指针自然狂转+持续微光,快进结束自然停
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
        /// <summary>装饰环累计转角(游标光点绕行);待机缓旋,演出/快进期加速</summary>
        internal float dialSpin;
        /// <summary>当前转速,向目标档平滑</summary>
        private float spinSpeed;

        /// <summary>供瓦片照明/辉光读取的综合亮度:充能底光+演出余辉+快进呼吸</summary>
        internal float EffectiveGlow {
            get {
                float glow = GlowIntensity;
                if (CeremonyFlash > 0) {
                    glow += CeremonyFlash / 90f;
                }
                if (Main.IsFastForwardingTime()) {
                    glow += 0.3f + 0.14f * MathF.Sin(Main.GlobalTimeWrappedHourly * 7f);
                }
                return glow;
            }
        }

        /// <summary>表盘中心(悬浮在机体上方)</summary>
        internal Vector2 DialCenter => new(CenterInWorld.X, PosInWorld.Y - 15f);

        /// <summary>
        /// 真实时刻的指针角:泰拉日 4:30 起昼 15:00 时长,19:30 起夜 9:00 时长,
        /// 折算 24h 表盘;正午指针朝上。快进时 Main.time 飞速,指针自然狂转
        /// </summary>
        internal static float HourHandAngle() {
            float hour = Main.dayTime
                ? 4.5f + (float)(Main.time / 3600.0)
                : 19.5f + (float)(Main.time / 3600.0);
            return (hour - 12f) / 24f * MathHelper.TwoPi;
        }

        public override void UpdateMachine() {
            if (CeremonyFlash > 0) {
                CeremonyFlash--;
            }
            //辉光随充能比例爬升,充满后满亮示意可用
            float ratio = MathHelper.Clamp(MachineData.UEvalue / MaxUEValue, 0f, 1f);
            GlowIntensity = MathHelper.Lerp(GlowIntensity, ratio, 0.05f);

            //装饰环转速:待机缓旋→演出冲高→快进期持续高速,回落平滑
            float targetSpin = 0.012f + ratio * 0.008f;
            if (Main.IsFastForwardingTime()) {
                targetSpin = 0.30f;
            }
            if (CeremonyFlash > 0) {
                targetSpin = MathF.Max(targetSpin, 0.06f + CeremonyFlash / 90f * 0.34f);
            }
            spinSpeed = MathHelper.Lerp(spinSpeed, targetSpin, 0.08f);
            dialSpin += spinSpeed;

            //快进期间持续微金尘:接真实世界旗标,不问触发者是谁
            if (!VaultUtils.isServer && InScreen && Main.IsFastForwardingTime() && Rand.NextBool(8)) {
                Defer(() => {
                    Vector2 pos = DialCenter + Main.rand.NextVector2Circular(14f, 6f);
                    Dust dust = Dust.NewDustPerfect(pos, DustID.GoldFlame,
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.6f, 1.4f)), 120, default, 0.9f);
                    dust.noGravity = true;
                });
            }
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

        /// <summary>各端演出:向天金色光信号 + 金尘喷发 + 表盘冲转,由广播回执调用</summary>
        internal void PlayCeremony() {
            CeremonyFlash = 90;
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.8f, Pitch = 0.2f }, CenterInWorld);
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = 0.5f }, CenterInWorld);

            //向天一道金色光信号:柱根锚在机顶,向上生长后收束(收口契约由 TechColumn 承担)
            SvcColumnFX.Push(new Vector2(CenterInWorld.X, PosInWorld.Y + 2f), 360f, 26f,
                SvcColumnFX.GoldBright, SvcColumnFX.GoldMain, SvcColumnFX.GoldDeep, 70, 0f);

            //演出粒子屏外不发,柱与声不受影响
            if (!VaultUtils.IsPointOnScreen(CenterInWorld - Main.screenPosition, 900)) {
                return;
            }

            //金尘喷发:锥形上抛 + 星芒点缀
            for (int i = 0; i < 26; i++) {
                Vector2 dustVel = (MathHelper.TwoPi * i / 26f).ToRotationVector2() * Main.rand.NextFloat(1.5f, 5f);
                Dust dust = Dust.NewDustDirect(HitBox.TopLeft(), HitBox.Width, HitBox.Height,
                    DustID.GoldFlame, dustVel.X, dustVel.Y - 2f, 100, default, 1.2f);
                dust.noGravity = true;
            }
            for (int i = 0; i < 6; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-1.4f, 1.4f), -Main.rand.NextFloat(2f, 5f));
                PRTLoader.NewParticle<PRT_Sparkle>(DialCenter + Main.rand.NextVector2Circular(10f, 4f), vel,
                    ElectricSundial.Tint, Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(new Color(255, 190, 80), Main.rand.Next(26, 40), 0.1f, 1.2f);
            }
        }

        #region 表盘绘制(实体批内)

        /// <summary>
        /// 悬浮金环表盘:薄锐环+12 刻度+真实时刻指针+绕行游光。
        /// 亮度吃充能档;缺电暗淡是全系统一暗化语言
        /// </summary>
        public override void Draw(SpriteBatch spriteBatch) {
            if (Main.dedServ) {
                return;
            }
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) {
                return;
            }

            Vector2 center = DialCenter - Main.screenPosition;
            float charge = MathHelper.Clamp(GlowIntensity, 0f, 1f);
            float bright = 0.30f + 0.70f * charge + (CeremonyFlash > 0 ? CeremonyFlash / 90f * 0.5f : 0f);
            Color gold = ElectricSundial.Tint;
            const float R = 14f;

            //薄锐外环:黑底环贴图在 AlphaBlend 批里走 A=0 加色
            Texture2D ring = CWRUtils.GetT2DAsset(CWRConstant.Masking + "DiffusionCircle4")?.Value;
            if (ring != null) {
                float scale = R / (ring.Width * 0.5f * 0.95f);
                spriteBatch.Draw(ring, center, null, gold with { A = 0 } * (bright * 0.85f), 0f,
                    ring.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            }

            //12 刻度:正午刻加长;实心黄铜,是仪器不是光效
            for (int i = 0; i < 12; i++) {
                float ang = MathHelper.TwoPi * i / 12f;
                Vector2 dir = (ang - MathHelper.PiOver2).ToRotationVector2();
                int len = i == 0 ? 5 : 3;
                Vector2 pos = center + dir * (R - 1f);
                Color tickCol = new Color(196, 158, 82) * (0.35f + 0.65f * bright);
                spriteBatch.Draw(px, new Rectangle((int)pos.X, (int)pos.Y, 2, len), null,
                    tickCol, ang, new Vector2(px.Width * 0.5f, px.Height * 0.5f), SpriteEffects.None, 0f);
            }

            //指针:随真实时刻缓走;快进时 Main.time 飞速,指针自然狂转
            float handAng = HourHandAngle();
            spriteBatch.Draw(px, new Rectangle((int)center.X, (int)center.Y, 2, 11), null,
                new Color(255, 232, 170) * (0.5f + 0.5f * bright), handAng,
                new Vector2(px.Width * 0.5f, px.Height), SpriteEffects.None, 0f);
            //轴心销钉
            spriteBatch.Draw(px, new Rectangle((int)center.X - 1, (int)center.Y - 1, 3, 3),
                new Color(120, 96, 48) * (0.4f + 0.6f * bright));

            //绕行游光:三粒金点沿环滑行,转速=dialSpin(演出/快进期猛转)
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                for (int i = 0; i < 3; i++) {
                    float ang = dialSpin + MathHelper.TwoPi * i / 3f;
                    Vector2 pos = center + (ang - MathHelper.PiOver2).ToRotationVector2() * R;
                    spriteBatch.Draw(glow, pos, null, gold with { A = 0 } * (bright * 0.8f), 0f,
                        glow.Size() * 0.5f, 0.10f + 0.04f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + i * 2f),
                        SpriteEffects.None, 0f);
                }
            }
        }

        #endregion

        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();
    }
}
