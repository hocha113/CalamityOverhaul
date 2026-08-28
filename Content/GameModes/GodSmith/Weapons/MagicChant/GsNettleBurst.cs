using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant
{
    /// <summary>
    /// 荨麻刺重铸：生根荨麻。材质身份：丛林荨麻藤（带倒刺的翠绿藤蔓）。<br/>
    /// ①「生根」：正拍藤刺命中点生根滞留荨麻丛（多跳低伤驻场，场上至多三丛，超限枯最旧）；<br/>
    /// ②满层强化「藤龙横扫」：一条大藤龙蜿蜒横扫沿途撕咬；<br/>
    /// ③命中挂中毒加长；④施法有前刺推压体感（与晶邪碎片的推压参数不同：过冲更深、回挑收势）
    /// </summary>
    internal class GsNettleBurst : GsChantScheme
    {
        public override int TargetItemID => ItemID.NettleBurst;

        protected override string GsDescFallback =>
            "Reforged: on-beat vines take root where they bite, leaving stinging nettle thickets (up to three)" +
            "\nAt full resonance the next cast unleashes a great vine dragon that sweeps through the field";

        protected override float BaseDamageMult => 1.08f;

        protected override Color ChantColor => NettleGreen;

        internal static readonly Color NettleBright = new(196, 240, 120);
        internal static readonly Color NettleGreen = new(110, 190, 60);
        internal static readonly Color NettleDeep = new(44, 104, 30);

        /// <summary>owner 端在场荨麻丛上限</summary>
        private const int MaxThickets = 3;

        /// <summary>本弹是否荨麻藤延展段（右延/左延/收尾）</summary>
        private static bool IsVineSegment(int projType)
            => projType is ProjectileID.NettleBurstRight or ProjectileID.NettleBurstLeft or ProjectileID.NettleBurstEnd;

        //==================== 动画法：前刺推压（深过冲变体） ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //前刺推压：过冲曲线（progress 平方）比晶邪碎片压得更深，收势带回挑（确定性输入，各端一致）
            float progress = player.itemAnimation / (float)player.itemAnimationMax;
            float shove = progress * progress;
            player.itemLocation += new Vector2(player.direction, 0f) * (6f * shove);
            player.itemRotation -= player.direction * 0.09f * shove;
        }

        //==================== 强化咏唱：藤龙横扫 ====================

        protected override bool? ChantEmpowerShoot(Item item, Player player, GsChantPlayer chant,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback) {
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.9f, Pitch = -0.35f }, position);
            int dragonDamage = Math.Max(1, (int)(damage * 1.5f));
            Projectile.NewProjectile(source, position, velocity.SafeNormalize(Vector2.UnitX) * 8.5f,
                ModContent.ProjectileType<GsNettleBurstDragonProj>(), dragonDamage, knockback, player.whoAmI);
            return false;
        }

        //==================== 飞行相：孢绿荧尘 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (VaultUtils.isServer || !IsVineSegment(proj.type)) {
                return;
            }
            Lighting.AddLight(proj.Center, NettleGreen.ToVector3() * 0.15f);
            //藤段荧尘：正拍藤的倒刺间渗出孢绿微光
            bool hot = router.MarkData is FormOnBeat or FormEmpower;
            if (proj.timeLeft % (hot ? 6 : 10) == 0 && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_Light>(proj.Center + Main.rand.NextVector2Circular(8f, 8f),
                    Main.rand.NextVector2Circular(0.4f, 0.4f), NettleBright,
                    Main.rand.NextFloat(0.05f, 0.09f))?.Configure(Main.rand.Next(10, 18), 0.6f);
            }
        }

        //==================== 命中：生根荨麻 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (!IsVineSegment(proj.type)) {
                return;
            }
            if (!VaultUtils.isServer) {
                //命中反馈：棘叶迸散
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_ToxicMist>(target.Center + Main.rand.NextVector2Circular(6f, 6f),
                        Main.rand.NextVector2Circular(1.2f, 1.2f), NettleGreen * 0.55f,
                        Main.rand.NextFloat(0.35f, 0.55f))?.Configure(Main.rand.Next(10, 16));
                }
            }
            if (!proj.IsOwnedByLocalPlayer()) {
                return;
            }
            bool onBeat = router.MarkData is FormOnBeat or FormEmpower;
            //中毒加长：正拍咬得更毒
            target.AddBuff(BuffID.Poisoned, onBeat ? 240 : 120);
            if (!onBeat) {
                return;
            }
            //生根：命中点滞留荨麻丛（12t 节流防一串藤段瞬间铺满）
            GsChantPlayer chant = Chant(Main.player[proj.owner]);
            uint now = Main.GameUpdateCount;
            if (chant.TimerB != 0 && now - chant.TimerB < 12) {
                return;
            }
            chant.TimerB = now;
            RootThicket(proj, target.Center);
        }

        /// <summary>生根荨麻丛：owner 端在场上限三丛，超限枯最旧</summary>
        private static void RootThicket(Projectile proj, Vector2 pos) {
            int thicketType = ModContent.ProjectileType<GsNettleBurstThicketProj>();
            int count = 0, oldestIdx = -1, oldestLeft = int.MaxValue;
            foreach (Projectile other in Main.ActiveProjectiles) {
                if (other.type != thicketType || other.owner != proj.owner) {
                    continue;
                }
                count++;
                if (other.timeLeft < oldestLeft) {
                    oldestLeft = other.timeLeft;
                    oldestIdx = other.whoAmI;
                }
            }
            if (count >= MaxThickets && oldestIdx >= 0) {
                Main.projectile[oldestIdx].Kill();
            }
            int thicketDamage = Math.Max(1, (int)(proj.damage * 0.2f));
            Projectile.NewProjectile(proj.GetSource_FromThis(), pos, Vector2.Zero,
                thicketType, thicketDamage, 0f, proj.owner);
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            //余痕相：藤段枯散的孢尘比藤活得久
            if (VaultUtils.isServer || !IsVineSegment(proj.type) || !Main.rand.NextBool(2)) {
                return;
            }
            PRTLoader.NewParticle<PRT_ToxicMist>(proj.Center, -Vector2.UnitY * 0.3f,
                NettleGreen * 0.45f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(14, 24));
        }
    }

    /// <summary>
    /// 荨麻丛：生根滞留的带刺灌丛，多跳低伤（判定圆与可见丛团同源）；
    /// 自绘三层呼吸丛影 + 倒刺閃光，孢雾缓升
    /// </summary>
    internal class GsNettleBurstThicketProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithMagicChant";

        private const int LifeTicks = 180;
        private const float Radius = 44f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 56;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = LifeTicks;
        }

        /// <summary>破土-繁茂-枯萎的半径生命周期</summary>
        private float RadiusNow {
            get {
                float grow = MathHelper.Clamp((LifeTicks - Projectile.timeLeft) / 12f, 0f, 1f);
                float fade = MathHelper.Clamp(Projectile.timeLeft / 26f, 0f, 1f);
                return Radius * VaultUtils.EaseOutQuad(grow) * fade;
            }
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            if (VaultUtils.isServer) {
                return;
            }
            //丛间孢雾缓升
            if (Projectile.timeLeft % 6 == 0) {
                float r = RadiusNow;
                PRTLoader.NewParticle<PRT_ToxicMist>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-r, r) * 0.8f, Main.rand.NextFloat(-6f, 10f)),
                    new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(0.4f, 0.9f)),
                    GsNettleBurst.NettleGreen * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(14, 22));
            }
            Lighting.AddLight(Projectile.Center, GsNettleBurst.NettleGreen.ToVector3() * 0.25f * (RadiusNow / Radius));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Poisoned, 120);

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float r = RadiusNow;
            if (r < 6f) {
                return false;
            }
            float nx = MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right);
            float ny = MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom);
            return new Vector2(nx - Projectile.Center.X, ny - Projectile.Center.Y).LengthSquared() <= r * r;
        }

        public override bool PreDraw(ref Color lightColor) {
            //三层呼吸丛影（A=0 加色批）+ 沿缘倒刺星芒，identity 定相错开
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            float r = RadiusNow;
            if (r < 4f) {
                return false;
            }
            Vector2 basePos = Projectile.Center - Main.screenPosition;
            for (int i = 0; i < 3; i++) {
                float phase = Main.GlobalTimeWrappedHourly * 2.8f + Projectile.identity * 0.57f + i * 2.1f;
                Vector2 off = new(MathF.Sin(phase) * r * 0.24f, -MathF.Abs(MathF.Cos(phase * 0.8f)) * 4f);
                float s = r / glow.Width * (2.0f - i * 0.42f);
                Color c = (i == 2 ? GsNettleBurst.NettleBright : i == 1 ? GsNettleBurst.NettleGreen : GsNettleBurst.NettleDeep) with { A = 0 };
                Main.EntitySpriteDraw(glow, basePos + off, null, c * (0.3f + i * 0.09f), 0f,
                    glow.Size() / 2f, new Vector2(s, s * 0.8f), SpriteEffects.None, 0);
            }
            //倒刺星芒：五根尖刺沿丛缘定相摆动
            for (int i = 0; i < 5; i++) {
                float ang = MathHelper.TwoPi * i / 5f + MathF.Sin(Main.GlobalTimeWrappedHourly * 1.7f + Projectile.identity * 0.9f + i) * 0.2f;
                Vector2 tip = basePos + ang.ToRotationVector2() * r * 0.72f;
                Main.EntitySpriteDraw(star, tip, null, GsNettleBurst.NettleBright with { A = 0 } * 0.5f,
                    ang, star.Size() / 2f, 0.1f, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 藤龙：强化咏唱放出的巨型荨麻藤龙。owner 端每 30t 锁定近敌写 ai[1] 过线，
    /// 各端向同一目标缓转横扫；身躯自绘（拖尾史分节，原版藤段贴图作节体 + 绿棘辉光）
    /// </summary>
    internal class GsNettleBurstDragonProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithMagicChant";

        private ref float SteerTimer => ref Projectile.localAI[0];
        private int TargetWho => (int)Projectile.ai[1] - 1;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 18;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 34;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 200;
        }

        public override void AI() {
            //蜿蜒横扫：基速上叠正弦摆身（identity 定相，各端确定性）
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.velocity = Projectile.velocity.RotatedBy(
                MathF.Sin(Projectile.timeLeft * 0.18f + Projectile.identity * 1.1f) * 0.05f);

            //owner 端周期性锁定近敌，目标经 ai[1] 过线，各端向同一目标缓转
            if (Projectile.IsOwnedByLocalPlayer() && ++SteerTimer >= 30f) {
                SteerTimer = 0f;
                NPC next = Projectile.Center.FindClosestNPC(540f);
                int encoded = next != null ? next.whoAmI + 1 : 0;
                if ((int)Projectile.ai[1] != encoded) {
                    Projectile.ai[1] = encoded;
                    Projectile.netUpdate = true;
                }
            }
            if (TargetWho >= 0 && TargetWho < Main.maxNPCs) {
                NPC chase = Main.npc[TargetWho];
                if (chase.active && chase.CanBeChasedBy()) {
                    float current = Projectile.velocity.ToRotation();
                    float wanted = (chase.Center - Projectile.Center).ToRotation();
                    Projectile.velocity = Utils.AngleTowards(current, wanted, MathHelper.ToRadians(2.2f))
                        .ToRotationVector2() * Projectile.velocity.Length();
                }
            }

            if (!VaultUtils.isServer) {
                //龙息孢雾：头部两侧甩出的绿尘
                if (Projectile.timeLeft % 3 == 0) {
                    Vector2 side = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2)
                        * MathF.Sin(Projectile.timeLeft * 0.55f) * 7f;
                    PRTLoader.NewParticle<PRT_ToxicMist>(Projectile.Center + side,
                        -Projectile.velocity * 0.1f, GsNettleBurst.NettleGreen * 0.55f,
                        Main.rand.NextFloat(0.35f, 0.55f))?.Configure(Main.rand.Next(12, 18));
                }
                Lighting.AddLight(Projectile.Center, GsNettleBurst.NettleGreen.ToVector3() * 0.5f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Poisoned, 360);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit32 with { Volume = 0.5f, Pitch = 0.2f, MaxInstances = 3 }, target.Center);
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_ToxicMist>(target.Center + Main.rand.NextVector2Circular(10f, 10f),
                        Main.rand.NextVector2Circular(2f, 2f), GsNettleBurst.NettleGreen * 0.6f,
                        Main.rand.NextFloat(0.45f, 0.7f))?.Configure(Main.rand.Next(12, 20));
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.8f, Pitch = -0.3f }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_ToxicMist>(Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                    Main.rand.NextVector2Circular(2.5f, 2.5f), GsNettleBurst.NettleGreen * 0.55f,
                    Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(16, 26));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //龙身自绘：拖尾史取节，原版藤段贴图作节体 + 绿棘辉光（A=0 加色），identity 定相脉动
            int vineType = ProjectileID.NettleBurstRight;
            Main.instance.LoadProjectile(vineType);
            Texture2D tex = TextureAssets.Projectile[vineType].Value;
            Rectangle frame = tex.Frame(1, Main.projFrames[vineType], 0, 0);
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                Vector2 pos = Projectile.oldPos[i];
                if (pos == Vector2.Zero) {
                    continue;
                }
                pos += Projectile.Size / 2f;
                float shrink = 1.2f - i * 0.05f;
                float pulse = 0.82f + 0.18f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + Projectile.identity * 0.83f + i * 0.7f);
                Color glow = GsNettleBurst.NettleGreen with { A = 0 } * (0.5f * shrink * pulse);
                float rot = Projectile.oldRot[i] + MathHelper.PiOver2;
                Main.EntitySpriteDraw(tex, pos - Main.screenPosition, frame, glow, rot,
                    frame.Size() / 2f, shrink * 1.3f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, pos - Main.screenPosition, frame, Color.White with { A = 50 } * (0.55f * shrink), rot,
                    frame.Size() / 2f, shrink * 0.9f, SpriteEffects.None, 0);
            }
            //龙首绿芒
            Texture2D star = CWRAsset.StarTexture.Value;
            Main.EntitySpriteDraw(star, Projectile.Center - Main.screenPosition, null,
                GsNettleBurst.NettleBright with { A = 0 } * 0.85f, Projectile.rotation,
                star.Size() / 2f, 0.32f, SpriteEffects.None, 0);
            return false;
        }
    }
}
