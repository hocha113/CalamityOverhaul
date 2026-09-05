using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords
{
    /// <summary>
    /// 铁短剑重铸「铁壁反刺」。<br/>
    /// 材质：厚脊冷锻铁刃，收势横持如小盾。签名行为：①收刀相持有格挡帧，期间被击减伤三成
    /// ②格挡吃下一击即点亮「反刺就绪」一秒，就绪期下一刺必暴击 ③格挡成功金铁交鸣
    /// </summary>
    internal class GsIronShortsword : GsShortswordScheme
    {
        public override int TargetItemID => ItemID.IronShortsword;

        protected override string GsDescFallback =>
            "Reforged: the recovery stance holds a guard that blunts incoming blows by 30%;\nblock a hit to ready a riposte, and your next thrust within a second strikes true";
        protected override int HeldProjType => ModContent.ProjectileType<GsIronShortswordHeld>();

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.15f;//格挡减伤 + 反刺必暴击是主要收益，底伤只小补

        public override void GsHoldItem(Item item, Player player) {
            base.GsHoldItem(item, player);
            //手持铁短剑时点亮 ModPlayer 的启用位（模式关闭/换武器即回落，零footprint）
            if (player.whoAmI == Main.myPlayer) {
                player.GetModPlayer<GsIronShortswordPlayer>().holdingFrames = 2;
            }
        }
    }

    /// <summary>
    /// 铁短剑私有每玩家状态：格挡窗与反刺就绪。
    /// 受击结算是被击玩家 owner-local（镜像 ShieldGeneratorPlayer 契约），全部字段只在 myPlayer 端有意义
    /// </summary>
    internal class GsIronShortswordPlayer : ModPlayer
    {
        /// <summary>格挡窗剩余帧，held 的收刀相每帧续写</summary>
        internal int guardFrames;
        /// <summary>反刺就绪剩余帧（60 = 1 秒），格挡吃下一击时点亮</summary>
        internal int riposteFrames;
        /// <summary>手持续写位，用于把状态限制在真正持剑期间</summary>
        internal int holdingFrames;

        public override void PostUpdateMiscEffects() {
            if (guardFrames > 0) {
                guardFrames--;
            }
            if (riposteFrames > 0) {
                riposteFrames--;
            }
            if (holdingFrames > 0) {
                holdingFrames--;
            }
            else {
                //收起武器即清账，防换装备带走就绪
                riposteFrames = 0;
                guardFrames = 0;
            }
        }

        public override void ModifyHurt(ref Player.HurtModifiers modifiers) {
            //受击结算 owner-local：只在被击玩家自己的端上减伤，其余端沿用广播的最终伤害
            if (Player.whoAmI != Main.myPlayer || guardFrames <= 0 || !GameModeSystem.GodSmithActive) {
                return;
            }
            modifiers.FinalDamage *= 0.70f;
            riposteFrames = 60;

            //格挡成功反馈：金铁交鸣
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.7f, Pitch = 0.35f }, Player.Center);
            }
        }
    }

    /// <summary>
    /// 铁短剑手持突刺：收刀相（PhaseRecover）即格挡帧，每帧把格挡窗写进 ModPlayer；
    /// 反刺就绪期出刺必暴击，命中升级反馈
    /// </summary>
    internal class GsIronShortswordHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.IronShortsword;

        protected override float WindupFrames => 3f;
        protected override float ThrustFrames => 4f;
        protected override float DwellFrames => 2f;
        //收刀相拉长：格挡帧就藏在这里，收得慢换来盾面
        protected override float RecoverFrames => 7f;
        protected override float PullbackDist => 10f;
        protected override float StabReach => 31f;
        protected override float BladeLength => 43f;
        protected override float ThrustEasePower => 2.6f;
        protected override int HitstopFrames => 2;
        protected override float LeanAmp => 0.032f;
        protected override float ThrustPitch => 0.10f;

        private GsIronShortswordPlayer ModPlayerState => Owner.GetModPlayer<GsIronShortswordPlayer>();
        /// <summary>本刺是否消费了反刺就绪（OnInit 定夺，全程锁定）</summary>
        private bool riposteThrust;

        protected override void OnInit() {
            //反刺消费：就绪期出刺即锁定必暴击（owner 端权威；远端只看普通刺 + 命中反馈）
            if (Owner.whoAmI == Main.myPlayer && ModPlayerState.riposteFrames > 0) {
                riposteThrust = true;
                ModPlayerState.riposteFrames = 0;
            }
        }

        /// <summary>收刀相 = 格挡帧：每帧把格挡窗续写进 ModPlayer（myPlayer 守门）</summary>
        protected override void OnTick(int phase) {
            if (phase == PhaseRecover && Owner.whoAmI == Main.myPlayer) {
                ModPlayerState.guardFrames = 2;
            }
        }

        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            if (riposteThrust) {
                modifiers.SetCrit();
            }
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            if (!riposteThrust || !firstOnTarget || VaultUtils.isServer) {
                return;
            }
            //反刺命中升级反馈：重音
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.55f, Pitch = 0.2f }, target.Center);
        }
    }
}
