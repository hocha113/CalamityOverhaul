using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph
{
    /// <summary>
    /// 毒牙双杖共用模板（镜像宝石杖的模板/子类结构）。材质身份：淬毒獠牙。<br/>
    /// A 形态 rider：毒牙拖毒雾；毒牙咬中第二个目标时分裂细牙；<br/>
    /// B 形态（右键蓄力）「蛇吻」：放出蜿蜒毒蛇巨弹，双杖蛇形互异——
    /// 毒杖蛇小而疾、分裂细牙更多；剧毒杖蛇大而重、消亡处滞留酸池
    /// </summary>
    internal abstract class GsMorphFangScheme : GsMorphScheme
    {
        /// <summary>蛇形变体：0 中毒蛇（毒杖）/ 1 剧毒蛇（剧毒杖）</summary>
        protected abstract int FangVariant { get; }

        /// <summary>A rider：第二目标分裂的细牙枚数</summary>
        protected abstract int SplitFangCount { get; }

        /// <summary>本杖主题色（亮/主/深）</summary>
        internal abstract Color FangBright { get; }
        internal abstract Color FangMain { get; }
        internal abstract Color FangDeep { get; }

        protected sealed override Color ChargeColor => FangMain;
        protected override float BaseDamageMult => 1.06f;

        /// <summary>本杖原版毒牙弹类型</summary>
        protected int FangType => ContentSamples.ItemsByType[TargetItemID].shoot;

        //==================== 动画法：举杖压腕 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //蛇杖压腕：出手瞬间杖头向下咬合 3px，随动画进度回抬（绝对剖面 −0.09·p 下压，差分施加防累积漂移）
            float n = player.itemAnimationMax;
            float progress = player.itemAnimation / n;
            player.itemLocation += new Vector2(player.direction * 1f, 3f) * progress;
            GsMagicKickMath.ApplyKickDiff(player, -0.09f * progress, -0.09f * ((player.itemAnimation + 1) / n));
        }

        //==================== B 形态：蛇吻 ====================

        protected sealed override void FireMorphB(Item item, Player player) {
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.9f, Pitch = -0.4f }, player.Center);
            SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.5f, Pitch = 0.2f }, player.Center);
            Vector2 aim = GsAimUnit(player);
            int damage = Math.Max(1, (int)(player.GetWeaponDamage(item) * 1.6f));
            float speed = FangVariant == 0 ? 9f : 6.5f;
            SpawnMorph(player, item, player.MountedCenter, aim * speed,
                ModContent.ProjectileType<GsMorphFangSerpentProj>(), damage, 5f,
                KindB, ai0: FangVariant);
        }

        //==================== A 形态 rider ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != FangType || VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(proj.Center, FangMain.ToVector3() * 0.14f);
            //毒牙尾雾：飞行沿途渗出毒雾
            if (proj.timeLeft % 6 == 0) {
                PRTLoader.NewParticle<PRT_ToxicMist>(proj.Center - proj.velocity * 0.4f,
                    -proj.velocity * 0.06f, FangMain * 0.5f,
                    Main.rand.NextFloat(0.28f, 0.45f))?.Configure(Main.rand.Next(10, 16));
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.type != FangType) {
                return;
            }
            if (!VaultUtils.isServer) {
                //命中反馈：毒沫迸溅
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_ToxicMist>(target.Center + Main.rand.NextVector2Circular(6f, 6f),
                        Main.rand.NextVector2Circular(1.4f, 1.4f), FangMain * 0.55f,
                        Main.rand.NextFloat(0.32f, 0.5f))?.Configure(Main.rand.Next(10, 16));
                }
            }
            if (!proj.IsOwnedByLocalPlayer()) {
                return;
            }
            OnFangBite(target);
            //咬中第二个目标：分裂细牙（0.3 倍，向两侧小扇甩出；numHits 此刻为 1）
            if (proj.numHits != 1 || router.MarkData == 10f) {
                return;
            }
            int splitDamage = Math.Max(1, (int)(proj.damage * 0.3f));
            Vector2 dir = proj.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < SplitFangCount; i++) {
                float off = MathHelper.Lerp(-0.5f, 0.5f, SplitFangCount == 1 ? 0.5f : i / (float)(SplitFangCount - 1));
                SpawnMorph(Main.player[proj.owner], Main.player[proj.owner].HeldItem, target.Center,
                    dir.RotatedBy(off) * 8f, FangType, splitDamage, proj.knockBack * 0.3f, 10);
                //细牙承 10 号形态标：不再二次分裂
            }
        }

        /// <summary>本杖专属咬合效果（owner 命中路径）</summary>
        protected abstract void OnFangBite(NPC target);

        protected sealed override void GsMorphOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            //细牙出生改制（owner 端，先于生成包发出）：收窄体型、限寿
            if (proj.type == FangType && router.MarkData == 10f) {
                proj.scale *= 0.62f;
                proj.timeLeft = Math.Min(proj.timeLeft, 60);
            }
        }
    }

    /// <summary>毒杖重铸：中毒蛇吻。蛇小而疾，细牙分裂更多</summary>
    internal class GsPoisonStaff : GsMorphFangScheme
    {
        public override int TargetItemID => ItemID.PoisonStaff;

        protected override string GsDescFallback =>
            "Reforged: fangs trail venom mist and split into thin fangs on their second bite" +
            "\nHold right click to charge the Serpent Kiss: a swift coiling viper that poisons everything on its path";

        protected override int FangVariant => 0;
        protected override int SplitFangCount => 2;
        protected override int ChargeTicksB => 45;
        protected override float ChargeManaMult => 1.8f;

        internal override Color FangBright => new(202, 255, 140);
        internal override Color FangMain => new(134, 214, 66);
        internal override Color FangDeep => new(52, 108, 28);

        protected override void OnFangBite(NPC target) => target.AddBuff(BuffID.Poisoned, 240);
    }

    /// <summary>剧毒杖重铸：剧毒蛇吻。蛇大而重，消亡处滞留酸池</summary>
    internal class GsVenomStaff : GsMorphFangScheme
    {
        public override int TargetItemID => ItemID.VenomStaff;

        protected override string GsDescFallback =>
            "Reforged: fangs trail venom mist and split a heavy fang on their second bite" +
            "\nHold right click to charge the Serpent Kiss: a massive pit viper whose corpse melts into a lingering acid pool";

        protected override int FangVariant => 1;
        protected override int SplitFangCount => 1;
        protected override int ChargeTicksB => 55;
        protected override float ChargeManaMult => 2.0f;

        internal override Color FangBright => new(236, 164, 255);
        internal override Color FangMain => new(186, 92, 224);
        internal override Color FangDeep => new(88, 30, 112);

        protected override void OnFangBite(NPC target) => target.AddBuff(BuffID.Venom, 240);
    }

    /// <summary>
    /// 蛇吻毒蛇巨弹：蜿蜒扑咬（identity 定相摆身 + owner 端 30t 锁靶经 ai[1] 过线）。
    /// ai[0] = 变体（0 中毒蛇小而疾 / 1 剧毒蛇大而重，消亡滞留酸池）。
    /// 蛇身自绘：拖尾史分节，原版毒牙贴图作节体 + 本色辉光
    /// </summary>
    internal class GsMorphFangSerpentProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithMagicMorph";

        private int Variant => (int)Projectile.ai[0];
        private int TargetWho => (int)Projectile.ai[1] - 1;
        private ref float SteerTimer => ref Projectile.localAI[0];

        private Color Bright => Variant == 0 ? new(202, 255, 140) : new(236, 164, 255);
        private Color Body => Variant == 0 ? new(134, 214, 66) : new(186, 92, 224);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 16;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
            Projectile.timeLeft = 190;
        }

        public override void AI() {
            //蛇形变体的体感差异：小蛇摆频高、大蛇摆幅重
            float sway = Variant == 0 ? 0.06f : 0.038f;
            float freq = Variant == 0 ? 0.22f : 0.14f;
            Projectile.velocity = Projectile.velocity.RotatedBy(
                MathF.Sin(Projectile.timeLeft * freq + Projectile.identity * 1.1f) * sway);
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.scale = Variant == 0 ? 0.9f : 1.25f;

            //owner 端周期锁靶经 ai[1] 过线，各端向同一目标缓转
            if (Projectile.IsOwnedByLocalPlayer() && ++SteerTimer >= 30f) {
                SteerTimer = 0f;
                NPC next = Projectile.Center.FindClosestNPC(520f);
                int encoded = next != null ? next.whoAmI + 1 : 0;
                if ((int)Projectile.ai[1] != encoded) {
                    Projectile.ai[1] = encoded;
                    Projectile.netUpdate = true;
                }
            }
            if (TargetWho >= 0 && TargetWho < Main.maxNPCs) {
                NPC chase = Main.npc[TargetWho];
                if (chase.active && chase.CanBeChasedBy()) {
                    float turn = Variant == 0 ? 2.6f : 1.8f;
                    float wanted = (chase.Center - Projectile.Center).ToRotation();
                    Projectile.velocity = Utils.AngleTowards(Projectile.velocity.ToRotation(), wanted,
                        MathHelper.ToRadians(turn)).ToRotationVector2() * Projectile.velocity.Length();
                }
            }

            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(Projectile.Center, Body.ToVector3() * 0.45f);
            //沿途毒雾：中毒蛇更密（签名：毒雾走廊）
            if (Projectile.timeLeft % (Variant == 0 ? 3 : 5) == 0) {
                PRTLoader.NewParticle<PRT_ToxicMist>(Projectile.Center - Projectile.velocity * 0.5f,
                    -Projectile.velocity * 0.08f, Body * 0.55f,
                    Main.rand.NextFloat(0.35f, 0.55f) * Projectile.scale)?.Configure(Main.rand.Next(12, 18));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(Variant == 0 ? BuffID.Poisoned : BuffID.Venom, Variant == 0 ? 360 : 240);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.55f, Pitch = 0.1f, MaxInstances = 3 }, target.Center);
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_ToxicMist>(target.Center + Main.rand.NextVector2Circular(9f, 9f),
                        Main.rand.NextVector2Circular(2f, 2f), Body * 0.6f,
                        Main.rand.NextFloat(0.4f, 0.65f))?.Configure(Main.rand.Next(12, 20));
                }
            }
        }

        public override void OnKill(int timeLeft) {
            //剧毒蛇消亡：owner 端滞留酸池（OnKill 各端都跑，生成守门）
            if (Variant == 1 && Projectile.owner == Main.myPlayer) {
                int poolDamage = Math.Max(1, (int)(Projectile.damage * 0.25f));
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsMorphFangAcidPoolProj>(), poolDamage, 0f, Projectile.owner);
            }
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.6f, Pitch = -0.2f }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_ToxicMist>(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                    Main.rand.NextVector2Circular(2.2f, 2.2f), Body * 0.55f,
                    Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(16, 26));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //蛇身自绘：拖尾史取节，原版毒牙贴图作节体 + 本色辉光（A=0 加色），identity 定相脉动
            int fangType = Variant == 0 ? ProjectileID.PoisonFang : ProjectileID.VenomFang;
            Main.instance.LoadProjectile(fangType);
            Texture2D tex = TextureAssets.Projectile[fangType].Value;
            Rectangle frame = tex.Frame(1, Main.projFrames[fangType], 0, 0);
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                Vector2 pos = Projectile.oldPos[i];
                if (pos == Vector2.Zero) {
                    continue;
                }
                pos += Projectile.Size / 2f;
                float shrink = (1.15f - i * 0.055f) * Projectile.scale;
                float pulse = 0.82f + 0.18f * MathF.Sin(Main.GlobalTimeWrappedHourly * 7f + Projectile.identity * 0.77f + i * 0.6f);
                float rot = Projectile.oldRot[i] + MathHelper.PiOver2;
                Main.EntitySpriteDraw(tex, pos - Main.screenPosition, frame,
                    Body with { A = 0 } * (0.5f * shrink * pulse), rot,
                    frame.Size() / 2f, shrink * 1.25f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, pos - Main.screenPosition, frame,
                    Color.White with { A = 60 } * (0.5f * shrink), rot,
                    frame.Size() / 2f, shrink * 0.85f, SpriteEffects.None, 0);
            }
            //蛇首亮芒
            Texture2D star = CWRAsset.StarTexture.Value;
            Main.EntitySpriteDraw(star, Projectile.Center - Main.screenPosition, null,
                Bright with { A = 0 } * 0.8f, Projectile.rotation, star.Size() / 2f,
                0.26f * Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 剧毒酸池：剧毒蛇消亡处滞留的腐蚀洼液，多跳低伤挂剧毒
    /// （判定圆与可见酸面同源；自绘三层垂坠酸辉 + 酸泡缓升）
    /// </summary>
    internal class GsMorphFangAcidPoolProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithMagicMorph";

        private const int LifeTicks = 200;
        private const float Radius = 52f;

        private static readonly Color AcidMain = new(186, 92, 224);
        private static readonly Color AcidBright = new(236, 164, 255);
        private static readonly Color AcidDeep = new(88, 30, 112);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24;
            Projectile.timeLeft = LifeTicks;
        }

        /// <summary>漫开-驻留-蒸干的半径生命周期</summary>
        private float RadiusNow {
            get {
                float grow = MathHelper.Clamp((LifeTicks - Projectile.timeLeft) / 10f, 0f, 1f);
                float fade = MathHelper.Clamp(Projectile.timeLeft / 24f, 0f, 1f);
                return Radius * VaultUtils.EaseOutQuad(grow) * fade;
            }
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            if (VaultUtils.isServer) {
                return;
            }
            //酸泡缓升
            if (Projectile.timeLeft % 6 == 0) {
                float r = RadiusNow;
                PRTLoader.NewParticle<PRT_ToxicMist>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-r, r) * 0.8f, Main.rand.NextFloat(-4f, 8f)),
                    new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(0.3f, 0.8f)),
                    AcidMain * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(14, 22));
            }
            Lighting.AddLight(Projectile.Center, AcidMain.ToVector3() * 0.28f * (RadiusNow / Radius));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Venom, 150);

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
            //三层垂坠酸辉（A=0 加色批），identity 定相的黏稠呼吸
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float r = RadiusNow;
            if (r < 4f) {
                return false;
            }
            Vector2 basePos = Projectile.Center - Main.screenPosition;
            for (int i = 0; i < 3; i++) {
                float phase = Main.GlobalTimeWrappedHourly * 3.1f + Projectile.identity * 0.59f + i * 2.3f;
                Vector2 off = new(MathF.Sin(phase) * r * 0.2f, MathF.Abs(MathF.Cos(phase * 0.7f)) * 4f);
                float s = r / glow.Width * (2.0f - i * 0.45f);
                Color c = (i == 2 ? AcidBright : i == 1 ? AcidMain : AcidDeep) with { A = 0 };
                Main.EntitySpriteDraw(glow, basePos + off, null, c * (0.3f + i * 0.1f), 0f,
                    glow.Size() / 2f, new Vector2(s, s * 0.62f), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
