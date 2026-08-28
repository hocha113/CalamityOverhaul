using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Hardmode
{
    /// <summary>
    /// 【叶绿套·共生寄藤 ★A】丛林活金属的共生秘术：①命中积攒孢子，满八层后下一击把寄生藤种进目标
    /// ②藤缠五秒，期间你每次击打宿主，藤便鞭抽一道传导藤锋咬向近旁第二个敌人
    /// ③藤谢时炸成驻场孢子云继续侵蚀。原版套装奖励（叶绿水晶叶）保留，神赋叠加
    /// </summary>
    internal class GsChlorophyteArmor : GsArmorsBChargeScheme
    {
        public override int[] HeadIDs => [ItemID.ChlorophyteMask, ItemID.ChlorophyteHelmet, ItemID.ChlorophyteHeadgear];

        public override int BodyID => ItemID.ChlorophytePlateMail;

        public override int LegsID => ItemID.ChlorophyteGreaves;

        protected override string EndowLineFallback =>
            "Symbiotic Vine: strikes build spores; at 8 stacks the next strike plants a parasite vine, and every strike on the host lashes a vine-blade at a second foe; the vine bursts into a spore cloud when it withers";

        //叶绿色板
        internal static readonly Color ChloroBright = new(204, 255, 154);
        internal static readonly Color ChloroMain = new(112, 220, 84);
        internal static readonly Color ChloroDeep = new(42, 122, 48);
        internal static readonly Color SporeLime = new(174, 255, 94);

        protected override int FullCharge => 8;

        protected override Color ThemeMain => ChloroMain;

        protected override Color ThemeBright => SporeLime;

        protected override bool IsOwnProc(Projectile proj)
            => proj.type == ModContent.ProjectileType<GsChlorophyteVineParasiteProj>()
            || proj.type == ModContent.ProjectileType<GsChlorophyteVineLashProj>();

        private static Projectile FindParasiteOn(Player player, int npcIndex) {
            int type = ModContent.ProjectileType<GsChlorophyteVineParasiteProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == player.whoAmI && proj.type == type
                    && proj.ai[0] == 0f && (int)proj.ai[1] == npcIndex) {
                    return proj;
                }
            }
            return null;
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (sourceProj != null && IsOwnProc(sourceProj)) {
                return;
            }
            if (target.type == NPCID.TargetDummy) {
                return;
            }

            //宿主在藤上：每次击打传导鞭抽（佩戴者端裁定）
            Projectile parasite = FindParasiteOn(player, target.whoAmI);
            if (parasite != null) {
                if (player.whoAmI == Main.myPlayer
                    && parasite.ModProjectile is GsChlorophyteVineParasiteProj vine) {
                    vine.TryLash();
                }
                //传导期间照常积攒，藤谢后无缝接力
                if (state.EndowCharge < FullCharge) {
                    state.EndowCharge++;
                }
                return;
            }
            base.OnEndowHitNPC(player, state, target, hit, damageDone, sourceProj);
        }

        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.6f, Pitch = 0.15f }, target.Center);
                //种藤：孢尘扑起
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(10f, 10f),
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f),
                        i % 2 == 0 ? SporeLime : ChloroMain, Main.rand.NextFloat(0.3f, 0.5f))
                        ?.Configure(false, Main.rand.Next(14, 22));
                }
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            int lashDamage = Math.Clamp((int)(damageDone * 0.25f), 6, 100);
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithChlorophyteEndow"),
                target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsChlorophyteVineParasiteProj>(),
                lashDamage, 0f, player.whoAmI, 0f, target.whoAmI);
        }
    }

    /// <summary>
    /// 寄生藤：种进宿主的活体藤蔓，三根须蔓沿宿主躯体生长缠绕、孢囊脉动；
    /// 受主人号令向近旁第二敌鞭出传导藤锋（20 帧内至多一次）；
    /// 藤谢或宿主先亡即炸成驻场孢子云，两秒内持续侵蚀过客
    /// </summary>
    internal class GsChlorophyteVineParasiteProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>0=缠附 1=孢子云</summary>
        private ref float State => ref Projectile.ai[0];

        private ref float HostIndex => ref Projectile.ai[1];

        private ref float Life => ref Projectile.localAI[0];

        /// <summary>鞭抽冷却（帧）</summary>
        private ref float LashCooldown => ref Projectile.localAI[1];

        private float Seed => Projectile.identity * 0.8117f % 3.73f;

        /// <summary>须蔓生长帧数</summary>
        private const int GrowFrames = 15;

        /// <summary>孢子云时长</summary>
        private const int CloudFrames = 120;

        private float VisualFade => MathHelper.Clamp(Life / 8f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        /// <summary>缠附态不判定，孢子云态才侵蚀</summary>
        public override bool? CanDamage() => State == 1f;

        /// <summary>受主人号令鞭出传导藤锋（佩戴者端调用）</summary>
        internal void TryLash() {
            if (State != 0f || LashCooldown > 0f || Projectile.owner != Main.myPlayer) {
                return;
            }
            NPC host = HostIndex >= 0 && HostIndex < Main.maxNPCs ? Main.npc[(int)HostIndex] : null;
            //找近旁第二个敌人
            NPC second = null;
            float bestDist = 320f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.whoAmI == (int)HostIndex || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Projectile.Center.Distance(npc.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    second = npc;
                }
            }
            if (second == null || host == null) {
                return;
            }
            LashCooldown = 20f;
            Vector2 vel = (second.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 24f;
            Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                Projectile.Center, vel,
                ModContent.ProjectileType<GsChlorophyteVineLashProj>(),
                Projectile.damage, 1f, Projectile.owner);
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.4f, Pitch = 0.35f, MaxInstances = 3 }, Projectile.Center);
            }
        }

        public override void AI() {
            Life++;
            if (LashCooldown > 0f) {
                LashCooldown--;
            }

            if (State == 0f) {
                NPC host = HostIndex >= 0 && HostIndex < Main.maxNPCs ? Main.npc[(int)HostIndex] : null;
                if (host == null || !host.active || Projectile.timeLeft <= 2) {
                    //藤谢/宿主亡：炸成孢子云
                    State = 1f;
                    Life = 0f;
                    Projectile.timeLeft = CloudFrames;
                    Projectile.netUpdate = true;
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.5f, Pitch = -0.3f, MaxInstances = 3 }, Projectile.Center);
                        for (int i = 0; i < 10; i++) {
                            PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center,
                                Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.8f, 2.2f),
                                GsChlorophyteArmor.ChloroMain, Main.rand.NextFloat(0.35f, 0.55f))
                                ?.Configure(30, 0.4f, 0.03f);
                        }
                    }
                    return;
                }
                //缠附宿主
                Projectile.Center = host.Center;
                Projectile.velocity = Vector2.Zero;
                //孢尘剥落（客户端装饰）
                if (!Main.dedServ && Main.rand.NextBool(9)) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2Circular(host.width * 0.4f, host.height * 0.4f),
                        new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.9f)),
                        GsChlorophyteArmor.SporeLime, Main.rand.NextFloat(0.18f, 0.3f))?.Configure(false, Main.rand.Next(10, 16));
                }
                Lighting.AddLight(Projectile.Center, GsChlorophyteArmor.ChloroMain.ToVector3() * (0.18f * VisualFade));
                return;
            }

            //孢子云：驻场缓漂
            Projectile.velocity *= 0.96f;
            if (!Main.dedServ && Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2Circular(40f, 34f),
                    new Vector2(MathF.Sin(Life * 0.05f) * 0.3f, -Main.rand.NextFloat(0.2f, 0.6f)),
                    Main.rand.NextBool() ? GsChlorophyteArmor.SporeLime : GsChlorophyteArmor.ChloroMain,
                    Main.rand.NextFloat(0.16f, 0.3f))?.Configure(false, Main.rand.Next(14, 24));
            }
            Lighting.AddLight(Projectile.Center, GsChlorophyteArmor.SporeLime.ToVector3() * (0.2f * Projectile.timeLeft / CloudFrames));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Poisoned, 180);

        //==================== 绘制：缠附须蔓 + 孢囊 / 孢子云三团 ====================

        /// <summary>逐段画一根沿宿主缠绕的须蔓</summary>
        private void DrawTendril(Texture2D seg, Vector2 hostCenter, Vector2 hostSize, int index, float grow, float fade) {
            const int Segments = 6;
            float phase = Seed * 2f + index * MathHelper.TwoPi / 3f;
            Vector2 prev = hostCenter + new Vector2(0f, hostSize.Y * 0.45f);
            for (int i = 1; i <= (int)(Segments * grow); i++) {
                float t = i / (float)Segments;
                //须蔓沿躯体螺旋爬升
                float ang = phase + t * 4.2f + Life * 0.02f;
                Vector2 at = hostCenter + new Vector2(
                    MathF.Cos(ang) * hostSize.X * 0.52f * (0.6f + t * 0.4f),
                    hostSize.Y * (0.45f - t * 0.9f) + MathF.Sin(Life * 0.06f + phase + t * 3f) * 2.5f);
                Vector2 delta = at - prev;
                float rot = delta.ToRotation();
                float len = delta.Length() / seg.Width;
                //藤段双层
                Main.EntitySpriteDraw(seg, (prev + at) * 0.5f - Main.screenPosition, null,
                    (GsChlorophyteArmor.ChloroDeep with { A = 0 }) * (0.85f * fade), rot, seg.Size() * 0.5f,
                    new Vector2(len * 1.15f, 0.06f), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(seg, (prev + at) * 0.5f - Main.screenPosition, null,
                    (GsChlorophyteArmor.ChloroMain with { A = 0 }) * fade, rot, seg.Size() * 0.5f,
                    new Vector2(len, 0.035f), SpriteEffects.None, 0);
                prev = at;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D seg = CWRAsset.LightShot?.Value;
            Texture2D core = CWRAsset.Extra_98?.Value;
            Texture2D fog = CWRAsset.Fog?.Value;
            if (seg == null || core == null || fog == null) {
                return false;
            }

            if (State == 0f) {
                NPC host = HostIndex >= 0 && HostIndex < Main.maxNPCs ? Main.npc[(int)HostIndex] : null;
                if (host == null || !host.active) {
                    return false;
                }
                float fade = VisualFade;
                float grow = MathHelper.Clamp(Life / GrowFrames, 0f, 1f);
                //三根须蔓
                for (int i = 0; i < 3; i++) {
                    DrawTendril(seg, host.Center, host.Size, i, grow, fade);
                }
                //两颗孢囊脉动
                float pulse = 0.8f + MathF.Sin(Life * 0.16f + Seed * 5f) * 0.2f;
                for (int i = 0; i < 2; i++) {
                    float ang = Seed * 3f + i * MathHelper.Pi + Life * 0.02f;
                    Vector2 sac = host.Center + ang.ToRotationVector2() * host.Size * 0.32f - Main.screenPosition;
                    Main.EntitySpriteDraw(core, sac, null,
                        (GsChlorophyteArmor.SporeLime with { A = 0 }) * (0.7f * pulse * fade * grow), 0f, core.Size() * 0.5f,
                        0.07f * pulse, SpriteEffects.None, 0);
                }
                return false;
            }

            //孢子云：三团慢旋孢雾
            float cfade = MathHelper.Clamp(Projectile.timeLeft / (float)CloudFrames, 0f, 1f);
            for (int i = 0; i < 3; i++) {
                float ang = Life * 0.014f + i * MathHelper.TwoPi / 3f + Seed;
                Vector2 puff = Projectile.Center + ang.ToRotationVector2() * 20f - Main.screenPosition;
                Main.EntitySpriteDraw(fog, puff, null,
                    GsChlorophyteArmor.ChloroMain * (0.5f * cfade), ang * 0.5f, fog.Size() * 0.5f,
                    new Vector2(0.20f, 0.17f), SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(core, Projectile.Center - Main.screenPosition, null,
                (GsChlorophyteArmor.SporeLime with { A = 0 }) * (0.5f * cfade), 0f, core.Size() * 0.5f,
                0.2f + MathF.Sin(Life * 0.1f + Seed) * 0.03f, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 传导藤锋：藤上鞭出的一道绿锋，快甩慢收，锋体月牙叠色 + 抽鞭残迹，命中挂毒
    /// </summary>
    internal class GsChlorophyteVineLashProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "CrescentEdge01";

        private ref float Life => ref Projectile.ai[0];

        private float Seed => Projectile.identity * 0.9539f % 4.19f;

        private float VisualFade => Math.Min(
            MathHelper.Clamp(Life / 3f, 0f, 1f),
            MathHelper.Clamp(Projectile.timeLeft / 4f, 0f, 1f));

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 5;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 16;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            //快甩慢收
            if (Life > 7f) {
                Projectile.velocity *= 0.84f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, GsChlorophyteArmor.ChloroMain.ToVector3() * (0.2f * VisualFade));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Poisoned, 240);
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.4f, Pitch = 0.4f, MaxInstances = 3 }, target.Center);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4.5f),
                    Main.rand.NextBool() ? GsChlorophyteArmor.SporeLime : GsChlorophyteArmor.ChloroMain,
                    Main.rand.NextFloat(0.28f, 0.46f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        //==================== 绘制：绿锋月牙 + 抽鞭残迹 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D crescent = CWRAsset.CrescentEdge01?.Value;
            if (crescent == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 origin = crescent.Size() * 0.5f;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.03f, 0.05f, 0.6f);
            float wob = MathF.Sin(Life * 0.7f + Seed * 5f) * 0.05f;

            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float ghost = (1f - i / (float)Projectile.oldPos.Length) * 0.35f * fade;
                Main.EntitySpriteDraw(crescent, Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition, null,
                    (GsChlorophyteArmor.ChloroDeep with { A = 0 }) * ghost, Projectile.rotation, origin,
                    new Vector2(0.24f + stretch * 0.5f, 0.15f) * (1f - i * 0.08f), SpriteEffects.None, 0);
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(crescent, pos, null,
                (GsChlorophyteArmor.ChloroMain with { A = 0 }) * fade, Projectile.rotation, origin,
                new Vector2(0.28f + stretch * 0.8f, 0.18f + wob), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(crescent, pos, null,
                (GsChlorophyteArmor.ChloroBright with { A = 0 }) * (0.8f * fade), Projectile.rotation, origin,
                new Vector2(0.19f + stretch * 0.5f, 0.09f + wob * 0.6f), SpriteEffects.None, 0);
            return false;
        }
    }
}
