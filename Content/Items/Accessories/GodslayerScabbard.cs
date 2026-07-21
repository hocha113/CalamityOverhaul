using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories
{
    /// 弑神者剑鞘饰品
    internal class GodslayerScabbard : ModItem
    {
        public override string Texture => CWRConstant.Item_Accessorie + "GodslayerScabbard";
        //拔刀值上限(180帧≈3s)
        public const int MaxDrawCharge = 180;
        //无敌帧时长(帧)
        public const int IFrameTime = 120;

        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.accessory = true;
            Item.value = Item.buyPrice(0, 25, 0, 0);
            Item.rare = CWRID.Rarity_CosmicPurple;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.GetModPlayer<GodslayerScabbardPlayer>().EquipScabbard = true;
        }

        public override void AddRecipes() {
            if (CWRID.Item_CosmiliteBar > 0 && CWRID.Tile_CosmicAnvil > 0) {
                _ = CreateRecipe().
                AddIngredient(CWRID.Item_CosmiliteBar, 10).
                AddTile(CWRID.Tile_CosmicAnvil).
                Register();
            }
        }
    }

    /// 拔刀值积累与无敌帧
    internal class GodslayerScabbardPlayer : ModPlayer
    {
        //已装备剑鞘
        public bool EquipScabbard;
        //当前拔刀值[0,MaxDrawCharge]
        public int DrawCharge;
        //拔刀值已满
        public bool DrawChargeReady;
        //无敌帧剩余
        private int iFrameTimer;
        //充能完成脉冲计时
        private int readyPulseTimer;
        //触发无敌视觉计时
        private int triggerEffectTimer;
        //上帧攻击动画状态
        private bool wasAttacking;
        //本挥是否命中
        private bool hasHitThisSwing;
        //打空失败效果计时
        private int missEffectTimer;

        public override void ResetEffects() {
            EquipScabbard = false;
        }

        public override void PreUpdateMovement() {
            if (!EquipScabbard) {
                DrawCharge = 0;
                DrawChargeReady = false;
                readyPulseTimer = 0;
                wasAttacking = false;
                hasHitThisSwing = false;
                return;
            }

            Item heldItem = Player.HeldItem;
            bool isMeleeWeapon = heldItem != null && !heldItem.IsAir
                && (heldItem.DamageType == DamageClass.Melee
                || heldItem.DamageType == CWRRef.GetTrueMeleeDamageClass()
                || heldItem.DamageType == CWRRef.GetTrueMeleeNoSpeedDamageClass());

            bool isAttacking = Player.itemAnimation > 0;
            bool notAttacking = !isAttacking && Player.itemTime <= 0;
            if (wasAttacking && !isAttacking && DrawChargeReady && isMeleeWeapon) {
                if (!hasHitThisSwing) {
                    DrawCharge = 0;
                    DrawChargeReady = false;
                    readyPulseTimer = 0;
                    missEffectTimer = 20;
                    SoundEngine.PlaySound(SoundID.Item64 with { Pitch = -0.5f, Volume = 0.5f }, Player.Center);
                    SpawnMissEffect();
                }
                hasHitThisSwing = false;
            }
            wasAttacking = isAttacking;

            if (isMeleeWeapon && notAttacking) {
                if (DrawCharge < GodslayerScabbard.MaxDrawCharge) {
                    DrawCharge++;
                    if (DrawCharge >= GodslayerScabbard.MaxDrawCharge && !DrawChargeReady) {
                        DrawChargeReady = true;
                        readyPulseTimer = 60;
                        SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = 0.5f, Volume = 0.6f }, Player.Center);
                        SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.3f, Volume = 0.5f }, Player.Center);
                        SpawnChargeReadyEffect();
                    }
                }
            }
            if (readyPulseTimer > 0) {
                readyPulseTimer--;
                if (readyPulseTimer % 8 == 0) {
                    SpawnReadyPulseEffect();
                }
            }
            if (DrawChargeReady && Main.GameUpdateCount % 4 == 0) {
                SpawnReadyAuraEffect();
            }
            if (iFrameTimer > 0) {
                iFrameTimer--;
                if (iFrameTimer % 2 == 0) {
                    SpawnProtectionAura();
                }
            }
            if (triggerEffectTimer > 0) {
                triggerEffectTimer--;
                SpawnTriggerTrailEffect();
            }
            if (missEffectTimer > 0) {
                missEffectTimer--;
                if (missEffectTimer % 4 == 0) {
                    SpawnMissTrailEffect();
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!EquipScabbard) {
                return;
            }
            bool isMeleeDamage = hit.DamageType == DamageClass.Melee
                || hit.DamageType == CWRRef.GetTrueMeleeDamageClass()
                || hit.DamageType == CWRRef.GetTrueMeleeNoSpeedDamageClass();

            if (!isMeleeDamage) {
                return;
            }
            hasHitThisSwing = true;

            if (!DrawChargeReady) {
                return;
            }
            Player.GivePlayerImmuneState(GodslayerScabbard.IFrameTime, true);
            iFrameTimer = GodslayerScabbard.IFrameTime;
            triggerEffectTimer = 30;
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f, Volume = 0.8f }, Player.Center);
            SoundEngine.PlaySound(SoundID.DD2_DarkMageHealImpact with { Pitch = 0.2f, Volume = 0.7f }, Player.Center);
            SpawnDrawEffect(target.Center);
            DrawCharge = 0;
            DrawChargeReady = false;
            readyPulseTimer = 0;
        }
        private void SpawnMissEffect() {
            if (VaultUtils.isServer) return;
            Color missColor = new Color(100, 100, 150);
            Color darkBlue = new Color(40, 60, 100);
            for (int i = 0; i < 16; i++) {
                float angle = MathHelper.TwoPi * i / 16f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 6f);
                PRTLoader.NewParticle<PRT_Spark>(Player.Center, velocity, Color.Lerp(missColor, darkBlue, Main.rand.NextFloat()), Main.rand.NextFloat(0.8f, 1.2f)).Configure(true, Main.rand.Next(15, 25), Player);
            }
            for (int i = 0; i < 12; i++) {
                Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f);
                velocity.Y -= 2f;//略上抬
                int dust = Dust.NewDust(Player.Center, 0, 0, DustID.Electric, velocity.X, velocity.Y, 180, missColor, 0.8f);
                Main.dust[dust].noGravity = false;
                Main.dust[dust].fadeIn = 0.5f;
            }
        }
        private void SpawnMissTrailEffect() {
            if (VaultUtils.isServer) return;

            float progress = 1f - (missEffectTimer / 20f);
            Color trailColor = new Color(80, 80, 120) * (1f - progress);

            Vector2 pos = Player.Center + Main.rand.NextVector2Circular(15f, 15f);
            int dust = Dust.NewDust(pos, 0, 0, DustID.Smoke, 0, -1f, 150, trailColor, 0.6f);
            Main.dust[dust].noGravity = true;
        }
        private void SpawnChargeReadyEffect() {
            if (VaultUtils.isServer) return;
            Color godslayerBlue = new Color(80, 180, 255);
            Color godslayerPurple = new Color(160, 80, 255);
            for (int i = 0; i < 24; i++) {
                float angle = MathHelper.TwoPi * i / 24f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 8f);
                PRTLoader.NewParticle<PRT_Spark>(Player.Center, velocity, Color.Lerp(godslayerBlue, godslayerPurple, Main.rand.NextFloat()), Main.rand.NextFloat(1.2f, 1.8f)).Configure(false, Main.rand.Next(20, 35), Player);
            }
            for (int i = 0; i < 16; i++) {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(6f, 6f);
                PRTLoader.NewParticle<PRT_Light>(
                    Player.Center,
                    velocity,
                    godslayerBlue,
                    0.6f
                ).Configure(Main.rand.Next(25, 40), opacity: 1.5f, squishStrenght: 2.5f, hueShift: 0.02f);
            }
            for (int i = 0; i < 36; i++) {
                float angle = MathHelper.TwoPi * i / 36f;
                Vector2 pos = Player.Center + angle.ToRotationVector2() * 50f;
                int dust = Dust.NewDust(pos, 0, 0, DustID.Electric, 0, 0, 100, godslayerBlue, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = angle.ToRotationVector2() * 3f;
            }
        }
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
        private void SpawnDrawEffect(Vector2 targetPos) {
            if (VaultUtils.isServer) return;

            Vector2 direction = Player.Center.To(targetPos).SafeNormalize(Vector2.UnitX * Player.direction);
            Color godslayerBlue = new Color(80, 180, 255);
            Color godslayerPurple = new Color(160, 80, 255);
            Color godslayerCyan = new Color(100, 255, 255);
            for (int i = 0; i < 20; i++) {
                Vector2 offset = direction.RotatedBy(Main.rand.NextFloat(-0.4f, 0.4f)) * Main.rand.NextFloat(20f, 80f);
                Vector2 vel = -offset.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3f, 8f);
                PRTLoader.NewParticle<PRT_Spark>(Player.Center + offset, vel, Color.Lerp(godslayerCyan, godslayerBlue, Main.rand.NextFloat()), Main.rand.NextFloat(1.5f, 2.5f)).Configure(false, Main.rand.Next(15, 30), Player);
            }
            for (int i = 0; i < 32; i++) {
                float angle = MathHelper.TwoPi * i / 32f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 14f);
                PRTLoader.NewParticle<PRT_Light>(
                    Player.Center,
                    velocity,
                    Color.Lerp(godslayerBlue, godslayerPurple, Main.rand.NextFloat()),
                    0.5f
                ).Configure(Main.rand.Next(20, 35), opacity: 1.2f, squishStrenght: 2f, hueShift: 0.01f);
            }
            for (int i = 0; i < 12; i++) {
                float t = i / 12f;
                Vector2 pos = Vector2.Lerp(Player.Center, targetPos, t);
                Vector2 vel = direction.RotatedBy(Main.rand.NextFloat(-0.2f, 0.2f)) * Main.rand.NextFloat(2f, 5f);
                PRTLoader.NewParticle<PRT_Spark>(pos + Main.rand.NextVector2Circular(10f, 10f), vel, godslayerCyan, Main.rand.NextFloat(1f, 1.8f)).Configure(false, Main.rand.Next(10, 20), Player);
            }
            for (int i = 0; i < 24; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(6f, 12f);
                PRTLoader.NewParticle<PRT_Light>(
                    Player.Center,
                    vel,
                    godslayerCyan,
                    0.4f
                ).Configure(Main.rand.Next(15, 25), opacity: 1f, squishStrenght: 1.8f, hueShift: 0f);
            }
        }
        private void SpawnTriggerTrailEffect() {
            if (VaultUtils.isServer) return;

            float progress = 1f - (triggerEffectTimer / 30f);
            Color trailColor = Color.Lerp(new Color(100, 255, 255), new Color(80, 180, 255), progress);

            for (int i = 0; i < 2; i++) {
                Vector2 pos = Player.Center + Main.rand.NextVector2Circular(20f, 20f);
                Vector2 vel = -Player.velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1f, 3f);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, trailColor * (1f - progress), Main.rand.NextFloat(0.8f, 1.2f)).Configure(false, Main.rand.Next(8, 15), Player);
            }
        }
        private void SpawnProtectionAura() {
            if (VaultUtils.isServer) return;

            float progress = 1f - (iFrameTimer / (float)GodslayerScabbard.IFrameTime);
            float radius = 35f + progress * 15f;
            float intensity = 1f - progress * 0.5f;

            Color godslayerBlue = new Color(80, 180, 255);
            Color godslayerPurple = new Color(160, 80, 255);
            float rotAngle = Main.GlobalTimeWrappedHourly * 4f;
            for (int i = 0; i < 3; i++) {
                float angle = rotAngle + MathHelper.TwoPi * i / 3f;
                Vector2 pos = Player.Center + angle.ToRotationVector2() * radius;
                Color color = Color.Lerp(godslayerBlue, godslayerPurple, (float)Math.Sin(angle + Main.GlobalTimeWrappedHourly * 2f) * 0.5f + 0.5f);

                int dust = Dust.NewDust(pos, 0, 0, DustID.Electric, 0, 0, 100, color, 1.2f * intensity);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 2f;
            }
            if (Main.rand.NextBool(3)) {
                Vector2 offset = Main.rand.NextVector2Circular(radius, radius);
                PRTLoader.NewParticle<PRT_Light>(
                    Player.Center + offset,
                    -offset.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1f, 2f),
                    godslayerBlue * intensity,
                    0.2f
                ).Configure(Main.rand.Next(10, 20), opacity: 0.8f, squishStrenght: 1.2f, hueShift: 0f);
            }
        }

        public override void PostUpdate() {
            if (EquipScabbard && DrawCharge > 0 && !DrawChargeReady) {
                float chargeRatio = DrawCharge / (float)GodslayerScabbard.MaxDrawCharge;
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
                if (chargeRatio > 0.7f && Main.GameUpdateCount % 8 == 0 && !VaultUtils.isServer) {
                    Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f);
                    PRTLoader.NewParticle<PRT_Light>(
                        Player.Center + Main.rand.NextVector2Circular(30f, 30f),
                        vel,
                        new Color(80, 180, 255) * chargeRatio,
                        0.15f
                    ).Configure(Main.rand.Next(10, 20), opacity: 0.6f, squishStrenght: 1f, hueShift: 0f);
                }
            }
        }
    }
}
