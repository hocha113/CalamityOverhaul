using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.Core;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Tools
{
    ///<summary>
    ///鱼油 - 可饮用消耗品
    ///</summary>
    internal class Fishoil : ModItem
    {
        public override string Texture => CWRConstant.Item + "Tools/Fishoil";

        public override void SetDefaults() {
            Item.width = 20;
            Item.height = 26;
            Item.useAnimation = 17;
            Item.useTime = 17;
            Item.useStyle = ItemUseStyleID.DrinkLiquid;
            Item.useTurn = true;
            Item.UseSound = SoundID.Item3; //饮用声音
            Item.maxStack = 9999;
            Item.consumable = true;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.buyPrice(0, 0, 2, 50);
            Item.buffType = ModContent.BuffType<FishoilBuff>();
            Item.buffTime = 60 * 60 * 8; //8分钟持续时间
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                //生成饮用特效弹幕
                Projectile.NewProjectile(
                    player.GetSource_ItemUse(Item),
                    player.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<FishoilDrinkEffect>(),
                    0,
                    0,
                    player.whoAmI
                );

                //播放特殊音效
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = -0.3f }, player.Center);
            }
            return true;
        }
    }

    ///<summary>
    ///鱼油Buff效果
    ///</summary>
    internal class FishoilBuff : ModBuff
    {
        public override string Texture => CWRConstant.Buff + "FishoilBuff";

        public override void SetStaticDefaults() {
            Main.debuff[Type] = false; //这不是Debuff
            Main.buffNoSave[Type] = false; //可以保存
            Main.buffNoTimeDisplay[Type] = false; //显示时间
        }

        public override void Update(Player player, ref int buffIndex) {
            //提供多种增益效果
            player.moveSpeed += 0.15f; //移动速度提升15%
            player.fishingSkill += 10; //渔力+10
            player.statLifeMax2 += 20; //最大生命+20
            player.statManaMax2 += 20; //最大魔力+20
            player.lifeRegen += 2; //生命再生+2

            //每秒恢复少量生命和魔力
            if (player.miscCounter % 60 == 0) {
                player.statLife += 1;
                player.statMana += 1;
            }

            //水下呼吸
            player.gills = true;

            //轻微发光效果
            if (!Main.dedServ && Main.rand.NextBool(10)) {
                Dust dust = Dust.NewDustDirect(
                    player.position,
                    player.width,
                    player.height,
                    DustID.Water,
                    0f,
                    -2f,
                    100,
                    new Color(100, 200, 255),
                    0.8f
                );
                dust.noGravity = true;
                dust.velocity *= 0.3f;
            }
        }
    }

    ///<summary>
    ///鱼油饮用特效弹幕
    ///</summary>
    internal class FishoilDrinkEffect : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int EffectDuration = 90; //特效持续时间

        public override void SetDefaults() {
            Projectile.width = 1;
            Projectile.height = 1;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = EffectDuration;
            Projectile.alpha = 255;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = owner.Center;
            Projectile.ai[0]++;

            float progress = Projectile.ai[0] / EffectDuration;

            //阶段1：能量汇聚 (0-30帧)
            if (Projectile.ai[0] < 30) {
                SpawnGatherParticles(owner);
            }
            //阶段2：能量爆发 (30-60帧)
            else if (Projectile.ai[0] == 30) {
                SpawnBurstEffect(owner);
            }
            //阶段3：淡出 (60-90帧)
            else if (Projectile.ai[0] > 60) {
                SpawnFadeParticles(owner);
            }

            //照明效果
            float lightIntensity = (float)System.Math.Sin(progress * System.Math.PI) * 1.5f;
            Lighting.AddLight(owner.Center,
                0.4f * lightIntensity,
                0.8f * lightIntensity,
                1.0f * lightIntensity);

            //音效
            if (Projectile.ai[0] == 30) {
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.5f, Pitch = 0.3f }, owner.Center);
            }
        }

        //汇聚粒子效果
        private void SpawnGatherParticles(Player owner) {
            if (Projectile.ai[0] % 3 != 0) return;

            for (int i = 0; i < 2; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float distance = Main.rand.NextFloat(80f, 150f);
                Vector2 spawnPos = owner.Center + angle.ToRotationVector2() * distance;
                Vector2 velocity = (owner.Center - spawnPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3f, 6f);

                //使用水波粒子
                var prt = PRTLoader.NewParticle<PRT_SporeBobo>(spawnPos, velocity);
                if (prt != null) {
                    prt.Scale = Main.rand.NextFloat(0.6f, 1.0f);
                    prt.Color = new Color(100, 200, 255);
                }

                //水尘埃
                Dust dust = Dust.NewDustPerfect(spawnPos, DustID.Water, velocity, 100, new Color(100, 200, 255), 1.2f);
                dust.noGravity = true;
            }
        }

        //爆发效果
        private void SpawnBurstEffect(Player owner) {
            //生成环形爆发粒子
            for (int i = 0; i < 32; i++) {
                float angle = MathHelper.TwoPi * i / 32f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 8f);

                var prt = PRTLoader.NewParticle<PRT_SporeBobo>(owner.Center, velocity);
                if (prt != null) {
                    prt.Scale = Main.rand.NextFloat(0.8f, 1.3f);
                    prt.Color = new Color(150, 220, 255);
                }

                //水花尘埃
                Dust dust = Dust.NewDustPerfect(owner.Center, DustID.Water, velocity, 100, new Color(100, 200, 255), 1.5f);
                dust.noGravity = true;
            }

            //冲击波尘埃环
            for (int i = 0; i < 50; i++) {
                float angle = MathHelper.TwoPi * i / 50f;
                Vector2 dustPos = owner.Center + angle.ToRotationVector2() * 30f;
                Dust dust = Dust.NewDustPerfect(dustPos, DustID.WaterCandle, angle.ToRotationVector2() * 3f, 150, new Color(150, 220, 255), 1.0f);
                dust.noGravity = true;
            }

            //向上飘浮的气泡
            for (int i = 0; i < 20; i++) {
                Vector2 bubblePos = owner.Center + Main.rand.NextVector2Circular(owner.width * 0.5f, owner.height * 0.5f);
                Dust bubble = Dust.NewDustPerfect(bubblePos, DustID.SilverCoin, new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(2f, 4f)), 100, new Color(200, 230, 255), Main.rand.NextFloat(0.8f, 1.5f));
                bubble.noGravity = true;
            }
        }

        //淡出粒子效果
        private void SpawnFadeParticles(Player owner) {
            if (Projectile.ai[0] % 5 != 0) return;

            for (int i = 0; i < 2; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(2f, 2f) + new Vector2(0, -2f);
                
                var prt = PRTLoader.NewParticle<PRT_SporeBobo>(
                    owner.Center + Main.rand.NextVector2Circular(owner.width * 0.5f, owner.height * 0.5f),
                    velocity
                );
                if (prt != null) {
                    prt.Scale = Main.rand.NextFloat(0.4f, 0.7f);
                    prt.Color = new Color(100, 200, 255);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //不绘制弹幕本体，只绘制粒子效果
            return false;
        }
    }
}
