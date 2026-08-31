using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph
{
    /// <summary>
    /// 暗影焰咒娃娃重铸：巫毒大咒。材质身份：巫毒暗焰（缝偶针脚间渗出的影火）。<br/>
    /// ①A 形态 rider：暗影焰触须拖深紫影火，命中钉下「咒偶印」10 秒，
    /// 被印之敌受本武器伤害 +8%；<br/>
    /// ②B 形态（蓄力 55t）「大咒」：光标处钉下咒偶烙印域，域内每 30t 从阵心
    /// 向至多两个敌人抽打暗影焰触须；③施法有举偶起手与族蓄力读数（紫黑色板）
    /// </summary>
    internal class GsShadowFlameHexDoll : GsMorphScheme
    {
        public override int TargetItemID => ItemID.ShadowFlameHexDoll;

        protected override string GsDescFallback =>
            "Reforged: shadowflame tendrils pin a hex brand into whatever they touch; branded foes take more from this doll" +
            "\nHold right click to charge the Grand Hex: a sigil at your cursor that lashes tendrils at intruders";

        protected override int ChargeTicksB => 55;
        protected override float ChargeManaMult => 2.0f;
        protected override Color ChargeColor => HexMain;
        protected override float BaseDamageMult => 1.05f;

        internal static readonly Color HexBright = new(216, 140, 255);
        internal static readonly Color HexMain = new(150, 70, 200);
        internal static readonly Color HexDeep = new(58, 20, 92);

        /// <summary>咒偶印的伤害加成</summary>
        private const float HexBonus = 0.08f;

        /// <summary>原版暗影焰触须弹类型</summary>
        internal static int TendrilType => ContentSamples.ItemsByType[ItemID.ShadowFlameHexDoll].shoot;

        //==================== 动画法：举偶 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //举偶：出手瞬间咒偶举高 4px 微斜，随动画进度垂回（绝对剖面 0.14·p，差分施加防累积漂移；本偶动画三发，中途 snap 由差分清账）
            float n = player.itemAnimationMax;
            float progress = player.itemAnimation / n;
            player.itemLocation += new Vector2(-player.direction * 1f, -4f) * progress;
            GsMagicKickMath.ApplyKickDiff(player, 0.14f * progress, 0.14f * ((player.itemAnimation + 1) / n));
        }

        public override void GsUseAnimation(Item item, Player player) {
            if (VaultUtils.isServer) {
                return;
            }
            //起手影火：偶身针脚渗出的一撮暗焰
            Vector2 tip = player.MountedCenter + new Vector2(player.direction * 14f, -10f);
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Light>(tip + Main.rand.NextVector2Circular(5f, 5f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.1f), HexMain,
                    Main.rand.NextFloat(0.07f, 0.11f))?.Configure(Main.rand.Next(12, 18), 0.7f);
            }
            Lighting.AddLight(tip, HexMain.ToVector3() * 0.3f);
        }

        //==================== B 形态：大咒 ====================

        protected override void FireMorphB(Item item, Player player) {
            SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.9f, Pitch = -0.3f }, Main.MouseWorld);
            int damage = Math.Max(1, (int)(player.GetWeaponDamage(item) * 0.5f));
            SpawnMorph(player, item, Main.MouseWorld, Vector2.Zero,
                ModContent.ProjectileType<GsShadowFlameHexSigilProj>(), damage, 2f, KindB);
        }

        //==================== A 形态 rider ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != TendrilType || VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(proj.Center, HexMain.ToVector3() * 0.2f);
            //深紫影火尾迹：缝隙里漏出来的火
            if (proj.timeLeft % 4 == 0) {
                PRTLoader.NewParticle<PRT_Light>(proj.Center - proj.velocity * 0.5f + Main.rand.NextVector2Circular(4f, 4f),
                    -proj.velocity * 0.05f, Main.rand.NextBool() ? HexMain : HexDeep,
                    Main.rand.NextFloat(0.06f, 0.1f))?.Configure(Main.rand.Next(10, 18), 0.65f);
            }
        }

        public override void GsProjModifyHitNPC(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            //咒偶印消费：被印之敌受本武器伤害 +8%（印是攻击方本地量，命中链天然攻击方端）
            GsShadowFlameHexNPC hex = target.GetGlobalNPC<GsShadowFlameHexNPC>();
            if (Main.GameUpdateCount < hex.HexUntil) {
                modifiers.FinalDamage *= 1f + HexBonus;
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.type != TendrilType) {
                return;
            }
            if (!VaultUtils.isServer) {
                //命中反馈：影火四漏
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Light>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                        Main.rand.NextVector2Circular(1.4f, 1.4f) - new Vector2(0f, 0.8f),
                        i % 2 == 0 ? HexMain : HexBright, Main.rand.NextFloat(0.07f, 0.12f))
                        ?.Configure(Main.rand.Next(12, 20), 0.7f);
                }
            }
            if (!proj.IsOwnedByLocalPlayer()) {
                return;
            }
            target.AddBuff(BuffID.ShadowFlame, 240);
            //钉印：咒偶印 10 秒
            target.GetGlobalNPC<GsShadowFlameHexNPC>().HexUntil = Main.GameUpdateCount + 600;
        }
    }

    /// <summary>
    /// 咒偶印（攻击方本地量：命中钩子只在攻击方端执行，加成只在攻击方端结算）
    /// </summary>
    internal class GsShadowFlameHexNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>咒偶印失效时刻</summary>
        internal uint HexUntil;

        public override void DrawEffects(NPC npc, ref Color drawColor) {
            //印记体表可见：皮下渗出的暗紫影火（印只在攻击方端存在，个人读数合法）
            if (Main.GameUpdateCount >= HexUntil || Main.dedServ) {
                return;
            }
            drawColor = Color.Lerp(drawColor, GsShadowFlameHexDoll.HexMain, 0.14f);
            if (Main.rand.NextBool(6)) {
                PRTLoader.NewParticle<PRT_Light>(
                    npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.4f, 0.9f),
                    GsShadowFlameHexDoll.HexBright, Main.rand.NextFloat(0.05f, 0.09f))
                    ?.Configure(Main.rand.Next(12, 18), 0.7f);
            }
        }
    }

    /// <summary>
    /// 咒偶烙印域：大咒钉在光标处的暗紫法阵，寿 5 秒。
    /// owner 端每 30t 从阵心向域内至多两个敌人抽打暗影焰触须（0.5 倍原版触须）；
    /// 自绘三层差速旋辉法阵 + 阵缘咒星（identity 定相，绘制零随机）
    /// </summary>
    internal class GsShadowFlameHexSigilProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithMagicMorph";

        private const int LifeTicks = 300;
        internal const float DomainRadius = 260f;

        private ref float Timer => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.timeLeft = LifeTicks;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Timer++;
            Lighting.AddLight(Projectile.Center, GsShadowFlameHexDoll.HexMain.ToVector3() * 0.5f);

            //owner 端抽打：每 30t 锁定域内至多两个敌人，从阵心甩出触须
            if (Projectile.IsOwnedByLocalPlayer() && Timer >= 20f && Timer % 30 == 0) {
                int lashed = 0;
                int lashDamage = Math.Max(1, (int)(Projectile.damage));
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (lashed >= 2) {
                        break;
                    }
                    if (!npc.CanBeChasedBy()
                        || Vector2.DistanceSquared(npc.Center, Projectile.Center) > DomainRadius * DomainRadius) {
                        continue;
                    }
                    Vector2 dir = (npc.Center - Projectile.Center).SafeNormalize(-Vector2.UnitY);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, dir * 11f,
                        GsShadowFlameHexDoll.TendrilType, lashDamage, 2f, Projectile.owner);
                    lashed++;
                }
                if (lashed > 0 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item104 with { Volume = 0.5f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
                }
            }

            if (VaultUtils.isServer) {
                return;
            }
            //阵内影火缓升
            if (Timer % 5 == 0) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + ang.ToRotationVector2() * Main.rand.NextFloat(DomainRadius * 0.2f, DomainRadius * 0.85f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.0f),
                    Main.rand.NextBool() ? GsShadowFlameHexDoll.HexMain : GsShadowFlameHexDoll.HexDeep,
                    Main.rand.NextFloat(0.06f, 0.1f))?.Configure(Main.rand.Next(14, 24), 0.7f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //三层差速旋辉法阵：外环慢旋、中环逆旋、内核呼吸（A=0 加色，identity 定相）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            float grow = VaultUtils.EaseOutCubic(MathHelper.Clamp(Timer / 16f, 0f, 1f));
            float fade = MathHelper.Clamp(Projectile.timeLeft / 22f, 0f, 1f);
            float r = DomainRadius * grow * fade;
            if (r < 8f) {
                return false;
            }
            Vector2 basePos = Projectile.Center - Main.screenPosition;
            float t = Main.GlobalTimeWrappedHourly;
            float seed = Projectile.identity * 0.43f;

            //底垫大辉
            Main.EntitySpriteDraw(glow, basePos, null,
                GsShadowFlameHexDoll.HexDeep with { A = 0 } * (0.4f * fade), 0f,
                glow.Size() / 2f, r * 2.2f / glow.Width, SpriteEffects.None, 0);
            //外环咒星：八枚沿环慢旋
            for (int i = 0; i < 8; i++) {
                float ang = t * 0.7f + seed + MathHelper.TwoPi * i / 8f;
                Main.EntitySpriteDraw(star, basePos + ang.ToRotationVector2() * r * 0.92f, null,
                    GsShadowFlameHexDoll.HexMain with { A = 0 } * (0.6f * fade), ang + MathHelper.PiOver2,
                    star.Size() / 2f, 0.14f, SpriteEffects.None, 0);
            }
            //中环逆旋：五枚亮星
            for (int i = 0; i < 5; i++) {
                float ang = -t * 1.3f + seed + MathHelper.TwoPi * i / 5f;
                Main.EntitySpriteDraw(star, basePos + ang.ToRotationVector2() * r * 0.55f, null,
                    GsShadowFlameHexDoll.HexBright with { A = 0 } * (0.5f * fade), ang,
                    star.Size() / 2f, 0.1f, SpriteEffects.None, 0);
            }
            //内核呼吸
            float pulse = 0.8f + 0.2f * MathF.Sin(t * 5f + seed);
            Main.EntitySpriteDraw(glow, basePos, null,
                GsShadowFlameHexDoll.HexBright with { A = 0 } * (0.55f * fade * pulse), 0f,
                glow.Size() / 2f, 0.5f * pulse, SpriteEffects.None, 0);
            return false;
        }
    }
}
