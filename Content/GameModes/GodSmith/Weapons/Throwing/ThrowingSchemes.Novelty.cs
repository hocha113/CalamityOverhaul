using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Throwing.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Throwing
{
    /// <summary>
    /// 雪球:三成不消耗;连续投掷越掷越快(每发 +4% 初速,至 8 发);
    /// 每第 8 发攥成大雪团(2.5 倍伤,90px 冰爆挂霜火)。被雪球炮当弹药消耗时不介入
    /// </summary>
    internal class GsSnowball : GsThrowScheme
    {
        public override int TargetItemID => ItemID.Snowball;
        protected override string GsDescFallback =>
            "Reforged: 30% chance not to consume; each consecutive throw flies 4% faster, up to 8\nEvery 8th throw packs into a great snowball: 2.5x damage and a chilling 90px burst";

        protected override float NoConsumeChance => 0.30f;
        protected override float DamageMul => 1.20f;

        /// <summary>MarkData 大雪团码</summary>
        private const float BigCode = 1f;

        //投掷连招计数(myPlayer 契约:射击链 owner 端)
        private int throwStreak;
        private uint lastThrowTick;
        private bool pendingBig;

        protected override bool ConsumeGateOpen(Item item, Player player)
            //雪球炮拿雪球当弹药时手持的是炮,不介入
            => player.HeldItem.type == ItemID.Snowball;

        protected override void GsThrowModifyShoot(Item item, Player player, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            if (Main.GameUpdateCount - lastThrowTick > 120) {
                throwStreak = 0;
            }
            lastThrowTick = Main.GameUpdateCount;
            throwStreak++;
            velocity *= 1f + 0.04f * System.Math.Min(8, throwStreak - 1);
            pendingBig = throwStreak % 8 == 0;
            if (pendingBig) {
                damage = (int)(damage * 2.5f);
                velocity *= 0.92f;
            }
        }

        protected override void GsThrowOnSpawn(Projectile proj, GodSmithProjRouter router, GsThrowProjState st) {
            if (pendingBig) {
                pendingBig = false;
                router.MarkData = BigCode;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = -0.4f }, proj.Center);
                }
            }
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (router.MarkData != BigCode || VaultUtils.isServer) {
                return;
            }
            //大雪团:冰雾尾
            if (Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_Spark>(proj.Center - proj.velocity * 0.3f,
                    -proj.velocity * 0.04f, new Color(200, 235, 255),
                    Main.rand.NextFloat(0.2f, 0.34f))?.Configure(false, 12);
            }
        }

        protected override void GsThrowOnHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone,
            GodSmithProjRouter router, GsThrowProjState st) {
            if (proj.owner != Main.myPlayer || !st.IsPrimary || router.MarkData != BigCode || st.Latch) {
                return;
            }
            st.Latch = true;
            //大雪团碎裂:冰爆 + 霜火
            Projectile.NewProjectile(proj.GetSource_FromThis(), target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsBurstProj>(), (int)(proj.damage * 0.6f), 4f,
                proj.owner, 90f, GsBurstProj.FxFrost);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(14f, 14f),
                        Main.rand.NextVector2Circular(3.5f, 3.5f) - Vector2.UnitY,
                        new Color(215, 240, 255), Main.rand.NextFloat(0.28f, 0.46f))?.Configure(true, 18);
                }
            }
        }

        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            if (router.MarkData != BigCode) {
                return null;
            }
            //大雪团:放大 1.8 倍画(各端按 MarkData 一致呈现)
            Main.instance.LoadProjectile(proj.type);
            Texture2D tex = TextureAssets.Projectile[proj.type].Value;
            Main.EntitySpriteDraw(tex, proj.Center - Main.screenPosition, null, lightColor,
                proj.rotation, tex.Size() / 2f, 1.8f, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>臭鸡蛋:破裂留 4s 臭云,云内敌受所有来源 +8%;暴击返还,纯整活</summary>
    internal class GsRottenEgg : GsThrowScheme
    {
        public override int TargetItemID => ItemID.RottenEgg;
        protected override string GsDescFallback =>
            "Reforged: the egg bursts into a 4s stench cloud; foes inside take 8% more damage from everything\nCrits refund one egg. It is still a rotten egg";

        protected override float NoConsumeChance => 0.20f;
        protected override bool CritRefund => true;
        protected override float DamageMul => 2f;

        protected override void GsThrowOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            if (proj.owner != Main.myPlayer || router.LocalState is not GsThrowProjState { IsPrimary: true }) {
                return;
            }
            Projectile.NewProjectile(proj.GetSource_FromThis(), proj.Center, Vector2.Zero,
                ModContent.ProjectileType<GsZoneProj>(), 0, 0f, proj.owner,
                GsZoneProj.KindStench, 80f, 0f);
        }
    }

    /// <summary>纸飞机共享:可操控滑翔(缓转追准星),命中后回旋一圈可二次命中,暴击返还</summary>
    internal abstract class GsPaperPlaneScheme : GsThrowScheme
    {
        protected override float NoConsumeChance => 0.20f;
        protected override float RecoverOnTileChance => 0.25f;
        protected override float RecoverOnFadeChance => 0.25f;
        protected override bool CritRefund => true;
        protected override float DamageMul => 1.25f;

        protected override void GsThrowOnSpawn(Projectile proj, GodSmithProjRouter router, GsThrowProjState st) {
            //回旋二次命中:多一穿 + 本地免疫窗
            if (proj.penetrate > 0) {
                proj.penetrate++;
            }
            proj.usesLocalNPCImmunity = true;
            proj.localNPCHitCooldown = 30;
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (!router.IsMarked || proj.owner != Main.myPlayer) {
                return;
            }
            GsThrowProjState st = router.GetOrCreateState<GsThrowProjState>();
            //可操控滑翔:owner 端每帧向准星缓转 2 度,低频同步校正远端
            float speed = proj.velocity.Length();
            if (speed < 1f) {
                return;
            }
            float cur = proj.velocity.ToRotation();
            float want = (Main.MouseWorld - proj.Center).SafeNormalize(Vector2.UnitX).ToRotation();
            float diff = MathHelper.WrapAngle(want - cur);
            float step = MathHelper.Clamp(diff, -MathHelper.ToRadians(2f), MathHelper.ToRadians(2f));
            proj.velocity = (cur + step).ToRotationVector2() * speed;
            if (++st.Custom % 12 == 0) {
                proj.netUpdate = true;
            }
        }

        protected override void GsThrowOnHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone,
            GodSmithProjRouter router, GsThrowProjState st) {
            //命中即回旋掉头,可再咬一口(每机一次)
            if (proj.owner != Main.myPlayer || !st.IsPrimary || st.Latch || !proj.active) {
                return;
            }
            st.Latch = true;
            proj.velocity = proj.velocity.RotatedBy(Main.rand.NextBool() ? 2.6 : -2.6) * 0.9f;
            proj.netUpdate = true;
        }
    }

    /// <summary>纸飞机:见共享说明</summary>
    internal class GsPaperAirplaneA : GsPaperPlaneScheme
    {
        public override int TargetItemID => ItemID.PaperAirplaneA;
        protected override string GsDescFallback =>
            "Reforged: the plane banks gently toward your cursor; after striking it loops back for one more pass\nCrits refund one; strays can be reclaimed";
    }

    /// <summary>白纸飞机:同纸飞机,叠印更利(+4% 暴击)</summary>
    internal class GsPaperAirplaneB : GsPaperPlaneScheme
    {
        public override int TargetItemID => ItemID.PaperAirplaneB;
        protected override int CritAdd => 4;
        protected override string GsDescFallback =>
            "Reforged: the white plane banks toward your cursor and loops back after striking; +4% crit\nCrits refund one; strays can be reclaimed";
    }
}
