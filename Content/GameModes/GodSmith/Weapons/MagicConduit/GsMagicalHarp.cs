using CalamityOverhaul.Common;
using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
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
    /// 魔法竖琴重铸：渐强奏鸣。材质身份：星彩琴弦（音符凝成的流光声波）。<br/>
    /// ①热量换皮「渐强」：连奏积攒声势，白热=最强奏（音符增大提速、尾迹音符雨）；
    /// 顶格维持不断奏，只涨蓝耗；<br/>
    /// ②泄压「终止和弦」：右键把声势化为一圈环形和弦冲击波（威力随声势）；<br/>
    /// ③「余韵」：音符 0.5 秒内再命中同一目标伤害 +10%（至多三层）；④拨弦顿挫体感与音符粒子
    /// </summary>
    internal class GsMagicalHarp : GsHeatScheme
    {
        public override int TargetItemID => ItemID.MagicalHarp;

        protected override string GsDescFallback =>
            "Reforged: unbroken play builds crescendo; at fortissimo the notes swell, race and rain music" +
            "\nRight click to resolve all crescendo into a ringing chord shockwave; rapid re-hits echo for bonus damage";

        //渐强语义：热量 = 声势。连奏积攒（每奏 +4），停手渐散；白热带 = 最强奏
        internal override float HeatPerShot => 4f;
        internal override float CoolRatePerTick => 0.6f;
        internal override float WhiteHotDamageMult => 1.12f;
        internal override float BaseDamageMult => 1f;
        internal override GsOverloadPolicy OverloadPolicy => GsOverloadPolicy.Sustain;
        internal override Color MuzzleTheme => HarpViolet;

        internal static readonly Color HarpViolet = new(178, 140, 255);
        internal static readonly Color HarpBlue = new(120, 200, 255);
        internal static readonly Color HarpDeep = new(70, 40, 140);

        private static int NoteType => ProjectileID.QuarterNote;

        /// <summary>顶格维持（Sustain）的临界蓝耗：声势拉满后每奏更费魔</summary>
        internal override float ExtraManaCostMult(Player player, GsHeatPlayer hp)
            => hp.BoundItemType == TargetItemID && hp.Heat >= GsHeatPlayer.HeatMax ? 1.6f : 1f;

        internal override void OnHeatCapped(Player player, GsHeatPlayer hp) {
            //声势顶格的一次性提示：定音 + 音符迸散（owner 本地反馈）
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item26 with { Volume = 0.9f, Pitch = 0.5f }, player.Center);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Note>(player.MountedCenter + Main.rand.NextVector2Circular(14f, 14f),
                    new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(0.8f, 1.6f)),
                    HarpViolet, Main.rand.NextFloat(0.5f, 0.7f))?.Configure(Main.rand.Next(24, 36));
            }
        }

        //==================== 动画法：拨弦顿挫 + 音符粒子 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //拨弦顿挫：按 itemAnimation 奇偶小幅交替（琴身随拨弦轻颤），随进度回稳（确定性输入）
            float progress = player.itemAnimation / (float)player.itemAnimationMax;
            float pluck = player.itemAnimation % 2 == 0 ? 1f : -1f;
            player.itemLocation += new Vector2(0f, pluck * 1.2f * progress);
            player.itemRotation += player.direction * pluck * 0.04f * progress;
        }

        public override void GsUseAnimation(Item item, Player player) {
            if (VaultUtils.isServer) {
                return;
            }
            //拨弦音符：琴弦上蹦出一枚小音符（各端可见的起手反馈）
            Vector2 tip = player.MountedCenter + new Vector2(player.direction * 14f, -8f);
            PRTLoader.NewParticle<PRT_Note>(tip + Main.rand.NextVector2Circular(4f, 6f),
                new Vector2(player.direction * Main.rand.NextFloat(0.4f, 1f), -Main.rand.NextFloat(0.5f, 1.1f)),
                Main.rand.NextBool() ? HarpViolet : HarpBlue, Main.rand.NextFloat(0.35f, 0.5f))
                ?.Configure(Main.rand.Next(20, 30));
            Lighting.AddLight(tip, HarpViolet.ToVector3() * 0.25f);
        }

        //==================== 最强奏：音符升格 ====================

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            base.GsProjOnSpawnMarked(proj, router);
            //白热出生的音符：增大提速（原版音符本就无限穿透且逐穿衰减 5%，TML 源已证，
            //设计的「穿透 +1」不可加，落地为弹速 +15% 的覆盖增益）
            if (proj.owner == Main.myPlayer && proj.type == NoteType && router.MarkData >= 1f) {
                proj.scale *= 1.25f;
                proj.velocity *= 1.15f;
                proj.netUpdate = true;
            }
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != NoteType || VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(proj.Center, HarpViolet.ToVector3() * 0.22f);
            //飞行相：星彩声波微光；最强奏音符身后落下音符雨
            if (proj.timeLeft % 5 == 0) {
                PRTLoader.NewParticle<PRT_Light>(proj.Center - proj.velocity * 0.5f,
                    -proj.velocity * 0.05f, HarpBlue, Main.rand.NextFloat(0.05f, 0.09f))
                    ?.Configure(Main.rand.Next(10, 18), 0.6f);
            }
            if (router.MarkData >= 1f && proj.timeLeft % 8 == 0) {
                PRTLoader.NewParticle<PRT_Note>(proj.Center - proj.velocity * 1.2f,
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(0.4f, 1f)),
                    Main.rand.NextBool() ? HarpViolet : HarpBlue, Main.rand.NextFloat(0.24f, 0.36f))
                    ?.Configure(Main.rand.Next(18, 28));
            }
        }

        public override void GsProjPostDraw(Projectile proj, Color lightColor, GodSmithProjRouter router) {
            if (proj.type != NoteType) {
                return;
            }
            //音符底垫彩辉：原版音符贴图作本体，identity 定相取色的加色光垫（最强奏更盛 + 白芯）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 center = proj.Center - Main.screenPosition;
            float hue = (proj.identity * 0.317f) % 1f;
            Color pad = Main.hslToRgb(hue, 0.6f, 0.62f) with { A = 0 };
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 7f + proj.identity * 0.9f);
            bool forte = router.MarkData >= 1f;
            Main.EntitySpriteDraw(glow, center, null, pad * (forte ? 0.75f : 0.5f) * pulse, 0f,
                glow.Size() / 2f, (forte ? 0.4f : 0.28f) * pulse * proj.scale, SpriteEffects.None, 0);
            if (forte) {
                Main.EntitySpriteDraw(glow, center, null, Color.White with { A = 0 } * (0.4f * pulse), 0f,
                    glow.Size() / 2f, 0.16f * pulse * proj.scale, SpriteEffects.None, 0);
            }
        }

        //==================== 余韵：连击回响 ====================

        public override void GsProjModifyHitNPC(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            if (proj.type != NoteType) {
                return;
            }
            //余韵消费：0.5 秒内再命中同一目标 +10%/层（层数是攻击方本地量，命中链天然攻击方端）
            GsMagicalHarpNPC echo = target.GetGlobalNPC<GsMagicalHarpNPC>();
            if (echo.EchoStacks > 0 && Main.GameUpdateCount < echo.EchoUntil) {
                modifiers.FinalDamage *= 1f + 0.10f * Math.Min(echo.EchoStacks, 3);
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.type != NoteType) {
                return;
            }
            if (!VaultUtils.isServer) {
                //命中反馈：声波涟漪 + 音符弹出（与原版音符命中区分）
                PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, HarpBlue, 0.12f)?.Configure(8, 0.7f);
                PRTLoader.NewParticle<PRT_Note>(target.Center + Main.rand.NextVector2Circular(6f, 6f),
                    new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(0.8f, 1.4f)),
                    HarpViolet, Main.rand.NextFloat(0.3f, 0.42f))?.Configure(Main.rand.Next(16, 24));
            }
            //余韵叠层：窗口内续层（上限 3），窗口外重开
            GsMagicalHarpNPC echo = target.GetGlobalNPC<GsMagicalHarpNPC>();
            uint now = Main.GameUpdateCount;
            echo.EchoStacks = now < echo.EchoUntil ? Math.Min(echo.EchoStacks + 1, 3) : 1;
            echo.EchoUntil = now + 30;
        }

        //==================== 泄压：终止和弦 ====================

        internal override void FireVent(Player player, GsHeatPlayer hp) {
            float frac = hp.Heat / GsHeatPlayer.HeatMax;
            int damage = Math.Max(1, (int)(player.GetWeaponDamage(player.HeldItem) * (0.8f + 2.4f * frac)));
            Projectile.NewProjectile(player.GetSource_Misc("GsConduitVent"), player.MountedCenter, Vector2.Zero,
                ModContent.ProjectileType<GsMagicalHarpNovaProj>(), damage, 8f, player.whoAmI, frac);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item26 with { Volume = 1f, Pitch = -0.4f }, player.Center);
                SoundEngine.PlaySound(SoundID.Item26 with { Volume = 0.7f, Pitch = 0.1f }, player.Center);
            }
        }
    }

    /// <summary>
    /// 余韵标记（攻击方本地量：命中钩子只在攻击方端执行，加成只在攻击方端结算）
    /// </summary>
    internal class GsMagicalHarpNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>余韵层数（上限 3）</summary>
        internal int EchoStacks;

        /// <summary>余韵窗关闭时刻</summary>
        internal uint EchoUntil;
    }

    /// <summary>
    /// 终止和弦：以奏者为心扩张的环形和弦冲击波，威力与半径随声势（ai0 = 声势比 0~1）；
    /// 环带判定与可见环同源，每目标一击；自绘环层 + 沿环迸出的音符
    /// </summary>
    internal class GsMagicalHarpNovaProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithMagicConduit";

        private const int LifeTicks = 30;

        /// <summary>声势比 0~1（生成时经 ai0 入包，各端同绘同判）</summary>
        private float HeatFrac => MathHelper.Clamp(Projectile.ai[0], 0f, 1f);

        private float MaxRadius => 150f + 170f * HeatFrac;

        private float RadiusNow
            => MaxRadius * VaultUtils.EaseOutQuad(1f - Projectile.timeLeft / (float)LifeTicks);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
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
            if (VaultUtils.isServer) {
                return;
            }
            //沿环迸出的音符雨：波前扫过之处音符四散
            float r = RadiusNow;
            if (Projectile.timeLeft % 3 == 0 && r > 20f) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 rim = Projectile.Center + ang.ToRotationVector2() * r;
                PRTLoader.NewParticle<PRT_Note>(rim, ang.ToRotationVector2() * 1.4f - Vector2.UnitY * 0.6f,
                    Main.rand.NextBool() ? GsMagicalHarp.HarpViolet : GsMagicalHarp.HarpBlue,
                    Main.rand.NextFloat(0.32f, 0.5f))?.Configure(Main.rand.Next(20, 32));
            }
            Lighting.AddLight(Projectile.Center, GsMagicalHarp.HarpViolet.ToVector3() * (0.5f + 0.4f * HeatFrac));
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //环带判定：只有波前扫过的一圈能打中（带宽 46px），与可见环同源
            float r = RadiusNow;
            if (r < 12f) {
                return false;
            }
            float nx = MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right);
            float ny = MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom);
            float dist = new Vector2(nx - Projectile.Center.X, ny - Projectile.Center.Y).Length();
            return dist <= r && dist >= r - 46f;
        }

        public override bool PreDraw(ref Color lightColor) {
            //和弦环：参数化冲击环 + 内圈残响弱环（identity 定相错噪声）
            float r = RadiusNow;
            if (r < 6f) {
                return false;
            }
            float fade = MathHelper.Clamp(Projectile.timeLeft / (float)LifeTicks, 0f, 1f);
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, r, 10f + 6f * HeatFrac,
                Color.White, GsMagicalHarp.HarpViolet, GsMagicalHarp.HarpDeep,
                0.85f * fade, innerGlow: 0.3f, timeSeed: Projectile.identity * 0.37f);
            //残响弱环：慢半拍的第二圈
            if (r > 40f) {
                ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, r * 0.62f, 6f,
                    GsMagicalHarp.HarpBlue, GsMagicalHarp.HarpDeep, GsMagicalHarp.HarpDeep,
                    0.4f * fade, innerGlow: 0.15f, timeSeed: Projectile.identity * 0.37f + 3.1f);
            }
            return false;
        }
    }
}
