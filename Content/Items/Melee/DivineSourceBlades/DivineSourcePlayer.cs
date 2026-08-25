using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.DivineSourceBlades
{
    /// <summary>金源灭却刃充能与连击状态，挂玩家而非物品实例(物品字段会被 tML 重克隆抹掉)</summary>
    internal class DivineSourcePlayer : ModPlayer
    {
        /// <summary>充能量 0~1，武器伤害到敌人缓慢积攒</summary>
        public float Charge;
        /// <summary>连击拍号 0~3</summary>
        public int ComboStage;
        /// <summary>上次挥砍帧计数，停手超时回首拍</summary>
        public uint LastSwingTick;

        private bool wasFull;
        private bool wasEmpowered;

        /// <summary>充能持续帧数，7秒</summary>
        public const int EmpowerDuration = 420;
        /// <summary>充能期武器伤害倍率</summary>
        public const float EmpowerDamageMul = 1.5f;

        public bool Empowered => Player.HasBuff<DivineSourceChargeBuff>();

        public int EmpowerTicksLeft {
            get {
                int idx = Player.FindBuffIndex(ModContent.BuffType<DivineSourceChargeBuff>());
                return idx >= 0 ? Player.buffTime[idx] : 0;
            }
        }

        public bool HoldingBlade => Player.HeldItem != null
            && Player.HeldItem.type == ModContent.ItemType<DivineSourceBlade>();

        public void AddCharge(float amount) {
            if (Empowered) {
                return;
            }
            Charge = Math.Clamp(Charge + amount, 0f, 1f);
        }

        /// <summary>右键激活，消耗整条充能换 7 秒强化</summary>
        public bool TryConsumeFullCharge() {
            if (Empowered || Charge < 1f) {
                return false;
            }
            Charge = 0f;
            Player.AddBuff(ModContent.BuffType<DivineSourceChargeBuff>(), EmpowerDuration);
            return true;
        }

        public override void PostUpdate() {
            if (VaultUtils.isServer) {
                return;
            }

            //充能满的边沿提示，只对本机主人响
            bool full = Charge >= 1f;
            if (full && !wasFull && Player.whoAmI == Main.myPlayer && HoldingBlade) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = 0.4f, Volume = 0.9f }, Player.Center);
                PRTLoader.NewParticle<PRT_StarPulseRing>(Player.Center, Vector2.Zero,
                    DivineSourceBladeFX.CyanBright, 0f).Configure(0.06f, 0.6f, 16);
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_DivineTechTriangle>(Player.Center,
                        Main.rand.NextVector2Circular(2.4f, 2.4f) - Vector2.UnitY * 1.5f,
                        DivineSourceBladeFX.CyanBright, Main.rand.NextFloat(0.08f, 0.14f))
                        .Configure(DivineSourceBladeFX.AzureBlue, Main.rand.Next(18, 28));
                }
            }
            wasFull = full;

            //buff 经原版同步，各端都能在这里看到激活爆发
            bool emp = Empowered;
            if (emp && !wasEmpowered) {
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.15f, Volume = 1f }, Player.Center);
                SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.3f, Volume = 0.7f }, Player.Center);
                PRTLoader.NewParticle<PRT_StarPulseRing>(Player.Center, Vector2.Zero,
                    DivineSourceBladeFX.AuricGold, 0f).Configure(0.1f, 1.3f, 22);
                PRTLoader.NewParticle<PRT_StarPulseRing>(Player.Center, Vector2.Zero,
                    DivineSourceBladeFX.CyanBright, 0f).Configure(0.06f, 0.9f, 16);
                for (int i = 0; i < 9; i++) {
                    Vector2 vel = (MathHelper.TwoPi * i / 9f).ToRotationVector2() * Main.rand.NextFloat(2.5f, 5.5f);
                    PRTLoader.NewParticle<PRT_DivineTechTriangle>(Player.Center, vel,
                        DivineSourceBladeFX.AuricGold, Main.rand.NextFloat(0.09f, 0.16f))
                        .Configure(DivineSourceBladeFX.AuricAmber, Main.rand.Next(22, 34));
                }
                for (int i = 0; i < 7; i++) {
                    PRTLoader.NewParticle<PRT_CyberSquare>(Player.Center,
                        Main.rand.NextVector2Circular(4f, 4f),
                        DivineSourceBladeFX.CyanBright, Main.rand.NextFloat(0.7f, 1.2f))
                        .Configure(DivineSourceBladeFX.AuricGold, Main.rand.Next(20, 30));
                }
            }
            wasEmpowered = emp;
        }
    }

    /// <summary>金源充能状态，7 秒强化窗口，伤害加成在武器的 ModifyWeaponDamage 里结算</summary>
    internal class DivineSourceChargeBuff : ModBuff
    {
        public override string Texture => CWRConstant.Buff + "DivineSourceCharge";

        public override void SetStaticDefaults() => Main.buffNoSave[Type] = true;

        public override void Update(Player player, ref int buffIndex) {
            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(player.Center, new Vector3(0.35f, 0.5f, 0.75f));
            //金蓝数据屑绕身上浮
            if (Main.rand.NextBool(7)) {
                Vector2 at = player.Center + Main.rand.NextVector2Circular(26f, 34f);
                bool gold = Main.rand.NextBool(3);
                PRTLoader.NewParticle<PRT_CyberSquare>(at,
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.6f, 1.5f)),
                    gold ? DivineSourceBladeFX.AuricGold : DivineSourceBladeFX.CyanBright,
                    Main.rand.NextFloat(0.5f, 0.9f))
                    .Configure(gold ? DivineSourceBladeFX.AuricAmber : DivineSourceBladeFX.AzureBlue,
                        Main.rand.Next(16, 26));
            }
            if (Main.rand.NextBool(16)) {
                PRTLoader.NewParticle<PRT_DivineTechTriangle>(
                    player.Center + Main.rand.NextVector2Circular(24f, 30f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.8f, 1.6f),
                    DivineSourceBladeFX.AuricGold, Main.rand.NextFloat(0.06f, 0.1f))
                    .Configure(DivineSourceBladeFX.CyanBright, Main.rand.Next(16, 24));
            }
        }
    }

    /// <summary>人物下方的充能条，只画给本机持刀玩家</summary>
    internal class DivineSourceChargeBarLayer : PlayerDrawLayer
    {
        private const int BarWidth = 56;
        private const int BarHeight = 5;

        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.FrontAccFront);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) {
            if (Main.gameMenu || drawInfo.shadow != 0f) {
                return false;
            }
            Player player = drawInfo.drawPlayer;
            if (!player.active || player.dead || player.ghost || player.whoAmI != Main.myPlayer) {
                return false;
            }
            return player.GetModPlayer<DivineSourcePlayer>().HoldingBlade;
        }

        protected override void Draw(ref PlayerDrawSet drawInfo) {
            Player player = drawInfo.drawPlayer;
            DivineSourcePlayer mp = player.GetModPlayer<DivineSourcePlayer>();
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle px = new(0, 0, 1, 1);

            bool empowered = mp.Empowered;
            float fillT = empowered
                ? mp.EmpowerTicksLeft / (float)DivineSourcePlayer.EmpowerDuration
                : mp.Charge;
            bool full = !empowered && mp.Charge >= 1f;

            float time = (float)Main.timeForVisualEffects * 0.05f;
            float pulse = full || empowered ? 0.82f + 0.18f * MathF.Sin(time * 2.6f) : 1f;

            Vector2 anchor = player.Bottom + new Vector2(0f, 10f + player.gfxOffY) - Main.screenPosition;
            Vector2 topLeft = anchor - new Vector2(BarWidth * 0.5f, 0f);

            Color frame = empowered || full
                ? DivineSourceBladeFX.AuricGold * 0.95f
                : DivineSourceBladeFX.DeepNavy * 0.95f;
            Color backing = new Color(8, 12, 28) * 0.88f;

            //外框与内底
            drawInfo.DrawDataCache.Add(new DrawData(pixel, topLeft - Vector2.One, px, frame,
                0f, Vector2.Zero, new Vector2(BarWidth + 2, BarHeight + 2), SpriteEffects.None));
            drawInfo.DrawDataCache.Add(new DrawData(pixel, topLeft, px, backing,
                0f, Vector2.Zero, new Vector2(BarWidth, BarHeight), SpriteEffects.None));

            //填充主体，充能期是金色倒计时，平时是蓝色积攒
            int fillPx = (int)MathF.Round(BarWidth * MathHelper.Clamp(fillT, 0f, 1f));
            if (fillPx > 0) {
                Color fillA = empowered ? DivineSourceBladeFX.AuricAmber : DivineSourceBladeFX.ElectricBlue;
                Color fillB = empowered ? DivineSourceBladeFX.AuricGold : DivineSourceBladeFX.CyanBright;
                drawInfo.DrawDataCache.Add(new DrawData(pixel, topLeft, px, fillA * (0.95f * pulse),
                    0f, Vector2.Zero, new Vector2(fillPx, BarHeight), SpriteEffects.None));
                //上半亮带，读出体积
                drawInfo.DrawDataCache.Add(new DrawData(pixel, topLeft, px, fillB * (0.85f * pulse),
                    0f, Vector2.Zero, new Vector2(fillPx, 2f), SpriteEffects.None));
                //推进沿白色刻线
                if (fillPx < BarWidth) {
                    Color tick = Color.White * 0.9f;
                    tick.A = 0;
                    drawInfo.DrawDataCache.Add(new DrawData(pixel, topLeft + new Vector2(fillPx - 1, 0f), px, tick,
                        0f, Vector2.Zero, new Vector2(1f, BarHeight), SpriteEffects.None));
                }
            }

            //分段刻线压在填充上，科技段格
            Color notch = new Color(4, 7, 18) * 0.9f;
            for (int i = 1; i < 8; i++) {
                drawInfo.DrawDataCache.Add(new DrawData(pixel, topLeft + new Vector2(i * BarWidth / 8f, 0f), px, notch,
                    0f, Vector2.Zero, new Vector2(1f, BarHeight), SpriteEffects.None));
            }

            //左端能量节点，满充/充能期点亮
            Vector2 nodeCenter = topLeft + new Vector2(-6f, BarHeight * 0.5f);
            Color nodeCol = full || empowered
                ? (empowered ? DivineSourceBladeFX.AuricGold : DivineSourceBladeFX.CyanBright) * pulse
                : DivineSourceBladeFX.DeepNavy;
            drawInfo.DrawDataCache.Add(new DrawData(pixel, nodeCenter, px, nodeCol,
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(5f), SpriteEffects.None));

            //满充或充能期垫一层软辉
            if (full || empowered) {
                Texture2D glow = DivineSourceBladeFX.SoftGlow;
                if (glow != null) {
                    Color glowCol = (empowered ? DivineSourceBladeFX.AuricGold : DivineSourceBladeFX.CyanBright) * (0.35f * pulse);
                    glowCol.A = 0;
                    drawInfo.DrawDataCache.Add(new DrawData(glow, anchor + new Vector2(0f, BarHeight * 0.5f), null, glowCol,
                        0f, glow.Size() * 0.5f, new Vector2(BarWidth * 1.6f / glow.Width, 0.28f), SpriteEffects.None));
                }
            }
        }
    }
}
