using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords
{
    /// <summary>
    /// 金短剑重铸「鎏金掠夺」。<br/>
    /// 材质：鎏金镀刃，出鞘即生辉。签名行为：①命中 25% 概率从目标身上迸出小额钱币
    /// ②击杀必迸一把 ③钱币脆响的富贵反馈
    /// </summary>
    internal class GsGoldShortsword : GsShortswordScheme
    {
        public override int TargetItemID => ItemID.GoldShortsword;

        protected override string GsDescFallback =>
            "Reforged: gilded edge that shakes loose coins, one hit in four rattles change from the wound;\nkills always spill a handful";
        protected override int HeldProjType => ModContent.ProjectileType<GsGoldShortswordHeld>();

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.12f;//掠夺经济收益在机制端，底伤只补一成出头
    }

    /// <summary>
    /// 金短剑手持突刺：命中掠币（owner 端 rand + IsOwnedByLocalPlayer 守门，
    /// noGrabDelay 常规掉落随原版物品同步过线）；假人/雕像怪不结算
    /// </summary>
    internal class GsGoldShortswordHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.GoldShortsword;

        protected override float WindupFrames => 3f;
        protected override float ThrustFrames => 4f;
        protected override float DwellFrames => 2f;
        protected override float RecoverFrames => 6f;
        protected override float PullbackDist => 10f;
        protected override float StabReach => 32f;
        protected override float BladeLength => 44f;
        protected override float ThrustEasePower => 2.6f;
        protected override int HitstopFrames => 2;
        protected override float LeanAmp => 0.030f;
        protected override float ThrustPitch => 0.15f;

        /// <summary>掠夺结算护栏：假人/雕像怪/召唤物不出币</summary>
        private static bool ValidPlunderTarget(NPC target)
            => target.active && !target.friendly && target.lifeMax > 5
               && target.type != NPCID.TargetDummy && !target.SpawnedFromStatue;

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            if (!firstOnTarget || !Projectile.IsOwnedByLocalPlayer() || !ValidPlunderTarget(target)) {
                return;
            }
            bool kill = target.life <= 0;
            //击杀必迸，普通命中 25% 概率（owner 端 rand）；币值护栏远低于 1 银
            if (!kill && !Main.rand.NextBool(4)) {
                return;
            }
            int stack = kill ? Main.rand.Next(8, 15) : Main.rand.Next(2, 6);
            Item.NewItem(Projectile.GetSource_FromThis(), target.Hitbox, ItemID.CopperCoin, stack, noGrabDelay: true);

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Coins with { Volume = kill ? 0.6f : 0.4f, Pitch = kill ? -0.1f : 0.2f }, target.Center);
            }
        }
    }
}
