using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.UI;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsEarly
{
    /// <summary>
    /// 雷筒「獠牙合口」：丛林硬木双管·兽牙锁扣。<br/>
    /// ①收束弹道：每管 4 粒铅弹朝准星处收拢交汇，散布随距离闭合而非散开；
    /// ②交汇点「咬合」：弹粒在准星处合口成兽口一击（延时小范围咬合爆，瞄得准就多咬一口）；
    /// ③折管装填：双壳齐飞、逐管落膛两响；完美装填下匣每管 +1 粒。<br/>
    /// 后坐 2px（末管 3.5px），带角度上踢。<br/>
    /// 账目：每管 4 粒 ×0.9 对原版均值 3.5 粒（×1.03），咬合 ×0.55 为瞄准奖励，
    /// 弹匣占空比 0.71 → 合计约 108%（瞄准满收益 115%，待游戏内标定）
    /// </summary>
    internal class GsBoomstick : GsMagazineScheme
    {
        public override int TargetItemID => ItemID.Boomstick;

        protected override string GsDescFallback =>
            "Reforged: each barrel throws 4 slugs that converge on your cursor instead of spraying.\n" +
            "Where they cross, the jaws snap shut: a delayed bite tears whatever stands at the aim point.\n" +
            "Break open to reload both shells; nail the sweet spot for +1 slug per barrel";

        public override int MagSize => 2;
        public override int ReloadTicks => 40;
        public override GsReloadStyle Style => GsReloadStyle.Break;
        protected override int ReloadCueCount => 2;
        protected override float GetRecoil(bool lastRound) => lastRound ? 3.5f : 2f;

        /// <summary>完美奖励改整匣：每管 +1 粒</summary>
        protected override void OnPerfectReload(Item item, Player player, GsGunsEarlyPlayer mp) => mp.perfectMag = true;

        protected override bool? FireNormalRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => FireBarrel(player, mp, source, position, velocity, type, damage, knockback, last: false);

        protected override bool? FireLastRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => FireBarrel(player, mp, source, position, velocity, type, damage, knockback, last: true);

        /// <summary>
        /// 一管收束弹：弹粒从枪口两侧错位出膛、弹道朝准星闭合；
        /// 同时在交汇点埋一记延时咬合（末管咬得更狠）
        /// </summary>
        private bool? FireBarrel(Player player, GsGunsEarlyPlayer mp, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback, bool last) {
            pendingMark = last ? 2f : 1f;
            float speed = velocity.Length();
            Vector2 aimUnit = velocity.SafeNormalize(Vector2.UnitX * player.direction);
            Vector2 sideUnit = new(-aimUnit.Y, aimUnit.X);

            //交汇点=准星，夹到 120~640px 的合口行程
            Vector2 focus = Main.MouseWorld;
            float dist = MathHelper.Clamp(Vector2.Distance(position, focus), 120f, 640f);
            focus = position + aimUnit * dist;

            int count = (last ? 5 : 4) + (mp.perfectMag ? 1 : 0);
            int pelletDamage = Math.Max(1, (int)(damage * 0.9f));
            for (int i = 0; i < count; i++) {
                //两侧错位出膛，弹道向焦点闭合
                float lane = (i - (count - 1) * 0.5f) * 7f;
                Vector2 spawn = position + sideUnit * lane;
                Vector2 vel = (focus - spawn).SafeNormalize(aimUnit) * speed * Main.rand.NextFloat(0.96f, 1.04f);
                Projectile.NewProjectile(source, spawn, vel, type, pelletDamage,
                    knockback * (last ? 1.5f : 1f), player.whoAmI);
            }

            //咬合：延时 = 弹粒抵达焦点的帧数
            int delay = Math.Max(4, (int)(dist / Math.Max(4f, speed)));
            Projectile.NewProjectile(source, focus, Vector2.Zero,
                ModContent.ProjectileType<GsBoomstickBiteProj>(),
                Math.Max(1, (int)(damage * (last ? 0.75f : 0.55f))), knockback,
                player.whoAmI, delay, last ? 1f : 0f);

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item36 with {
                    Volume = last ? 0.95f : 0.7f,
                    Pitch = last ? -0.3f : -0.05f
                }, position);
                Vector2 muzzle = position + aimUnit * 10f;
                for (int i = 0; i < (last ? 4 : 2); i++) {
                    PRTLoader.NewParticle<PRT_Smoke>(muzzle,
                        aimUnit.RotatedByRandom(0.3) * Main.rand.NextFloat(1.5f, 3.2f),
                        new Color(172, 160, 140), Main.rand.NextFloat(0.06f, 0.1f))
                        ?.Configure(Main.rand.Next(16, 24), 0.4f, 0.02f);
                }
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(muzzle,
                        aimUnit.RotatedByRandom(0.35) * Main.rand.NextFloat(3f, 7f),
                        GameModeTheme.GodSmithEmber, Main.rand.NextFloat(0.25f, 0.42f))?.Configure(false, 10);
                }
            }
            return false;
        }

        //==================== 折管两响装填 ====================

        protected override void OnReloadStart(Item item, Player player, GsGunsEarlyPlayer mp) {
            if (VaultUtils.isServer) {
                return;
            }
            //折开：双壳齐飞
            SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.7f, Pitch = -0.35f }, player.Center);
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_ProcChip>(player.Center + new Vector2(player.direction * 8f, -3f),
                    new Vector2(-player.direction * Main.rand.NextFloat(1.2f, 2.2f), -Main.rand.NextFloat(2.2f, 3.4f)),
                    new Color(196, 96, 60), Main.rand.NextFloat(0.55f, 0.7f))
                    ?.Configure(new Color(255, 200, 130), Main.rand.Next(26, 36), 0.5f);
            }
        }

        protected override void OnReloadCue(Item item, Player player, GsGunsEarlyPlayer mp, int index, int total) {
            if (!VaultUtils.isServer) {
                //逐管落膛两响
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.75f, Pitch = -0.3f + 0.2f * index }, player.Center);
            }
        }

        protected override void OnReloadComplete(Item item, Player player, GsGunsEarlyPlayer mp, bool perfect) {
            if (!VaultUtils.isServer) {
                //合膛重扣
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.8f, Pitch = 0.3f }, player.Center);
            }
        }

        //==================== 后坐姿态：位移 + 角度上踢 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            float progress = player.itemAnimation / (float)player.itemAnimationMax;
            player.itemLocation -= new Vector2(player.direction, 0f) * (3f * progress);
            player.itemRotation -= player.direction * 0.09f * progress;
        }

        //==================== 收束弹表现 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (router.MarkData < 1f || VaultUtils.isServer) {
                return;
            }
            if (proj.timeLeft % 3 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(proj.Center - proj.velocity * 0.3f,
                    -proj.velocity * 0.04f,
                    router.MarkData >= 2f ? GameModeTheme.GodSmithEmber : new Color(214, 196, 150),
                    Main.rand.NextFloat(0.14f, 0.24f))?.Configure(false, 7);
            }
        }
    }

    /// <summary>
    /// 咬合弹：埋伏在准星交汇点的延时兽口。ai[0]=咬合延时帧，ai[1]=末管重咬旗标。<br/>
    /// 等待期双颚渐合（自绘上下弦月牙），到点合口：小范围判定 + 咬合爆。
    /// 绘制以 identity 定相不掷随机
    /// </summary>
    internal class GsBoomstickBiteProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private static readonly Color FangIvory = new(232, 220, 190);
        private static readonly Color FangDeep = new(150, 128, 92);

        private int Delay => (int)Projectile.ai[0];
        private bool Heavy => Projectile.ai[1] > 0f;
        private float Seed => Projectile.identity * 0.6180f % 1f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 60;
        }

        /// <summary>合口前不判定，合口帧一次性张开判定圈</summary>
        public override bool? CanDamage() => Projectile.localAI[0] >= Delay ? null : false;

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            Projectile.localAI[0]++;
            if (Projectile.localAI[0] == Delay) {
                //合口：撑开判定 + 咬合演出
                int size = Heavy ? 84 : 64;
                Projectile.Resize(size, size);
                Projectile.timeLeft = 6;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.7f, Pitch = -0.25f }, Projectile.Center);
                    PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero,
                        FangIvory * 0.85f, (Heavy ? 0.2f : 0.15f))?.Configure(Vector2.One, 0f, 1.5f, 12);
                    for (int i = 0; i < 6; i++) {
                        PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                            Main.rand.NextVector2Circular(4.5f, 4.5f), FangIvory,
                            Main.rand.NextFloat(0.28f, 0.45f))?.Configure(false, 11);
                    }
                }
            }
            else if (Projectile.localAI[0] > Delay + 6) {
                Projectile.Kill();
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Heavy) {
                target.AddBuff(BuffID.Bleeding, 180);
            }
            if (!VaultUtils.isServer) {
                //兽口撕咬命中：牙屑迸溅
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_ProcSpark>(target.Center,
                        Main.rand.NextVector2Circular(3.5f, 3f) - Vector2.UnitY,
                        FangIvory, Main.rand.NextFloat(0.3f, 0.5f));
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null || Delay <= 0) {
                return false;
            }
            //等待期：上下双颚弦月渐合，合口瞬白闪
            float t = MathHelper.Clamp(Projectile.localAI[0] / Delay, 0f, 1f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float gape = MathHelper.Lerp(26f, 4f, t * t);
            float wob = MathF.Sin(Projectile.localAI[0] * 0.4f + Seed * 8f) * 1.5f;

            Color jaw = Color.Lerp(FangDeep, FangIvory, t) * (0.25f + 0.5f * t);
            jaw.A = 0;
            Vector2 jawScale = new(0.36f * (Heavy ? 1.25f : 1f), 0.09f);
            Main.EntitySpriteDraw(glow, drawPos - new Vector2(0f, gape + wob), null, jaw,
                0.16f, glow.Size() / 2f, jawScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos + new Vector2(0f, gape + wob), null, jaw,
                -0.16f, glow.Size() / 2f, jawScale, SpriteEffects.FlipVertically, 0);

            if (Projectile.localAI[0] >= Delay) {
                Color snap = Color.White * 0.85f;
                snap.A = 0;
                Main.EntitySpriteDraw(glow, drawPos, null, snap, 0f, glow.Size() / 2f,
                    new Vector2(0.5f, 0.2f) * (Heavy ? 1.3f : 1f), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
