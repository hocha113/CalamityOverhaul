using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit;
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
    /// 咒焰之书重铸（A 档）。材质身份：不灭的腐化咒火。<br/>
    /// ①咒火弹裹旋邪焰、蛇形摆尾飞行；②正拍命中种下咒种，三咒自燃爆出八向咒刺并滞留咒火滩；
    /// ③共鸣满层强化咏唱纵出蜿蜒焚蟒，沿途滴落咒火滩；④施法有后压体感与起手腾焰
    /// </summary>
    internal class GsCursedFlames : GsChantScheme
    {
        public override int TargetItemID => ItemID.CursedFlames;

        protected override string GsDescFallback =>
            "Reforged: on-beat bolts plant curse seeds; the third seed ignites into a ring of cursed spikes and a lingering fire pool" +
            "\nAt full resonance the next cast unleashes a weaving curse serpent that sheds fire pools along its path";

        protected override float BaseDamageMult => 1.05f;
        protected override Color ChantColor => CurseGreen;

        internal static readonly Color CurseGreen = new(150, 235, 70);
        internal static readonly Color CurseDeep = new(66, 130, 24);

        /// <summary>私有形态：咒刺（三咒引爆的放射尖焰）</summary>
        private const float FormCurseSpike = 10f;

        /// <summary>原版咒火弹类型（模板数据取，SetDefaults 密封不可写）</summary>
        private static int BoltType => ContentSamples.ItemsByType[ItemID.CursedFlames].shoot;

        //==================== 动画法：施法后压 + 起手腾焰 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //施法后压：出手瞬间书身后坐 3px 并上挑，随动画进度回坐（绝对剖面 0.1·p，差分施加防累积漂移）
            float n = player.itemAnimationMax;
            float progress = player.itemAnimation / n;
            player.itemLocation -= new Vector2(player.direction, 0f) * (3f * progress);
            GsMagicKickMath.ApplyKickDiff(player, 0.1f * progress, 0.1f * ((player.itemAnimation + 1) / n));
        }

        public override void GsUseAnimation(Item item, Player player) {
            if (VaultUtils.isServer) {
                return;
            }
            //起手腾焰：书页上方一撮咒火腾起（各端可见的起手光效）
            Vector2 tip = player.MountedCenter + new Vector2(player.direction * 14f, -10f);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_HellFlame>(tip + Main.rand.NextVector2Circular(5f, 4f),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(1.2f, 2.4f)),
                    CurseGreen, Main.rand.NextFloat(0.35f, 0.55f));
            }
            Lighting.AddLight(tip, CurseGreen.ToVector3() * 0.4f);
        }

        //==================== 强化咏唱：焚蟒 ====================

        protected override bool? ChantEmpowerShoot(Item item, Player player, GsChantPlayer chant,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback) {
            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Volume = 0.8f, Pitch = -0.25f }, position);
            int serpentDamage = Math.Max(1, (int)(damage * 1.55f));
            Projectile.NewProjectile(source, position, velocity.SafeNormalize(Vector2.UnitX) * 9f,
                ModContent.ProjectileType<GsCursedFlamesSerpentProj>(), serpentDamage, knockback, player.whoAmI);
            return false;
        }

        //==================== 弹幕行为：原生弹蛇摆 / 咒刺直线衰速 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != BoltType) {
                return;
            }
            if (router.MarkData == FormCurseSpike) {
                //咒刺：出膛急、迅速泄力，短程放射（无重力直线）
                if (proj.timeLeft < 20) {
                    proj.velocity *= 0.93f;
                }
                if (!VaultUtils.isServer && proj.timeLeft % 2 == 0) {
                    PRTLoader.NewParticle<PRT_Spark>(proj.Center, -proj.velocity * 0.1f,
                        CurseGreen, Main.rand.NextFloat(0.24f, 0.4f))?.Configure(false, Main.rand.Next(6, 10));
                }
                Lighting.AddLight(proj.Center, CurseGreen.ToVector3() * 0.2f);
                return;
            }
            //原生咒火弹：蛇形摆尾（identity 定相的确定性转向，各端一致），禁匀速直飞
            proj.velocity = proj.velocity.RotatedBy(MathF.Sin(proj.timeLeft * 0.33f + proj.identity * 0.9f) * 0.022f);
            if (!VaultUtils.isServer) {
                //裹旋邪焰：绕体螺旋的两簇咒火
                if (proj.timeLeft % 3 == 0) {
                    float ang = proj.timeLeft * 0.5f + proj.identity;
                    Vector2 orbit = ang.ToRotationVector2() * 7f;
                    PRTLoader.NewParticle<PRT_HellFlame>(proj.Center + orbit - proj.velocity * 0.4f,
                        -proj.velocity * 0.08f, IsOnBeatProj(router) ? CurseGreen : CurseDeep,
                        Main.rand.NextFloat(0.32f, 0.5f));
                }
                Lighting.AddLight(proj.Center, CurseGreen.ToVector3() * 0.3f);
            }
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            //余痕相：咒烬升腾，活得比弹体久
            if (VaultUtils.isServer || proj.type != BoltType) {
                return;
            }
            int count = router.MarkData == FormCurseSpike ? 2 : 4;
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_Light>(proj.Center + Main.rand.NextVector2Circular(6f, 6f),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.5f, 1.1f)),
                    CurseGreen, Main.rand.NextFloat(0.07f, 0.12f))?.Configure(Main.rand.Next(18, 30), 0.6f);
            }
        }

        //==================== 命中：种咒与引爆 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (!VaultUtils.isServer && proj.type == BoltType) {
                //命中反馈：咒火迸绽（正拍更盛）
                int burst = IsOnBeatProj(router) ? 4 : 2;
                for (int i = 0; i < burst; i++) {
                    PRTLoader.NewParticle<PRT_HellFlame>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                        Main.rand.NextVector2Circular(1.8f, 1.8f) - new Vector2(0f, 1f),
                        CurseGreen, Main.rand.NextFloat(0.4f, 0.65f));
                }
            }
            if (!proj.IsOwnedByLocalPlayer() || proj.type != BoltType) {
                return;
            }
            if (router.MarkData == FormCurseSpike) {
                target.AddBuff(BuffID.CursedInferno, 120);
                return;
            }
            if (!IsOnBeatProj(router)) {
                return;
            }
            //正拍种咒：三咒自燃
            GsCursedFlamesNPC seeds = target.GetGlobalNPC<GsCursedFlamesNPC>();
            seeds.AddSeed(300);
            if (seeds.SeedStacks < 3) {
                return;
            }
            seeds.ClearSeeds();
            DetonateSeeds(proj, target);
        }

        /// <summary>三咒引爆：八向咒刺 + 咒火滩（owner 端生成，全端可见）</summary>
        private void DetonateSeeds(Projectile proj, NPC target) {
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.75f, Pitch = 0.1f }, target.Center);
            int spikeDamage = Math.Max(1, (int)(proj.damage * 0.4f));
            float baseRot = proj.velocity.ToRotation();
            for (int i = 0; i < 8; i++) {
                Vector2 vel = (baseRot + MathHelper.TwoPi * i / 8f).ToRotationVector2() * 10.5f;
                QueueForm(Main.player[proj.owner], FormCurseSpike);
                int idx = Projectile.NewProjectile(proj.GetSource_FromThis(), target.Center, vel,
                    BoltType, spikeDamage, proj.knockBack * 0.3f, proj.owner);
                if (idx >= 0 && idx < Main.maxProjectiles) {
                    Projectile spike = Main.projectile[idx];
                    spike.scale *= 0.7f;
                    spike.timeLeft = 30;
                    spike.tileCollide = false;
                    spike.netUpdate = true;
                }
            }
            int poolDamage = Math.Max(1, (int)(proj.damage * 0.22f));
            Projectile.NewProjectile(proj.GetSource_FromThis(), target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsCursedFlamesPoolProj>(), poolDamage, 0f, proj.owner);
        }
    }

    /// <summary>
    /// 咒种标记（攻击方本地量：命中钩子只在攻击方端执行，引爆裁决与可见结果经弹幕过线）
    /// </summary>
    internal class GsCursedFlamesNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>咒种层数（3 层自燃）</summary>
        internal int SeedStacks;

        /// <summary>咒种失效时刻</summary>
        internal uint SeedUntil;

        internal void AddSeed(uint durationTicks) {
            if (SeedStacks > 0 && Main.GameUpdateCount >= SeedUntil) {
                SeedStacks = 0;
            }
            SeedStacks++;
            SeedUntil = Main.GameUpdateCount + durationTicks;
        }

        internal void ClearSeeds() {
            SeedStacks = 0;
            SeedUntil = 0;
        }

        public override void DrawEffects(NPC npc, ref Color drawColor) {
            //咒种在体表可见：层数越多邪焰越盛（各端只在攻击方端有层数，个人读数合法）
            if (SeedStacks <= 0 || Main.GameUpdateCount >= SeedUntil || Main.dedServ) {
                return;
            }
            if (Main.rand.NextBool(9 - SeedStacks * 2)) {
                PRTLoader.NewParticle<PRT_HellFlame>(npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f),
                    new Vector2(0f, -Main.rand.NextFloat(0.8f, 1.6f)),
                    GsCursedFlames.CurseGreen, Main.rand.NextFloat(0.3f, 0.45f));
            }
        }
    }

    /// <summary>
    /// 焚蟒：强化咏唱放出的蜿蜒咒焰巨蟒。owner 端每 30t 锁定近敌写 ai[1] 过线，
    /// 各端向同一目标缓转；沿途滴落咒火滩；身躯自绘（拖尾史分节 + 双层辉光）
    /// </summary>
    internal class GsCursedFlamesSerpentProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithMagicChant";

        private ref float SteerTimer => ref Projectile.localAI[0];
        private int TargetWho => (int)Projectile.ai[1] - 1;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 16;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 210;
        }

        public override void AI() {
            //蛇行：基速上叠正弦摆尾（identity 定相，各端确定性）
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.velocity = Projectile.velocity.RotatedBy(
                MathF.Sin(Projectile.timeLeft * 0.21f + Projectile.identity * 1.3f) * 0.045f);

            //owner 端周期性锁定近敌，目标经 ai[1] 过线，各端向同一目标缓转
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
                    float current = Projectile.velocity.ToRotation();
                    float wanted = (chase.Center - Projectile.Center).ToRotation();
                    Projectile.velocity = Utils.AngleTowards(current, wanted, MathHelper.ToRadians(2.4f))
                        .ToRotationVector2() * Projectile.velocity.Length();
                }
            }

            //沿途滴落咒火滩（owner 生成，全端可见）
            if (Projectile.IsOwnedByLocalPlayer() && Projectile.timeLeft % 36 == 0 && Projectile.timeLeft < 200) {
                int poolDamage = Math.Max(1, (int)(Projectile.damage * 0.15f));
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsCursedFlamesPoolProj>(), poolDamage, 0f, Projectile.owner);
            }

            if (!VaultUtils.isServer) {
                //蟒息：头部两侧喷息咒火
                if (Projectile.timeLeft % 2 == 0) {
                    Vector2 side = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2)
                        * MathF.Sin(Projectile.timeLeft * 0.6f) * 6f;
                    PRTLoader.NewParticle<PRT_HellFlame>(Projectile.Center + side,
                        -Projectile.velocity * 0.12f, GsCursedFlames.CurseGreen, Main.rand.NextFloat(0.45f, 0.7f));
                }
                Lighting.AddLight(Projectile.Center, GsCursedFlames.CurseGreen.ToVector3() * 0.55f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.CursedInferno, 240);
            //蟒噬亦种咒（攻击方端），与正拍弹协同引爆
            target.GetGlobalNPC<GsCursedFlamesNPC>().AddSeed(300);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.5f, Pitch = 0.2f, MaxInstances = 3 }, target.Center);
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_HellFlame>(target.Center + Main.rand.NextVector2Circular(10f, 10f),
                        Main.rand.NextVector2Circular(2.2f, 2.2f), GsCursedFlames.CurseGreen, Main.rand.NextFloat(0.5f, 0.8f));
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.7f, Pitch = -0.2f }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_HellFlame>(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                    Main.rand.NextVector2Circular(3f, 3f), GsCursedFlames.CurseGreen, Main.rand.NextFloat(0.5f, 0.9f));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //蛇身自绘：拖尾史取节，原版咒火贴图作节体 + 灼芯光晕（A=0 加色），identity 定相脉动
            int boltType = ContentSamples.ItemsByType[ItemID.CursedFlames].shoot;
            Main.instance.LoadProjectile(boltType);
            Texture2D tex = TextureAssets.Projectile[boltType].Value;
            Rectangle frame = tex.Frame(1, Main.projFrames[boltType], 0, 0);
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                Vector2 pos = Projectile.oldPos[i];
                if (pos == Vector2.Zero) {
                    continue;
                }
                pos += Projectile.Size / 2f;
                float shrink = 1.15f - i * 0.055f;
                float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.identity * 0.77f + i * 0.8f);
                Color glow = GsCursedFlames.CurseGreen with { A = 0 } * (0.55f * shrink * pulse);
                float rot = Projectile.oldRot[i] + MathHelper.PiOver2;
                Main.EntitySpriteDraw(tex, pos - Main.screenPosition, frame, glow, rot,
                    frame.Size() / 2f, shrink * 1.25f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, pos - Main.screenPosition, frame, Color.White with { A = 40 } * (0.5f * shrink), rot,
                    frame.Size() / 2f, shrink * 0.85f, SpriteEffects.None, 0);
            }
            //头部灼芯
            Texture2D star = CWRAsset.StarTexture.Value;
            Main.EntitySpriteDraw(star, Projectile.Center - Main.screenPosition, null,
                GsCursedFlames.CurseGreen with { A = 0 } * 0.9f, Projectile.rotation,
                star.Size() / 2f, 0.34f, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>咒火滩：引爆点/蟒径上滞留的邪焰域，多跳低伤（判定圆与可见焰团同源）</summary>
    internal class GsCursedFlamesPoolProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithMagicChant";

        private const int LifeTicks = 100;
        private const float Radius = 46f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = LifeTicks;
        }

        /// <summary>展开-驻留-熄灭的半径生命周期</summary>
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
            //焰滩舔舐：低频升腾的咒火舌
            if (Projectile.timeLeft % 4 == 0) {
                float r = RadiusNow;
                PRTLoader.NewParticle<PRT_HellFlame>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-r, r), Main.rand.NextFloat(-8f, 10f)),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.9f, 1.9f)),
                    Main.rand.NextBool() ? GsCursedFlames.CurseGreen : GsCursedFlames.CurseDeep,
                    Main.rand.NextFloat(0.35f, 0.6f));
            }
            Lighting.AddLight(Projectile.Center, GsCursedFlames.CurseGreen.ToVector3() * 0.35f * (RadiusNow / Radius));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.CursedInferno, 90);

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
            //三层呼吸焰团（A=0 加色批），identity 定相错开
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float r = RadiusNow;
            if (r < 4f) {
                return false;
            }
            Vector2 basePos = Projectile.Center - Main.screenPosition;
            for (int i = 0; i < 3; i++) {
                float phase = Main.GlobalTimeWrappedHourly * 4.2f + Projectile.identity * 0.53f + i * 2.1f;
                Vector2 off = new(MathF.Sin(phase) * r * 0.34f, -MathF.Abs(MathF.Cos(phase * 0.7f)) * 6f);
                float s = r / glow.Width * (2.1f - i * 0.45f);
                Color c = (i == 2 ? Color.White : i == 1 ? GsCursedFlames.CurseGreen : GsCursedFlames.CurseDeep) with { A = 0 };
                Main.EntitySpriteDraw(glow, basePos + off, null, c * (0.34f + i * 0.1f), 0f,
                    glow.Size() / 2f, s, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
