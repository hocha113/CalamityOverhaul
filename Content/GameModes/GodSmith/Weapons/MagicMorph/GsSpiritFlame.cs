using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
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
    /// 灵焰法杖重铸：沙漠冥火二形态。材质身份：蓝白灵焰（悬停锁猎的沙漠亡灵之火）。<br/>
    /// A 形态灵焰缠蓝白焰缕，命中时或迸出追灵火苗；
    /// B 形态（右键蓄 50t）「灵焰环阵」：在光标处布下五焰环阵，
    /// 阵焰轮巡扑猎入界之敌；施法有举杖响应与杖尖蓝辉
    /// </summary>
    internal class GsSpiritFlame : GsMorphScheme
    {
        public override int TargetItemID => ItemID.SpiritFlame;

        protected override string GsDescFallback =>
            "Reforged: spirit flames trail pale soulfire; half of their hits split off a hunting wisp" +
            "\nHold right click to charge; release to lay a five-flame ring at your cursor that pounces on intruders";

        protected override int ChargeTicksB => 50;
        protected override float ChargeManaMult => 1.9f;
        protected override Color ChargeColor => SoulBlue;
        protected override float BaseDamageMult => 1.05f;

        internal static readonly Color SoulBlue = new(96, 200, 255);
        internal static readonly Color SoulDeep = new(34, 72, 150);

        /// <summary>原版灵焰弹类型</summary>
        internal static int FlameType => ContentSamples.ItemsByType[ItemID.SpiritFlame].shoot;

        //==================== 动画法：举杖 + 杖尖蓝辉 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //施法举杖：出手瞬间杖身抬升 4px 再落回（确定性输入，各端一致）
            float progress = player.itemAnimation / (float)player.itemAnimationMax;
            player.itemLocation += new Vector2(0f, -4f * progress);
            player.itemRotation -= player.direction * 0.08f * progress;
        }

        public override void GsUseAnimation(Item item, Player player) {
            if (VaultUtils.isServer) {
                return;
            }
            //起手蓝辉：杖尖冥火拢聚
            Vector2 tip = player.MountedCenter + new Vector2(player.direction * 16f, -12f);
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_SoulFire>(tip + Main.rand.NextVector2Circular(6f, 6f),
                    new Vector2(0f, -Main.rand.NextFloat(0.6f, 1.4f)), SoulBlue, Main.rand.NextFloat(0.3f, 0.5f));
            }
            Lighting.AddLight(tip, SoulBlue.ToVector3() * 0.35f);
        }

        //==================== A 形态：焰缕与追灵火苗 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != FlameType || VaultUtils.isServer) {
                return;
            }
            //蓝白焰缕：悬停期缓漂、俯冲期拉丝（速度越快焰缕越长，禁匀速裸弹）
            float speed = proj.velocity.Length();
            int interval = speed > 6f ? 2 : 5;
            if (proj.timeLeft % interval == 0) {
                PRTLoader.NewParticle<PRT_SoulFire>(proj.Center - proj.velocity * 0.6f,
                    -proj.velocity * 0.1f + Main.rand.NextVector2Circular(0.3f, 0.3f),
                    SoulBlue, Main.rand.NextFloat(0.3f, 0.5f));
            }
            Lighting.AddLight(proj.Center, SoulBlue.ToVector3() * 0.4f);
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.type != FlameType) {
                return;
            }
            if (!VaultUtils.isServer) {
                //命中反馈：冥火迸散上腾
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_SoulFire>(target.Center + Main.rand.NextVector2Circular(9f, 9f),
                        Main.rand.NextVector2Circular(1.6f, 1.6f) - new Vector2(0f, 1.4f),
                        SoulBlue, Main.rand.NextFloat(0.4f, 0.65f));
                }
            }
            //追灵火苗：owner 端掷签，半数命中分出一缕（生成随原生链同步）
            if (proj.IsOwnedByLocalPlayer() && Main.rand.NextBool()) {
                int wispDamage = Math.Max(1, (int)(proj.damage * 0.3f));
                Vector2 vel = Main.rand.NextVector2CircularEdge(4f, 4f) - new Vector2(0f, 2f);
                Projectile.NewProjectile(proj.GetSource_FromThis(), target.Center, vel,
                    ModContent.ProjectileType<GsSpiritFlameWispProj>(), wispDamage, 0f, proj.owner);
            }
        }

        //==================== B 形态：灵焰环阵 ====================

        protected override void FireMorphB(Item item, Player player) {
            SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.9f, Pitch = -0.3f }, player.Center);
            //环阵锚点取光标（限 900px）；阵心弹幕的 damage 承载武器基伤，节点数走 ai0 随生成包过线
            Vector2 anchor = Main.MouseWorld;
            Vector2 off = anchor - player.Center;
            if (off.Length() > 900f) {
                anchor = player.Center + off.SafeNormalize(Vector2.UnitX) * 900f;
            }
            int damage = player.GetWeaponDamage(item);
            SpawnMorph(player, item, anchor, Vector2.Zero,
                ModContent.ProjectileType<GsSpiritFlameRingProj>(), damage, item.knockBack, KindB,
                0f, GsSpiritFlameRingProj.MaxNodes);
        }
    }

    /// <summary>
    /// 灵焰环阵：五焰节点绕阵心缓旋，敌入界（420px）则依次扑猎——
    /// owner 端消耗节点生成原版灵焰弹（自带悬猎 AI），剩余节点数经 ai[0] 过线各端同绘
    /// </summary>
    internal class GsSpiritFlameRingProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithMagicMorph";

        private const int LifeTicks = 420;
        private const float OrbitRadius = 88f;
        internal const int MaxNodes = 5;

        /// <summary>剩余阵焰节点（生成时经 ai0 入包；owner 消耗后 netUpdate 过线）</summary>
        private ref float NodesLeft => ref Projectile.ai[0];

        /// <summary>扑猎冷却（owner 本地量，远端不消费）</summary>
        private ref float PounceTimer => ref Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTicks;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation += 0.012f;

            //owner 端扑猎裁决：有敌入界且冷却就绪，消耗一个节点放出原版灵焰（自带悬猎 AI）
            if (Projectile.IsOwnedByLocalPlayer() && NodesLeft > 0) {
                if (PounceTimer > 0f) {
                    PounceTimer--;
                }
                NPC prey = Projectile.Center.FindClosestNPC(420f);
                if (prey != null && PounceTimer <= 0f) {
                    int idx = (int)(MaxNodes - NodesLeft);
                    Vector2 nodePos = NodePos(idx);
                    Vector2 vel = (prey.Center - nodePos).SafeNormalize(Vector2.UnitY) * 3f;
                    int dmg = Math.Max(1, (int)(Projectile.damage * 0.95f));
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), nodePos, vel,
                        GsSpiritFlame.FlameType, dmg, Projectile.knockBack, Projectile.owner);
                    NodesLeft--;
                    PounceTimer = 42f;
                    Projectile.netUpdate = true;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.6f, Pitch = 0.3f, MaxInstances = 3 }, nodePos);
                    }
                }
            }

            //节点耗尽提前散阵
            if (NodesLeft <= 0 && Projectile.timeLeft > 18) {
                Projectile.timeLeft = 18;
            }

            if (VaultUtils.isServer) {
                return;
            }
            //阵焰呼吸：在场节点各自缓燃
            if (Projectile.timeLeft % 5 == 0) {
                for (int i = (int)(MaxNodes - NodesLeft); i < MaxNodes; i++) {
                    if (Main.rand.NextBool(3)) {
                        PRTLoader.NewParticle<PRT_SoulFire>(NodePos(i), new Vector2(0f, -Main.rand.NextFloat(0.5f, 1f)),
                            GsSpiritFlame.SoulBlue, Main.rand.NextFloat(0.25f, 0.4f));
                    }
                }
            }
            Lighting.AddLight(Projectile.Center, GsSpiritFlame.SoulBlue.ToVector3() * 0.3f);
        }

        /// <summary>第 i 号节点的当前位（绕阵心缓旋，各端确定性）</summary>
        private Vector2 NodePos(int i) {
            float ang = MathHelper.TwoPi * i / MaxNodes + Projectile.rotation * 3f;
            return Projectile.Center + ang.ToRotationVector2() * OrbitRadius;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_SoulLight>(Projectile.Center + Main.rand.NextVector2Circular(OrbitRadius, OrbitRadius),
                    new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.2f)), GsSpiritFlame.SoulBlue, Main.rand.NextFloat(0.3f, 0.5f));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //阵环 + 阵焰节点自绘：环带淡辉、节点双层焰核，identity 定相脉动
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Vector2 center = Projectile.Center - Main.screenPosition;
            float fadeIn = MathHelper.Clamp((LifeTicks - Projectile.timeLeft) / 14f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 18f, 0f, 1f);
            float env = VaultUtils.EaseOutQuad(fadeIn) * fadeOut;
            if (env <= 0.03f) {
                return false;
            }
            //阵心淡涡
            Main.EntitySpriteDraw(glow, center, null, GsSpiritFlame.SoulDeep with { A = 0 } * (0.5f * env), 0f,
                glow.Size() / 2f, OrbitRadius * 2.4f / glow.Width, SpriteEffects.None, 0);
            //环带：十二点淡光珠勾出环形
            for (int i = 0; i < 12; i++) {
                float ang = MathHelper.TwoPi * i / 12f + Projectile.rotation * 3f;
                Vector2 pos = center + ang.ToRotationVector2() * OrbitRadius;
                Main.EntitySpriteDraw(glow, pos, null, GsSpiritFlame.SoulBlue with { A = 0 } * (0.22f * env), 0f,
                    glow.Size() / 2f, 0.09f, SpriteEffects.None, 0);
            }
            //阵焰节点：外焰 + 白芯
            for (int i = (int)(MaxNodes - NodesLeft); i < MaxNodes; i++) {
                float ang = MathHelper.TwoPi * i / MaxNodes + Projectile.rotation * 3f;
                Vector2 pos = center + ang.ToRotationVector2() * OrbitRadius;
                float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f + Projectile.identity * 0.7f + i * 1.9f);
                Main.EntitySpriteDraw(glow, pos, null, GsSpiritFlame.SoulBlue with { A = 0 } * (0.85f * env * pulse), 0f,
                    glow.Size() / 2f, 0.4f * pulse, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(star, pos, null, Color.White with { A = 0 } * (0.75f * env * pulse),
                    ang + Main.GlobalTimeWrappedHourly * 2f, star.Size() / 2f, 0.16f, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>追灵火苗：命中分出的小灵焰，弧线上飘后咬向近敌（自绘焰核+光缕尾）</summary>
    internal class GsSpiritFlameWispProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithMagicMorph";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.timeLeft = 90;
        }

        public override void AI() {
            //前 18t 上飘减速（出手弧线），随后锁定近敌俯冲
            if (Projectile.timeLeft > 72) {
                Projectile.velocity *= 0.95f;
            }
            else {
                NPC prey = Projectile.Center.FindClosestNPC(360f);
                if (prey != null) {
                    float wanted = (prey.Center - Projectile.Center).ToRotation();
                    float current = Projectile.velocity.ToRotation();
                    float speed = Math.Min(Projectile.velocity.Length() + 0.25f, 11f);
                    Projectile.velocity = Utils.AngleTowards(current, wanted, MathHelper.ToRadians(7f))
                        .ToRotationVector2() * speed;
                }
            }
            if (!VaultUtils.isServer && Projectile.timeLeft % 3 == 0) {
                PRTLoader.NewParticle<PRT_SoulLight>(Projectile.Center, -Projectile.velocity * 0.15f,
                    GsSpiritFlame.SoulBlue, Main.rand.NextFloat(0.2f, 0.32f));
            }
            Lighting.AddLight(Projectile.Center, GsSpiritFlame.SoulBlue.ToVector3() * 0.25f);
        }

        public override bool PreDraw(ref Color lightColor) {
            //光缕尾 + 双层焰核（A=0 加色批）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                Vector2 pos = Projectile.oldPos[i];
                if (pos == Vector2.Zero) {
                    continue;
                }
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                Main.EntitySpriteDraw(glow, pos + Projectile.Size / 2f - Main.screenPosition, null,
                    GsSpiritFlame.SoulBlue with { A = 0 } * (0.3f * fade), 0f,
                    glow.Size() / 2f, 0.16f * fade, SpriteEffects.None, 0);
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + Projectile.identity * 1.1f);
            Main.EntitySpriteDraw(glow, center, null, GsSpiritFlame.SoulBlue with { A = 0 } * (0.9f * pulse), 0f,
                glow.Size() / 2f, 0.24f * pulse, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, center, null, Color.White with { A = 0 } * 0.7f, 0f,
                glow.Size() / 2f, 0.12f, SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_SoulLight>(Projectile.Center, Main.rand.NextVector2Circular(1.5f, 1.5f),
                    GsSpiritFlame.SoulBlue, Main.rand.NextFloat(0.2f, 0.35f));
            }
        }
    }
}
