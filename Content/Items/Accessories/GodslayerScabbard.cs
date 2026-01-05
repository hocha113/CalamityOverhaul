using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories
{
    //弑神者剑鞘饰品主类
    internal class GodslayerScabbard : ModItem
    {
        public override string Texture => CWRConstant.Item_Accessorie + "GodslayerScabbard";
        //拔刀值上限120帧即2秒
        public const int MaxDrawCharge = 120;
        //提供的无敌帧时间
        public const int IFrameTime = 120;

        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.accessory = true;
            Item.value = Item.buyPrice(0, 25, 0, 0);
            Item.rare = CWRID.Rarity_DarkBlue;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.GetModPlayer<GodslayerScabbardPlayer>().EquipScabbard = true;
        }

        public override void AddRecipes() {
            if (!CWRRef.Has) {
                return;
            }
            _ = CreateRecipe().
                AddIngredient(CWRID.Item_CosmiliteBar, 10).
                AddTile(CWRID.Tile_CosmicAnvil).
                Register();
        }
    }

    //玩家效果类负责处理拔刀值积累和无敌帧效果
    internal class GodslayerScabbardPlayer : ModPlayer
    {
        //是否装备剑鞘
        public bool EquipScabbard;
        //当前拔刀值0到MaxDrawCharge
        public int DrawCharge;
        //拔刀值达标标志当拔刀值满时为true
        public bool DrawChargeReady;
        //无敌帧剩余时间
        private int iFrameTimer;
        //充能完成后的视觉脉冲计时
        private int readyPulseTimer;
        //触发无敌帧的视觉效果计时
        private int triggerEffectTimer;

        public override void ResetEffects() {
            EquipScabbard = false;
        }

        public override void PreUpdateMovement() {
            if (!EquipScabbard) {
                //未装备时清空拔刀值
                DrawCharge = 0;
                DrawChargeReady = false;
                readyPulseTimer = 0;
                return;
            }

            Item heldItem = Player.HeldItem;

            //检查是否手持近战武器且未攻击
            bool isMeleeWeapon = heldItem != null && !heldItem.IsAir
                && (heldItem.DamageType == DamageClass.Melee
                || heldItem.DamageType == CWRRef.GetTrueMeleeDamageClass()
                || heldItem.DamageType == CWRRef.GetTrueMeleeNoSpeedDamageClass());

            bool notAttacking = Player.itemAnimation <= 0 && Player.itemTime <= 0;

            if (isMeleeWeapon && notAttacking) {
                //积累拔刀值
                if (DrawCharge < GodslayerScabbard.MaxDrawCharge) {
                    DrawCharge++;
                    //满值时设置标志并播放音效
                    if (DrawCharge >= GodslayerScabbard.MaxDrawCharge && !DrawChargeReady) {
                        DrawChargeReady = true;
                        readyPulseTimer = 60;
                        //播放充能完成音效
                        SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = 0.5f, Volume = 0.6f }, Player.Center);
                        SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.3f, Volume = 0.5f }, Player.Center);
                        //生成充能完成粒子效果
                        SpawnChargeReadyEffect();
                    }
                }
            }
            else if (Player.itemAnimation > 0 && DrawChargeReady) {
                //正在攻击且拔刀值已满，但需要等待OnHitNPC触发无敌帧
                //这里不再直接触发，而是保持蓄力状态
            }

            //更新充能完成的脉冲效果
            if (readyPulseTimer > 0) {
                readyPulseTimer--;
                if (readyPulseTimer % 8 == 0) {
                    SpawnReadyPulseEffect();
                }
            }

            //如果蓄力完成，持续显示待机光环
            if (DrawChargeReady && Main.GameUpdateCount % 4 == 0) {
                SpawnReadyAuraEffect();
            }

            //更新无敌帧计时器
            if (iFrameTimer > 0) {
                iFrameTimer--;
                //无敌帧期间生成保护光环粒子
                if (iFrameTimer % 2 == 0) {
                    SpawnProtectionAura();
                }
            }

            //更新触发效果计时器
            if (triggerEffectTimer > 0) {
                triggerEffectTimer--;
                SpawnTriggerTrailEffect();
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!EquipScabbard || !DrawChargeReady) {
                return;
            }

            //检查是否是近战伤害
            bool isMeleeDamage = hit.DamageType == DamageClass.Melee
                || hit.DamageType == CWRRef.GetTrueMeleeDamageClass()
                || hit.DamageType == CWRRef.GetTrueMeleeNoSpeedDamageClass();

            if (!isMeleeDamage) {
                return;
            }

            //蓄力完成且造成了近战伤害，触发无敌帧
            Player.GivePlayerImmuneState(GodslayerScabbard.IFrameTime, true);
            iFrameTimer = GodslayerScabbard.IFrameTime;
            triggerEffectTimer = 30;

            //播放拔刀音效
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f, Volume = 0.8f }, Player.Center);
            SoundEngine.PlaySound(SoundID.DD2_DarkMageHealImpact with { Pitch = 0.2f, Volume = 0.7f }, Player.Center);

            //生成拔刀特效
            SpawnDrawEffect(target.Center);

            //清空拔刀值
            DrawCharge = 0;
            DrawChargeReady = false;
            readyPulseTimer = 0;
        }

        //生成充能完成的粒子效果
        private void SpawnChargeReadyEffect() {
            if (VaultUtils.isServer) return;

            //使用弑神者主题的深紫蓝色
            Color godslayerBlue = new Color(80, 180, 255);
            Color godslayerPurple = new Color(160, 80, 255);

            //环形爆发
            for (int i = 0; i < 24; i++) {
                float angle = MathHelper.TwoPi * i / 24f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 8f);
                BasePRT spark = new PRT_Spark(
                    Player.Center,
                    velocity,
                    false,
                    Main.rand.Next(20, 35),
                    Main.rand.NextFloat(1.2f, 1.8f),
                    Color.Lerp(godslayerBlue, godslayerPurple, Main.rand.NextFloat()),
                    Player
                );
                PRTLoader.AddParticle(spark);
            }

            //内层光芒
            for (int i = 0; i < 16; i++) {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(6f, 6f);
                BasePRT light = new PRT_Light(
                    Player.Center,
                    velocity,
                    0.6f,
                    godslayerBlue,
                    Main.rand.Next(25, 40),
                    1.5f,
                    2.5f,
                    hueShift: 0.02f
                );
                PRTLoader.AddParticle(light);
            }

            //扩散圆环效果
            for (int i = 0; i < 36; i++) {
                float angle = MathHelper.TwoPi * i / 36f;
                Vector2 pos = Player.Center + angle.ToRotationVector2() * 50f;
                int dust = Dust.NewDust(pos, 0, 0, DustID.Electric, 0, 0, 100, godslayerBlue, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = angle.ToRotationVector2() * 3f;
            }
        }

        //生成蓄力完成后的脉冲效果
        private void SpawnReadyPulseEffect() {
            if (VaultUtils.isServer) return;

            Color pulseColor = new Color(100, 200, 255);
            float pulseScale = 1f + (60 - readyPulseTimer) / 60f * 0.5f;

            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f + Main.GlobalTimeWrappedHourly * 2f;
                Vector2 pos = Player.Center + angle.ToRotationVector2() * 35f * pulseScale;
                int dust = Dust.NewDust(pos, 0, 0, DustID.BlueTorch, 0, 0, 150, pulseColor, 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Vector2.Zero;
            }
        }

        //生成待机光环效果
        private void SpawnReadyAuraEffect() {
            if (VaultUtils.isServer) return;

            float angle = Main.GlobalTimeWrappedHourly * 3f + Main.rand.NextFloat(0.2f);
            Vector2 pos = Player.Center + angle.ToRotationVector2() * 40f;
            Color auraColor = Color.Lerp(new Color(80, 180, 255), new Color(160, 80, 255),
                (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.5f + 0.5f);

            int dust = Dust.NewDust(pos, 0, 0, DustID.Electric, 0, 0, 100, auraColor, 1.0f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity = (Player.Center - pos).SafeNormalize(Vector2.Zero) * 0.5f;
        }

        //生成拔刀瞬间的粒子效果（击中敌人时触发）
        private void SpawnDrawEffect(Vector2 targetPos) {
            if (VaultUtils.isServer) return;

            Vector2 direction = Player.Center.To(targetPos).SafeNormalize(Vector2.UnitX * Player.direction);
            Color godslayerBlue = new Color(80, 180, 255);
            Color godslayerPurple = new Color(160, 80, 255);
            Color godslayerCyan = new Color(100, 255, 255);

            //斩击线特效
            for (int i = 0; i < 20; i++) {
                Vector2 offset = direction.RotatedBy(Main.rand.NextFloat(-0.4f, 0.4f)) * Main.rand.NextFloat(20f, 80f);
                Vector2 vel = -offset.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3f, 8f);
                BasePRT spark = new PRT_Spark(
                    Player.Center + offset,
                    vel,
                    false,
                    Main.rand.Next(15, 30),
                    Main.rand.NextFloat(1.5f, 2.5f),
                    Color.Lerp(godslayerCyan, godslayerBlue, Main.rand.NextFloat()),
                    Player
                );
                PRTLoader.AddParticle(spark);
            }

            //环状冲击波
            for (int i = 0; i < 32; i++) {
                float angle = MathHelper.TwoPi * i / 32f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 14f);
                BasePRT light = new PRT_Light(
                    Player.Center,
                    velocity,
                    0.5f,
                    Color.Lerp(godslayerBlue, godslayerPurple, Main.rand.NextFloat()),
                    Main.rand.Next(20, 35),
                    1.2f,
                    2f,
                    hueShift: 0.01f
                );
                PRTLoader.AddParticle(light);
            }

            //向目标方向的剑气效果
            for (int i = 0; i < 12; i++) {
                float t = i / 12f;
                Vector2 pos = Vector2.Lerp(Player.Center, targetPos, t);
                Vector2 vel = direction.RotatedBy(Main.rand.NextFloat(-0.2f, 0.2f)) * Main.rand.NextFloat(2f, 5f);
                BasePRT spark = new PRT_Spark(
                    pos + Main.rand.NextVector2Circular(10f, 10f),
                    vel,
                    false,
                    Main.rand.Next(10, 20),
                    Main.rand.NextFloat(1f, 1.8f),
                    godslayerCyan,
                    Player
                );
                PRTLoader.AddParticle(spark);
            }

            //爆发光芒
            for (int i = 0; i < 24; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(6f, 12f);
                BasePRT light = new PRT_Light(
                    Player.Center,
                    vel,
                    0.4f,
                    godslayerCyan,
                    Main.rand.Next(15, 25),
                    1f,
                    1.8f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(light);
            }
        }

        //生成触发后的拖尾效果
        private void SpawnTriggerTrailEffect() {
            if (VaultUtils.isServer) return;

            float progress = 1f - (triggerEffectTimer / 30f);
            Color trailColor = Color.Lerp(new Color(100, 255, 255), new Color(80, 180, 255), progress);

            for (int i = 0; i < 2; i++) {
                Vector2 pos = Player.Center + Main.rand.NextVector2Circular(20f, 20f);
                Vector2 vel = -Player.velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1f, 3f);
                BasePRT spark = new PRT_Spark(
                    pos,
                    vel,
                    false,
                    Main.rand.Next(8, 15),
                    Main.rand.NextFloat(0.8f, 1.2f),
                    trailColor * (1f - progress),
                    Player
                );
                PRTLoader.AddParticle(spark);
            }
        }

        //生成保护光环粒子
        private void SpawnProtectionAura() {
            if (VaultUtils.isServer) return;

            float progress = 1f - (iFrameTimer / (float)GodslayerScabbard.IFrameTime);
            float radius = 35f + progress * 15f;
            float intensity = 1f - progress * 0.5f;

            Color godslayerBlue = new Color(80, 180, 255);
            Color godslayerPurple = new Color(160, 80, 255);

            //旋转光环
            float rotAngle = Main.GlobalTimeWrappedHourly * 4f;
            for (int i = 0; i < 3; i++) {
                float angle = rotAngle + MathHelper.TwoPi * i / 3f;
                Vector2 pos = Player.Center + angle.ToRotationVector2() * radius;
                Color color = Color.Lerp(godslayerBlue, godslayerPurple, (float)Math.Sin(angle + Main.GlobalTimeWrappedHourly * 2f) * 0.5f + 0.5f);

                int dust = Dust.NewDust(pos, 0, 0, DustID.Electric, 0, 0, 100, color, 1.2f * intensity);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 2f;
            }

            //随机护盾粒子
            if (Main.rand.NextBool(3)) {
                Vector2 offset = Main.rand.NextVector2Circular(radius, radius);
                BasePRT light = new PRT_Light(
                    Player.Center + offset,
                    -offset.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1f, 2f),
                    0.2f,
                    godslayerBlue * intensity,
                    Main.rand.Next(10, 20),
                    0.8f,
                    1.2f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(light);
            }
        }

        public override void PostUpdate() {
            //绘制拔刀值充能光环
            if (EquipScabbard && DrawCharge > 0 && !DrawChargeReady) {
                float chargeRatio = DrawCharge / (float)GodslayerScabbard.MaxDrawCharge;

                //根据充能进度调整粒子生成频率
                int interval = (int)MathHelper.Lerp(15, 5, chargeRatio);
                if (Main.GameUpdateCount % interval == 0) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float distance = MathHelper.Lerp(50f, 35f, chargeRatio);
                    Vector2 offset = angle.ToRotationVector2() * distance;

                    Color startColor = new Color(60, 100, 140);
                    Color endColor = new Color(100, 200, 255);
                    Color color = Color.Lerp(startColor, endColor, chargeRatio);

                    int dust = Dust.NewDust(Player.Center + offset, 0, 0, DustID.BlueTorch, 0, 0, 100, color, 0.8f + chargeRatio * 0.6f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (Player.Center - Main.dust[dust].position).SafeNormalize(Vector2.Zero) * (1f + chargeRatio * 2f);
                }

                //高充能时添加额外效果
                if (chargeRatio > 0.7f && Main.GameUpdateCount % 8 == 0 && !VaultUtils.isServer) {
                    Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f);
                    BasePRT light = new PRT_Light(
                        Player.Center + Main.rand.NextVector2Circular(30f, 30f),
                        vel,
                        0.15f,
                        new Color(80, 180, 255) * chargeRatio,
                        Main.rand.Next(10, 20),
                        0.6f,
                        1f,
                        hueShift: 0f
                    );
                    PRTLoader.AddParticle(light);
                }
            }
        }
    }
}
