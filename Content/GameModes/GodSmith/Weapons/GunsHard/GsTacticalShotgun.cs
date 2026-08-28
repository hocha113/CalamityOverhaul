using CalamityOverhaul.Common;
using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsHard
{
    /// <summary>
    /// 战术霰弹枪重铸：战术节奏两态 + 破门冲击。材质：哑黑聚合物泵动霰弹枪。<br/>
    /// 签名行为：①压制三泵的首泵推出「破门冲击」气浪楔，撞开的敌人 1 秒内吃本枪弹丸 +10%
    /// ②战术扇面常亮散布锥标线，读出 6 粒覆盖面 ③滚烫弹壳与末泵三壳齐出收势。<br/>
    /// [战术扇面]：标线常亮；[压制三泵]：1.8 倍泵速连打 3 泵后强制 90 tick 泵闲。
    /// 原版 6 粒装药原样，每泵照常耗 1 发
    /// </summary>
    internal class GsTacticalShotgun : GsFireModeScheme
    {
        public override int TargetItemID => ItemID.TacticalShotgun;

        public override string GsFamily => "GunsHard";

        protected override string GsDescFallback =>
            "Reforged: right click to switch stance\n" +
            "Tactical Fan paints the spread cone so you know exactly what the six pellets cover\n" +
            "Triple Pump racks three fast shells then forces a rest; the first pump slams a breaching shockwave, and breached foes take 10% more from your pellets for a second";

        /// <summary>战术哑黑的高光橙</summary>
        internal static readonly Color BreachOrange = new(255, 150, 70);

        /// <summary>破门窗口：NPC 编号 → (类型, 截止帧)。owner 本地量，收益只在攻击方端结算</summary>
        private readonly uint[] breachUntil = new uint[Main.maxNPCs + 1];
        private readonly int[] breachType = new int[Main.maxNPCs + 1];

        public override GsFireMode[] Modes { get; } = [
            new GsFireMode {
                Key = "ModeFan", EnName = "Tactical Fan",
                AimLine = GsAimLineKind.Cone, AimConeHalfAngle = MathHelper.ToRadians(8f),
            },
            new GsFireMode {
                Key = "ModeTriplePump", EnName = "Triple Pump",
                UseSpeed = 1.80f, DamageMul = 1.10f,
                BurstCount = 3, BurstRest = 90,
            },
        ];

        //霰弹后坐：重挫大抬，三泵档更沉
        protected override float RecoilShift => 5.5f;
        protected override float RecoilKick => 0.07f;
        protected override float RecoilScale(Item item, Player player, GsFireMode mode)
            => mode.BurstCount > 0 ? 1.25f : 1f;

        protected override bool? GsGunShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback,
            GsFireMode mode, GsGunsHardPlayer mp) {
            Vector2 unit = velocity.SafeNormalize(Vector2.UnitX * player.direction);
            //破门冲击：三泵档首泵推出气浪楔（owner 端生成，全端可见）
            if (mp.ModeIndex == 1 && mp.BurstShots == 0) {
                Projectile.NewProjectile(source, position + unit * 20f, unit,
                    ModContent.ProjectileType<GsTacticalShotgunBreachProj>(),
                    Math.Max(1, (int)(damage * 0.40f)), knockback * 2f, player.whoAmI);
            }
            if (VaultUtils.isServer) {
                return null;
            }
            //每泵抛一枚滚烫弹壳
            PRTLoader.NewParticle<PRT_ProcChip>(position - unit * 4f,
                unit.RotatedBy(-MathHelper.PiOver2 * player.direction) * Main.rand.NextFloat(1.2f, 2f)
                    - Vector2.UnitY * Main.rand.NextFloat(2f, 3.5f),
                new Color(196, 92, 60), Main.rand.NextFloat(0.5f, 0.7f))
                ?.Configure(new Color(255, 170, 110), Main.rand.Next(20, 32));
            //三泵档末泵：泵闲哨响 + 三壳齐出的收势演出
            if (mp.ModeIndex == 1 && mp.BurstShots == mode.BurstCount - 1) {
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.7f, Pitch = -0.5f }, position);
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_Smoke>(position + unit * Main.rand.NextFloat(6f, 16f),
                        unit * 1.4f - Vector2.UnitY * 0.5f, new Color(110, 104, 94),
                        Main.rand.NextFloat(0.4f, 0.6f))?.Configure(Main.rand.Next(20, 30), 0.45f, 0.02f);
                }
            }
            return null;
        }

        /// <summary>攻击方端：气浪楔命中登记破门窗口</summary>
        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            if (proj.type != ModContent.ProjectileType<GsTacticalShotgunBreachProj>()
                || target.friendly || target.whoAmI >= breachUntil.Length) {
                return;
            }
            breachUntil[target.whoAmI] = Main.GameUpdateCount + 60;
            breachType[target.whoAmI] = target.type;
        }

        public override void GsProjModifyHitNPC(Projectile proj, NPC target,
            ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            //弹丸打门破目标 +10%（气浪楔自身不吃，防自乘）
            if (proj.type == ModContent.ProjectileType<GsTacticalShotgunBreachProj>()
                || target.whoAmI >= breachUntil.Length) {
                return;
            }
            if (breachUntil[target.whoAmI] > Main.GameUpdateCount
                && breachType[target.whoAmI] == target.type) {
                modifiers.FinalDamage *= 1.10f;
            }
        }

        internal override void GsGunHeldReset(Player player) {
            Array.Clear(breachUntil);
            Array.Clear(breachType);
        }
    }

    /// <summary>
    /// 破门冲击气浪楔：枪口前推的短程扇形冲击（ai 无参，方向取生成速度）。
    /// 判定 = 楔形（距离 + 夹角），击退沿出手向外撞。
    /// 自绘压扁震环 + 白炽气浪核，6 帧伤害窗后余散
    /// </summary>
    internal class GsTacticalShotgunBreachProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithGunsHard";

        /// <summary>楔形有效距离</summary>
        private const float Reach = 150f;
        /// <summary>楔形半角</summary>
        private const float HalfAngle = 0.42f;
        private const int LifeFrames = 16;
        private const int DamageFrames = 6;

        private int Elapsed => LifeFrames - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => Elapsed < DamageFrames ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 to = targetHitbox.Center.ToVector2() - Projectile.Center;
            float dist = to.Length();
            if (dist > Reach + Math.Max(targetHitbox.Width, targetHitbox.Height) * 0.5f) {
                return false;
            }
            //贴脸段不查角度，远段查楔形夹角
            if (dist < 40f) {
                return true;
            }
            float aim = Projectile.velocity.ToRotation();
            return MathF.Abs(MathHelper.WrapAngle(to.ToRotation() - aim)) <= HalfAngle;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = target.Center.X >= Projectile.Center.X ? 1 : -1;

        public override void AI() {
            if (Elapsed == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.55f, Pitch = 0.5f }, Projectile.Center);
                //气浪扬尘沿楔形铺开
                Vector2 aim = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                for (int i = 0; i < 6; i++) {
                    Vector2 dir = aim.RotatedBy(Main.rand.NextFloat(-HalfAngle, HalfAngle));
                    PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center + dir * Main.rand.NextFloat(20f, 60f),
                        dir * Main.rand.NextFloat(2.5f, 5f), new Color(150, 140, 126),
                        Main.rand.NextFloat(0.35f, 0.55f))?.Configure(Main.rand.Next(14, 22), 0.5f, 0.03f);
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + dir * Main.rand.NextFloat(16f, 40f),
                        dir * Main.rand.NextFloat(3f, 6f), GsTacticalShotgun.BreachOrange,
                        Main.rand.NextFloat(0.24f, 0.4f))?.Configure(true, Main.rand.Next(10, 16));
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //压扁震环沿出手向前推，白亮转橙余散
            float t = Elapsed / (float)LifeFrames;
            float push = MathHelper.Lerp(20f, Reach * 0.8f, 1f - (1f - t) * (1f - t));
            Vector2 core = Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.UnitX) * push;
            float radius = MathHelper.Lerp(16f, 62f, t);
            ShockRingDraw.Draw(Main.spriteBatch, core, radius, 9f - 5f * t,
                Color.White, GsTacticalShotgun.BreachOrange, new Color(120, 70, 40),
                (1f - t) * 0.7f, innerGlow: 0.28f,
                timeSeed: Projectile.identity * 0.43f);
            return false;
        }
    }
}
