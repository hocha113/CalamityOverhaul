using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Throwing.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Throwing
{
    /// <summary>
    /// 扔酒杖:本族唯一走弹药钩子的武器(非消耗武器,消耗麦芽酒)。
    /// 三成不耗酒;醉拳节奏:命中叠酒意(+8%/层,5 层,5s 续窗);
    /// 每第 6 投掷出酒桶爆(主伤 +35%,命中再起 120px 酒沫脉冲,非 Boss 迷醉)
    /// </summary>
    internal class GsAleTosser : GsThrowScheme
    {
        public override int TargetItemID => ItemID.AleThrowingGlove;
        protected override string GsDescFallback =>
            "Reforged: 30% chance to not consume ale\nHits stack Tipsy, +8% damage each up to 5; every 6th throw is a keg blast that dazes non-boss foes";
        /// <summary>MarkData 酒桶爆码</summary>
        private const float KegCode = 1f;

        //酒意与投掷计数(myPlayer 契约:命中钩子与射击链都在 owner 端)
        private int aleStacks;
        private uint aleUntil;
        private int throwCount;
        private bool pendingKeg;

        /// <summary>酒意加伤走伤害行(结算在 owner 端取 owner 的层数)</summary>
        protected override float DamageMul {
            get {
                int stacks = Main.GameUpdateCount <= aleUntil ? aleStacks : 0;
                return 1.05f + 0.08f * stacks;
            }
        }

        public override bool? GsCanConsumeAmmo(Item weapon, Item ammo, Player player) {
            //三成不耗酒(弹药消耗 owner 端结算,客户端权威库存)
            if (ammo.type == ItemID.Ale && Main.rand.NextFloat() < 0.3f) {
                return false;
            }
            return null;
        }

        protected override void GsThrowModifyShoot(Item item, Player player, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            throwCount++;
            pendingKeg = throwCount % 6 == 0;
            if (pendingKeg) {
                damage = (int)(damage * 1.35f);
            }
        }

        protected override void GsThrowOnSpawn(Projectile proj, GodSmithProjRouter router, GsThrowProjState st) {
            if (pendingKeg) {
                pendingKeg = false;
                router.MarkData = KegCode;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.9f, Pitch = -0.5f }, proj.Center);
                }
            }
        }

        protected override void GsThrowOnHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone,
            GodSmithProjRouter router, GsThrowProjState st) {
            if (proj.owner != Main.myPlayer || !st.IsPrimary || target.friendly) {
                return;
            }
            //酒意记账
            aleStacks = Main.GameUpdateCount <= aleUntil ? System.Math.Min(5, aleStacks + 1) : 1;
            aleUntil = Main.GameUpdateCount + 300;
            //酒桶爆:命中处酒沫脉冲(每瓶一次)
            if (router.MarkData == KegCode && !st.Latch) {
                st.Latch = true;
                Projectile.NewProjectile(proj.GetSource_FromThis(), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsBurstProj>(), (int)(proj.damage * 0.65f), 5f,
                    proj.owner, 120f, GsBurstProj.FxConfuse);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item107 with { Volume = 0.7f, Pitch = 0.2f }, target.Center);
                }
            }
        }
    }
}
