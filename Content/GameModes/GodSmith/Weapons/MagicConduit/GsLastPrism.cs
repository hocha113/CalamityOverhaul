using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit
{
    /// <summary>
    /// 最后棱镜重铸（A 档）。材质身份：棱晶聚光（七彩折射的白炽核束）。<br/>
    /// ①热量即聚焦度：六股彩束引导中逐渐收拢，白热合为一根白炽主束并在落点折出棱光碎束；
    /// ②顶格走 NoBreak：聚束永不断，只涨蓝耗（经典不毁）；
    /// ③右键泄压「棱光崩解」（需引导中）：主束炸解为七瓣棱光扇，威力随热量走，清热重新聚焦。<br/>
    /// 数值包络：聚束单目标约原版 110%，散束单束偏低但可多目标分摊；泄压收益计入预算故基伤不加成
    /// </summary>
    internal class GsLastPrism : GsHeatScheme
    {
        public override int TargetItemID => ItemID.LastPrism;

        protected override string GsDescFallback =>
            "Reforged: heat is focus; six splayed beams converge into one white-hot lance, and at white heat the impact refracts prism shards" +
            "\nCapping the gauge never breaks the beam, it only surges mana upkeep\nRight click while channeling to shatter the lance into a prismatic fan, spending all heat";

        internal override float HeatPerShot => 0f;
        internal override float CoolRatePerTick => 1.4f;
        internal override GsOverloadPolicy OverloadPolicy => GsOverloadPolicy.NoBreak;
        internal override float VentMinHeat => 40f;
        internal override Color MuzzleTheme => new(214, 190, 255);

        public override bool? GsCanUseItem(Item item, Player player) {
            if (base.GsCanUseItem(item, player) == false) {
                return false;
            }
            if (HeldAlive<GsLastPrismHeldProj>(player)) {
                return false;
            }
            return null;
        }

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.whoAmI == Main.myPlayer && !HeldAlive<GsLastPrismHeldProj>(player)) {
                Projectile.NewProjectile(source, player.MountedCenter, GsAimUnit(player),
                    ModContent.ProjectileType<GsLastPrismHeldProj>(), damage, knockback, player.whoAmI);
            }
            return false;
        }

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            //出生热量全值过线：各端由它 + 引导帧数确定性重建聚焦度（基类只写热段，本族要连续值）
            Player player = Main.player[proj.owner];
            router.MarkData = player.GetModPlayer<GsHeatPlayer>().Heat;
        }

        /// <summary>泄压前置：必须正在引导（棱光崩解是主束的炸解，无束可炸不成立）</summary>
        internal override bool VentReady(Player player, GsHeatPlayer hp) => HeldAlive<GsLastPrismHeldProj>(player);

        internal override void FireVent(Player player, GsHeatPlayer hp) {
            //棱光崩解：主束炸解为七瓣棱光扇，威力随热量走；随后折束收场，重新聚焦
            float power = 1.1f + 1.6f * hp.Heat / GsHeatPlayer.HeatMax;
            int damage = Math.Max(1, (int)(player.GetWeaponDamage(player.HeldItem) * power));
            Vector2 aim = GsAimUnit(player);
            Projectile.NewProjectile(player.GetSource_Misc("GsConduitVent"), player.MountedCenter, Vector2.Zero,
                ModContent.ProjectileType<GsLastPrismBurstProj>(), damage, 7f, player.whoAmI, aim.ToRotation());
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == player.whoAmI && proj.ModProjectile is GsLastPrismHeldProj held) {
                    held.RequestCollapse();
                }
            }
        }

        internal override void OnHeatCapped(Player player, GsHeatPlayer hp) {
            //顶格白炽定音：聚焦到底的一声棱鸣 + 环闪（owner 本地读数）
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.85f, Pitch = 0.5f }, player.Center);
            PRTLoader.NewParticle<PRT_ProcRing>(player.MountedCenter + GsAimUnit(player) * 20f,
                Vector2.Zero, Color.White, 1f)?.Configure(26f, 6f, 12);
        }
    }

    /// <summary>
    /// 棱光崩解：七瓣棱光扇自枪口展开扫过前方。ai[0]=扇轴朝向（生成时定死随包过线），
    /// 每瓣一色短束，半径 30→340 展开；每目标只结算一次
    /// </summary>
    internal class GsLastPrismBurstProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithMagicConduit";

        private const int LifeTicks = 26;
        private const int GrowTicks = 22;
        private const int PetalCount = 7;
        /// <summary>扇半张角</summary>
        private const float HalfFan = 0.85f;
        private const float MaxRadius = 340f;

        private float AxisRot => Projectile.ai[0];

        private float Progress => MathHelper.Clamp((LifeTicks - Projectile.timeLeft) / (float)GrowTicks, 0f, 1f);

        private float RadiusNow => 30f + (MaxRadius - 30f) * VaultUtils.EaseOutCubic(Progress);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = LifeTicks;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            if (Projectile.timeLeft == LifeTicks - 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.9f, Pitch = 0.2f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.7f, Pitch = -0.3f }, Projectile.Center);
                //出手相：崩解瞬间的棱尘环
                for (int i = 0; i < 14; i++) {
                    float ang = AxisRot + Main.rand.NextFloat(-HalfFan, HalfFan);
                    PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center + ang.ToRotationVector2() * 18f,
                        ang.ToRotationVector2() * Main.rand.NextFloat(3f, 9f),
                        GsLastPrismHeldProj.HueOf(i % 6), Main.rand.NextFloat(0.35f, 0.6f))
                        ?.Configure(Color.White, Main.rand.Next(12, 20), 0.08f, 0.85f);
                }
            }
            //扇缘棱屑（余痕相在扇缘滞留）
            if (!VaultUtils.isServer && Projectile.timeLeft % 2 == 0) {
                float ang = AxisRot + Main.rand.NextFloat(-HalfFan, HalfFan);
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center + ang.ToRotationVector2() * RadiusNow,
                    ang.ToRotationVector2() * 0.8f, GsLastPrismHeldProj.HueOf(Main.rand.Next(6)),
                    Main.rand.NextFloat(0.08f, 0.13f))?.Configure(Main.rand.Next(14, 24), 0.7f);
            }
            Lighting.AddLight(Projectile.Center, Color.White.ToVector3() * 0.5f * (1f - Progress * 0.6f));
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //扇形判定：距离入界 + 夹角入扇（几何与七瓣可见扇同源）
            Vector2 to = targetHitbox.Center.ToVector2() - Projectile.Center;
            float dist = to.Length();
            if (dist > RadiusNow + 18f || dist < 8f) {
                return false;
            }
            float delta = MathHelper.WrapAngle(to.ToRotation() - AxisRot);
            return Math.Abs(delta) <= HalfFan + 0.12f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                    (AxisRot + Main.rand.NextFloat(-0.5f, 0.5f)).ToRotationVector2() * Main.rand.NextFloat(2.5f, 6f),
                    Main.rand.NextBool() ? Color.White : GsLastPrismHeldProj.HueOf(Main.rand.Next(6)),
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(10, 16));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //七瓣色束扇 + 白炽轴瓣：瓣长 = 当前半径，宽度随展开收窄（爆瞬宽、扫尾细）
            SpriteBatch sb = Main.spriteBatch;
            float fade = MathHelper.Clamp(Projectile.timeLeft / 8f, 0f, 1f);
            float radius = RadiusNow;
            float width = MathHelper.Lerp(26f, 10f, Progress);
            for (int i = 0; i < PetalCount; i++) {
                float lane = i / (PetalCount - 1f) * 2f - 1f;
                float rot = AxisRot + lane * HalfFan;
                Color hue = GsLastPrismHeldProj.HueOf(i % 6);
                GsConduitVFX.DrawBeam(sb, Projectile.Center, rot, radius, width, hue,
                    Color.Lerp(hue, Color.White, 0.6f), 0.8f * fade);
            }
            GsConduitVFX.DrawBeam(sb, Projectile.Center, AxisRot, radius * 1.06f, width * 0.8f,
                Color.White, Color.White, 0.7f * fade);
            //扇心白闪
            Texture2D glow = CWRAsset.SoftGlow.Value;
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null,
                Color.White with { A = 0 } * (0.85f * fade), 0f, glow.Size() / 2f,
                0.7f * (1f - Progress * 0.5f), SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>棱光碎束：白热落点折出的高速折射短束（ai[0]=谱色序）</summary>
    internal class GsLastPrismShardProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithMagicConduit";

        private Color Hue => GsLastPrismHeldProj.HueOf((int)Projectile.ai[0] % 6);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 7;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 2;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 16;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, Hue.ToVector3() * 0.3f);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Main.rand.NextVector2Circular(1.2f, 1.2f),
                    Hue, Main.rand.NextFloat(0.07f, 0.11f))?.Configure(Main.rand.Next(10, 18), 0.7f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //拖尾史拉出的折射细芒：色鞘 + 白芯
            Texture2D glow = CWRAsset.SoftGlow.Value;
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                Vector2 pos = Projectile.oldPos[i];
                if (pos == Vector2.Zero) {
                    continue;
                }
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                Main.EntitySpriteDraw(glow, pos + Projectile.Size / 2f - Main.screenPosition, null,
                    Hue with { A = 0 } * (0.5f * fade), Projectile.rotation,
                    glow.Size() / 2f, new Vector2(0.36f, 0.09f) * fade, SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null,
                Color.White with { A = 0 } * 0.8f, Projectile.rotation,
                glow.Size() / 2f, new Vector2(0.3f, 0.07f), SpriteEffects.None, 0);
            return false;
        }
    }
}
