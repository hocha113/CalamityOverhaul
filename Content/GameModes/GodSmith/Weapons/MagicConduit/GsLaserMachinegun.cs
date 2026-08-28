using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit
{
    /// <summary>
    /// 激光机枪重铸：转速引擎（Sustain 政策注释点名本枪）。材质身份：过热红束。<br/>
    /// ①热量=转速：射速随热段爬坡（低热迟滞 → 白热全速）；<br/>
    /// ②白热「穿透红束」：每第 6 发追加一道贯穿红色重束；<br/>
    /// ③临界维持蓝耗 ×1.5；④泄压「清膛扫射」：1.2 秒扇形 30 连速射清膛；
    /// ⑤施法有随热量增幅的持续后坐抖动
    /// </summary>
    internal class GsLaserMachinegun : GsHeatScheme
    {
        public override int TargetItemID => ItemID.LaserMachinegun;

        protected override string GsDescFallback =>
            "Reforged: heat is spin; the barrel drags cold and roars at white heat, where every sixth shot rides a piercing crimson lance" +
            "\nHolding the gauge at the redline only surges mana upkeep" +
            "\nRight click to vent the whole cylinder as a sweeping thirty-round fan";

        internal override float HeatPerShot => 2.2f;
        internal override float CoolRatePerTick => 0.9f;
        internal override GsOverloadPolicy OverloadPolicy => GsOverloadPolicy.Sustain;
        internal override Color MuzzleTheme => GsConduitVFX.ForgeMain;

        internal static readonly Color LaserRed = new(255, 80, 70);
        internal static readonly Color LaserBright = new(255, 170, 150);

        /// <summary>白热带第 N 发升格重束（owner 端射击链计数，方案单例但只在 myPlayer 路径消费）</summary>
        private int whiteHotShotCounter;

        //==================== 转速：射速随热段 ====================

        public override float GsUseSpeedMultiplier(Item item, Player player) {
            //Sustain 政策不进过热锁，base 的锁减速永不生效，转速曲线自管：
            //低热 0.85 → 白热 1.15（热量是 owner 本地量，远端按基准速演绎，只影响远端观感）
            GsHeatPlayer hp = player.GetModPlayer<GsHeatPlayer>();
            if (hp.BoundItemType != TargetItemID) {
                return 0.85f;
            }
            return MathHelper.Lerp(0.85f, 1.15f, MathHelper.Clamp(hp.Heat / GsHeatPlayer.SoftBandLow, 0f, 1f));
        }

        internal override float ExtraManaCostMult(Player player, GsHeatPlayer hp)
            => hp.BoundItemType == TargetItemID && hp.Heat >= GsHeatPlayer.HeatMax ? 1.5f : 1f;

        //==================== 动画法：后坐抖动随热量 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //持续后坐：itemAnimation 定相抖动打底（各端一致），owner 端叠热量增幅（个人体感）
            float heatBoost = 1f;
            if (player.whoAmI == Main.myPlayer) {
                GsHeatPlayer hp = player.GetModPlayer<GsHeatPlayer>();
                if (hp.BoundItemType == TargetItemID) {
                    heatBoost = 1f + hp.Heat / GsHeatPlayer.HeatMax;
                }
            }
            float jitter = MathF.Sin(player.itemAnimation * 2.6f) * 1.1f * heatBoost;
            player.itemLocation -= new Vector2(player.direction * (1f + 0.6f * heatBoost) * 0.8f, jitter * 0.6f);
            player.itemRotation += player.direction * jitter * 0.02f;
        }

        //==================== 白热重束 ====================

        public override bool? GsShoot(Item item, Player player, Terraria.DataStructures.EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //先走基类积热，再在白热带计数升格（射击链只在 owner 端执行）
            bool? result = base.GsShoot(item, player, source, position, velocity, type, damage, knockback);
            if (LocalWhiteHot(player) && ++whiteHotShotCounter >= 6) {
                whiteHotShotCounter = 0;
                int lanceDamage = Math.Max(1, (int)(damage * 0.6f));
                Projectile.NewProjectile(source, position, velocity.SafeNormalize(Vector2.UnitX) * 20f,
                    ModContent.ProjectileType<GsLaserMachinegunLanceProj>(), lanceDamage, knockback, player.whoAmI);
                SoundEngine.PlaySound(SoundID.Item33 with { Volume = 0.6f, Pitch = 0.4f, MaxInstances = 4 }, player.Center);
            }
            return result;
        }

        //==================== 飞行相：机枪激光的炽尾 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.LaserMachinegunLaser || VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(proj.Center, LaserRed.ToVector3() * 0.2f);
            //白热出生的激光更炽：尾迹火星
            if (router.MarkData >= 1f && proj.timeLeft % 4 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(proj.Center - proj.velocity * 0.4f,
                    -proj.velocity * 0.05f, LaserBright, Main.rand.NextFloat(0.14f, 0.24f))
                    ?.Configure(false, Main.rand.Next(6, 10));
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.LaserMachinegunLaser || VaultUtils.isServer) {
                return;
            }
            //命中反馈：红热溅点
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(5f, 5f),
                    Main.rand.NextVector2Circular(1.8f, 1.8f), LaserRed,
                    Main.rand.NextFloat(0.18f, 0.3f))?.Configure(true, Main.rand.Next(8, 12));
            }
        }

        //==================== 泄压：清膛扫射 ====================

        internal override void FireVent(Player player, GsHeatPlayer hp) {
            //清膛主控自扇摆速射（伤害烘焙 ×0.35~0.6 随转速），枪口方向随生成时瞄准
            float frac = hp.Heat / GsHeatPlayer.HeatMax;
            int damage = Math.Max(1, (int)(player.GetWeaponDamage(player.HeldItem) * (0.35f + 0.25f * frac)));
            Projectile.NewProjectile(player.GetSource_Misc("GsConduitVent"), player.MountedCenter,
                GsAimUnit(player), ModContent.ProjectileType<GsMachinegunVentProj>(), damage, 1.6f, player.whoAmI);
        }
    }

    /// <summary>
    /// 穿透红束：白热带每第 6 发升格的贯穿重束。
    /// 高速直线贯穿（穿 4），束体自绘（速度向拉伸三层线束 + 首芒），identity 定相闪烁
    /// </summary>
    internal class GsLaserMachinegunLanceProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithMagicConduit";

        private const float TailLength = 110f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 4;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.timeLeft = 60;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(Projectile.Center, GsLaserMachinegun.LaserRed.ToVector3() * 0.4f);
            if (Projectile.timeLeft % 3 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center - Projectile.velocity * 0.5f,
                    Main.rand.NextVector2Circular(0.5f, 0.5f), GsLaserMachinegun.LaserRed,
                    Main.rand.NextFloat(0.16f, 0.26f))?.Configure(false, Main.rand.Next(6, 10));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.35f, Pitch = 0.5f, MaxInstances = 4 }, target.Center);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(6f, 6f),
                    Main.rand.NextVector2Circular(2.4f, 2.4f), GsLaserMachinegun.LaserBright,
                    Main.rand.NextFloat(0.22f, 0.36f))?.Configure(true, Main.rand.Next(10, 16));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //束体：速度向拉伸三层线束（外红/中亮/白芯）+ 首芒（A=0 加色，identity 定相闪烁）
            float flick = 1f + 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * 47f + Projectile.identity * 0.71f);
            Vector2 tail = Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.UnitX) * TailLength;
            GsConduitVFX.DrawBeam(Main.spriteBatch, tail, Projectile.rotation, TailLength,
                9f * flick, GsLaserMachinegun.LaserRed, GsLaserMachinegun.LaserBright);
            Texture2D star = CWRAsset.StarTexture.Value;
            Main.EntitySpriteDraw(star, Projectile.Center - Main.screenPosition, null,
                Color.White with { A = 0 } * 0.8f, Projectile.rotation, star.Size() / 2f,
                new Vector2(0.3f, 0.12f) * flick, SpriteEffects.None, 0);
            return false;
        }
    }
}
