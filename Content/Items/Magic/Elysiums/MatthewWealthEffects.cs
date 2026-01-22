using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 圣马修（Matthew）门徒的财富祝福效果
    /// 处理敌人死亡时的金币爆发和神圣财富特效
    /// </summary>
    internal class MatthewGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>是否被马修祝福标记</summary>
        public bool BlessedByMatthew { get; set; } = false;

        /// <summary>祝福的玩家索引</summary>
        public int BlessingOwner { get; set; } = -1;

        /// <summary>祝福强度（影响掉落倍率和特效）</summary>
        public float BlessingStrength { get; set; } = 1f;

        public override void OnKill(NPC npc) {
            if (!BlessedByMatthew || BlessingOwner < 0) return;
            if (npc.friendly || npc.CountsAsACritter || npc.SpawnedFromStatue) return;

            Player owner = Main.player[BlessingOwner];
            if (owner == null || !owner.active) return;

            //生成财富祝福死亡特效
            if (!VaultUtils.isServer) {
                MatthewWealthEffects.SpawnDeathBlessingEffect(npc, BlessingStrength);
            }

            //额外掉落金币
            if (!VaultUtils.isClient) {
                SpawnBonusCoins(npc, owner);
            }
        }

        /// <summary>
        /// 生成额外金币掉落
        /// </summary>
        private void SpawnBonusCoins(NPC npc, Player owner) {
            //基础金币数量
            int baseCoins = (int)(npc.value / 100f * BlessingStrength); //基于NPC价值

            if (baseCoins <= 0) return;

            //随机额外金币
            int bonusCoins = Main.rand.Next(baseCoins / 2, baseCoins + 1);

            if (bonusCoins > 0) {
                //转换为实际金币物品
                int platinum = bonusCoins / 1000000;
                bonusCoins %= 1000000;
                int gold = bonusCoins / 10000;
                bonusCoins %= 10000;
                int silver = bonusCoins / 100;
                int copper = bonusCoins % 100;

                //生成金币物品
                if (platinum > 0) {
                    SpawnCoinItem(npc, ItemID.PlatinumCoin, Math.Min(platinum, 5));
                }
                if (gold > 0) {
                    SpawnCoinItem(npc, ItemID.GoldCoin, Math.Min(gold, 10));
                }
                if (silver > 0) {
                    SpawnCoinItem(npc, ItemID.SilverCoin, Math.Min(silver, 20));
                }
                if (copper > 0) {
                    SpawnCoinItem(npc, ItemID.CopperCoin, Math.Min(copper, 50));
                }
            }
        }

        /// <summary>
        /// 生成金币物品
        /// </summary>
        private static void SpawnCoinItem(NPC npc, int coinType, int amount) {
            Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 6f);
            int item = Item.NewItem(npc.GetSource_Loot(), npc.Hitbox, coinType, amount);
            if (item >= 0 && item < Main.maxItems) {
                Main.item[item].velocity = velocity;
                if (!VaultUtils.isSinglePlayer) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, item);
                }
            }
        }
    }

    /// <summary>
    /// 圣马修的财富祝福视觉效果
    /// </summary>
    internal static class MatthewWealthEffects
    {
        #region 颜色定义
        /// <summary>财富金色 - 主色调</summary>
        public static Color WealthGold => new Color(255, 215, 100);
        /// <summary>铂金白 - 高光</summary>
        public static Color PlatinumWhite => new Color(255, 255, 240);
        /// <summary>深金色 - 阴影</summary>
        public static Color DeepGold => new Color(200, 160, 50);
        /// <summary>神圣黄 - 光芒</summary>
        public static Color HolyYellow => new Color(255, 240, 150);
        /// <summary>铜色 - 辅助</summary>
        public static Color CopperTone => new Color(200, 140, 80);
        #endregion

        #region 死亡祝福特效
        /// <summary>
        /// 敌人死亡时的财富祝福完整特效
        /// </summary>
        public static void SpawnDeathBlessingEffect(NPC npc, float strength) {
            if (VaultUtils.isServer) return;

            Vector2 center = npc.Center;
            float size = Math.Max(npc.width, npc.height);
            float effectScale = Math.Min(1f + size / 100f, 2.5f) * strength;

            //播放神圣金币音效
            PlayWealthSounds(center);

            //阶段1：金币爆发
            SpawnCoinBurst(center, effectScale);

            //阶段2：财富光环
            SpawnWealthHalo(center, effectScale);

            //阶段3：上升的金色符文
            SpawnGoldenRunes(center, effectScale);

            //阶段4：神圣十字架（马修的象征）
            SpawnHolyCross(center, effectScale);

            //阶段5：金色光柱
            SpawnGoldenPillar(center, effectScale);

            //阶段6：钱袋符号
            SpawnMoneyBagSymbol(center, effectScale);

            //阶段7：扩散的财富波纹
            SpawnWealthRipples(center, effectScale);
        }
        #endregion

        #region 音效
        private static void PlayWealthSounds(Vector2 center) {
            //金币叮当声
            SoundEngine.PlaySound(SoundID.CoinPickup with {
                Volume = 1.2f,
                Pitch = 0.2f,
                PitchVariance = 0.15f
            }, center);

            //神圣回响
            SoundEngine.PlaySound(SoundID.Item29 with {
                Volume = 0.6f,
                Pitch = 0.4f
            }, center);
        }
        #endregion

        #region 金币爆发
        /// <summary>
        /// 生成金币爆发效果
        /// </summary>
        private static void SpawnCoinBurst(Vector2 center, float scale) {
            int coinCount = (int)(25 * scale);

            for (int i = 0; i < coinCount; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float speed = Main.rand.NextFloat(4f, 12f) * scale;
                Vector2 velocity = angle.ToRotationVector2() * speed;

                //添加向上的偏移（金币会往上飞）
                velocity.Y -= Main.rand.NextFloat(2f, 5f);

                //随机金币颜色（金/银/铜）
                Color coinColor = Main.rand.Next(3) switch {
                    0 => WealthGold,
                    1 => new Color(200, 200, 220), //银色
                    _ => CopperTone
                };

                //金币粒子
                BasePRT particle = new PRT_Light(
                    center + Main.rand.NextVector2Circular(20f * scale, 20f * scale),
                    velocity,
                    Main.rand.NextFloat(0.15f, 0.3f) * scale,
                    coinColor,
                    Main.rand.Next(30, 50),
                    1f,
                    1.5f,
                    hueShift: Main.rand.NextFloat(-0.02f, 0.02f)
                );
                PRTLoader.AddParticle(particle);

                //金币闪光
                if (i % 3 == 0) {
                    BasePRT spark = new PRT_Spark(
                        center,
                        velocity * 0.8f,
                        true, //受重力影响
                        Main.rand.Next(20, 35),
                        0.7f * scale,
                        WealthGold,
                        null
                    );
                    PRTLoader.AddParticle(spark);
                }
            }

            //金币Dust
            for (int i = 0; i < (int)(20 * scale); i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 8f);
                vel.Y -= 2f;
                Dust d = Dust.NewDustPerfect(center + Main.rand.NextVector2Circular(15f, 15f),
                    DustID.GoldCoin, vel, 100, WealthGold, Main.rand.NextFloat(1f, 1.5f));
                d.noGravity = false; //让金币尘埃下落
            }
        }
        #endregion

        #region 财富光环
        /// <summary>
        /// 生成财富光环
        /// </summary>
        private static void SpawnWealthHalo(Vector2 center, float scale) {
            float haloRadius = 40f * scale;
            int segments = (int)(20 * scale);

            //主光环
            for (int i = 0; i < segments; i++) {
                float angle = MathHelper.TwoPi * i / segments;
                Vector2 pos = center + angle.ToRotationVector2() * haloRadius;
                Vector2 velocity = angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 3f;

                BasePRT particle = new PRT_Light(
                    pos,
                    velocity,
                    0.18f * scale,
                    WealthGold,
                    Main.rand.Next(25, 40),
                    1f,
                    1.2f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //内层光环（白金色）
            for (int i = 0; i < segments / 2; i++) {
                float angle = MathHelper.TwoPi * i / (segments / 2);
                Vector2 pos = center + angle.ToRotationVector2() * (haloRadius * 0.5f);

                Dust d = Dust.NewDustPerfect(pos, DustID.GoldFlame,
                    angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 2f,
                    100, PlatinumWhite, 0.9f);
                d.noGravity = true;
            }
        }
        #endregion

        #region 金色符文
        /// <summary>
        /// 生成上升的金色符文
        /// </summary>
        private static void SpawnGoldenRunes(Vector2 center, float scale) {
            int runeCount = (int)(8 * scale);

            for (int i = 0; i < runeCount; i++) {
                float angle = MathHelper.TwoPi * i / runeCount;
                float radius = 30f * scale;
                Vector2 startPos = center + angle.ToRotationVector2() * radius;

                //上升的符文
                Vector2 velocity = new Vector2(
                    MathF.Sin(angle) * 1f,
                    -3f - Main.rand.NextFloat(1f, 3f)
                );

                BasePRT rune = new PRT_Light(
                    startPos,
                    velocity,
                    0.25f * scale,
                    Color.Lerp(WealthGold, HolyYellow, Main.rand.NextFloat()),
                    Main.rand.Next(40, 60),
                    1.1f,
                    1.4f,
                    hueShift: 0.005f
                );
                PRTLoader.AddParticle(rune);

                //符文尾迹
                for (int trail = 0; trail < 3; trail++) {
                    Vector2 trailPos = startPos + new Vector2(0, trail * 8f);
                    Dust d = Dust.NewDustPerfect(trailPos, DustID.GoldCoin,
                        velocity * 0.3f, 100, WealthGold, 0.6f - trail * 0.15f);
                    d.noGravity = true;
                }
            }
        }
        #endregion

        #region 神圣十字架
        /// <summary>
        /// 生成神圣十字架效果
        /// </summary>
        private static void SpawnHolyCross(Vector2 center, float scale) {
            float crossLength = 50f * scale;
            float armOffset = -15f * scale;

            //垂直臂
            SpawnCrossArm(center, Vector2.UnitY * -1, crossLength * 0.35f, WealthGold);
            SpawnCrossArm(center, Vector2.UnitY, crossLength * 0.65f, WealthGold);

            //水平臂
            Vector2 armCenter = center + new Vector2(0, armOffset);
            SpawnCrossArm(armCenter, Vector2.UnitX * -1, crossLength * 0.4f, WealthGold);
            SpawnCrossArm(armCenter, Vector2.UnitX, crossLength * 0.4f, WealthGold);

            //十字架中心爆发
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f);
                BasePRT particle = new PRT_Light(
                    armCenter + Main.rand.NextVector2Circular(5f, 5f),
                    vel,
                    Main.rand.NextFloat(0.2f, 0.35f),
                    PlatinumWhite,
                    Main.rand.Next(20, 35),
                    1.2f,
                    1.3f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //四方向光芒
            for (int dir = 0; dir < 4; dir++) {
                float angle = MathHelper.PiOver2 * dir;
                for (int i = 0; i < 6; i++) {
                    float dist = 5f + i * 5f;
                    Vector2 pos = armCenter + angle.ToRotationVector2() * dist;

                    BasePRT spark = new PRT_Spark(
                        pos,
                        angle.ToRotationVector2() * (2f + i * 0.3f),
                        false,
                        Main.rand.Next(15, 25),
                        0.7f - i * 0.08f,
                        Color.Lerp(PlatinumWhite, WealthGold, i / 6f),
                        null
                    );
                    PRTLoader.AddParticle(spark);
                }
            }
        }

        /// <summary>
        /// 生成十字架臂
        /// </summary>
        private static void SpawnCrossArm(Vector2 start, Vector2 direction, float length, Color color) {
            int count = (int)(length / 6f);

            for (int i = 0; i <= count; i++) {
                float t = i / (float)count;
                float dist = length * t;
                Vector2 pos = start + direction * dist;

                float scale = 0.2f * (1f - t * 0.4f);

                BasePRT particle = new PRT_Light(
                    pos,
                    direction * 0.5f,
                    scale,
                    Color.Lerp(PlatinumWhite, color, t * 0.6f),
                    Main.rand.Next(30, 45),
                    1f,
                    1.2f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }
        }
        #endregion

        #region 金色光柱
        /// <summary>
        /// 生成金色光柱效果
        /// </summary>
        private static void SpawnGoldenPillar(Vector2 center, float scale) {
            float pillarHeight = 100f * scale;

            //从中心向上的光柱
            for (int i = 0; i < 30; i++) {
                float t = i / 30f;
                float height = pillarHeight * t;
                Vector2 pos = center - new Vector2(Main.rand.NextFloat(-10f, 10f) * scale, height);

                //光柱粒子
                Vector2 velocity = new Vector2(0, -4f - t * 2f);

                float alpha = 1f - t * 0.5f;
                BasePRT particle = new PRT_Light(
                    pos,
                    velocity,
                    (0.2f - t * 0.1f) * scale,
                    WealthGold * alpha,
                    Main.rand.Next(20, 35),
                    0.9f,
                    1.1f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //光柱底部的爆发
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 vel = angle.ToRotationVector2() * 3f + new Vector2(0, -2f);

                Dust d = Dust.NewDustPerfect(center, DustID.GoldFlame,
                    vel, 100, HolyYellow, 1.2f);
                d.noGravity = true;
            }
        }
        #endregion

        #region 钱袋符号
        /// <summary>
        /// 生成钱袋符号效果（马修的象征物）
        /// </summary>
        private static void SpawnMoneyBagSymbol(Vector2 center, float scale) {
            //钱袋形状：圆形底部 + 收口顶部
            Vector2 bagCenter = center - new Vector2(0, 20f * scale);
            float bagRadius = 15f * scale;

            //底部圆形
            int circlePoints = 12;
            for (int i = 0; i < circlePoints; i++) {
                float angle = MathHelper.Pi + MathHelper.Pi * i / circlePoints; //下半圆
                Vector2 pos = bagCenter + angle.ToRotationVector2() * bagRadius;

                Dust d = Dust.NewDustPerfect(pos, DustID.GoldCoin,
                    new Vector2(0, -1f), 100, DeepGold, 0.8f);
                d.noGravity = true;
            }

            //收口部分（两条向上收拢的线）
            for (int side = -1; side <= 1; side += 2) {
                for (int i = 0; i < 5; i++) {
                    float t = i / 4f;
                    Vector2 pos = bagCenter + new Vector2(
                        side * bagRadius * (1f - t * 0.7f),
                        -bagRadius * 0.5f - t * bagRadius * 0.8f
                    );

                    Dust d = Dust.NewDustPerfect(pos, DustID.GoldCoin,
                        new Vector2(0, -1.5f), 100, WealthGold, 0.7f);
                    d.noGravity = true;
                }
            }

            //顶部结
            Vector2 knotPos = bagCenter + new Vector2(0, -bagRadius * 1.5f);
            BasePRT knot = new PRT_Light(
                knotPos,
                new Vector2(0, -2f),
                0.2f * scale,
                WealthGold,
                30,
                1f,
                1.2f,
                hueShift: 0f
            );
            PRTLoader.AddParticle(knot);

            //钱袋上的$符号
            SpawnDollarSign(bagCenter, scale * 0.6f);
        }

        /// <summary>
        /// 生成$符号
        /// </summary>
        private static void SpawnDollarSign(Vector2 center, float scale) {
            //S形状的点
            List<Vector2> sPoints = [
                new(5, -8), new(3, -10), new(-1, -10), new(-4, -8),
                new(-4, -5), new(-1, -3), new(2, -1), new(4, 2),
                new(3, 5), new(-1, 6), new(-4, 5), new(-5, 3)
            ];

            foreach (Vector2 point in sPoints) {
                Vector2 pos = center + point * scale;
                Dust d = Dust.NewDustPerfect(pos, DustID.GoldFlame,
                    new Vector2(0, -0.5f), 100, PlatinumWhite, 0.5f);
                d.noGravity = true;
            }

            //垂直线
            for (int i = -12; i <= 8; i += 3) {
                Vector2 pos = center + new Vector2(0, i) * scale;
                Dust d = Dust.NewDustPerfect(pos, DustID.GoldFlame,
                    new Vector2(0, -0.5f), 100, PlatinumWhite, 0.4f);
                d.noGravity = true;
            }
        }
        #endregion

        #region 财富波纹
        /// <summary>
        /// 生成扩散的财富波纹
        /// </summary>
        private static void SpawnWealthRipples(Vector2 center, float scale) {
            //三层波纹
            for (int wave = 0; wave < 3; wave++) {
                float waveRadius = (15f + wave * 20f) * scale;
                int particleCount = 10 + wave * 4;
                float speed = (5f + wave * 2f);

                for (int i = 0; i < particleCount; i++) {
                    float angle = MathHelper.TwoPi * i / particleCount + wave * 0.2f;
                    Vector2 pos = center + angle.ToRotationVector2() * waveRadius;
                    Vector2 velocity = angle.ToRotationVector2() * speed;

                    Color color = wave switch {
                        0 => PlatinumWhite,
                        1 => WealthGold,
                        _ => DeepGold
                    };

                    BasePRT particle = new PRT_Spark(
                        pos,
                        velocity,
                        false,
                        Main.rand.Next(15, 25),
                        0.8f - wave * 0.15f,
                        color,
                        null
                    );
                    PRTLoader.AddParticle(particle);
                }
            }
        }
        #endregion

        #region 持续祝福光环
        /// <summary>
        /// 门徒的持续祝福光环效果（每帧调用）
        /// </summary>
        public static void SpawnPassiveBlessingAura(Vector2 discipleCenter, int timer) {
            if (VaultUtils.isServer) return;

            //每15帧生成一个金币粒子
            if (timer % 15 == 0) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = discipleCenter + angle.ToRotationVector2() * Main.rand.NextFloat(10f, 25f);

                BasePRT particle = new PRT_Light(
                    pos,
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-1f, 0f)),
                    0.1f,
                    WealthGold,
                    Main.rand.Next(20, 35),
                    0.7f,
                    0.9f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //每30帧生成一个小金币Dust
            if (timer % 30 == 0) {
                Dust d = Dust.NewDustPerfect(
                    discipleCenter + Main.rand.NextVector2Circular(15f, 15f),
                    DustID.GoldCoin,
                    new Vector2(0, -1f),
                    100, WealthGold, 0.6f);
                d.noGravity = true;
            }
        }

        /// <summary>
        /// 祝福敌人时的标记效果
        /// </summary>
        public static void SpawnBlessingMark(NPC target) {
            if (VaultUtils.isServer) return;

            Vector2 center = target.Center;

            //小型金色十字标记
            for (int arm = 0; arm < 4; arm++) {
                float angle = MathHelper.PiOver2 * arm;
                for (int i = 1; i <= 3; i++) {
                    Vector2 pos = center + angle.ToRotationVector2() * (i * 5f);
                    Dust d = Dust.NewDustPerfect(pos, DustID.GoldFlame,
                        angle.ToRotationVector2() * 0.5f, 100, WealthGold, 0.7f - i * 0.15f);
                    d.noGravity = true;
                }
            }

            //金色光晕
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f;
                Vector2 pos = center + angle.ToRotationVector2() * 20f;

                Dust d = Dust.NewDustPerfect(pos, DustID.GoldCoin,
                    (center - pos).SafeNormalize(Vector2.Zero) * 2f,
                    100, WealthGold, 0.5f);
                d.noGravity = true;
            }
        }
        #endregion
    }
}
