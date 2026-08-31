using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords
{
    /// <summary>
    /// 铁短剑重铸「铁壁反刺」。<br/>
    /// 材质：厚脊冷锻铁刃，收势横持如小盾。签名行为：①收刀相持有格挡帧，期间被击减伤三成
    /// ②格挡吃下一击即点亮「反刺就绪」一秒，就绪期下一刺必暴击 ③格挡成功金铁交鸣，就绪刃身灼亮
    /// </summary>
    internal class GsIronShortsword : GsShortswordScheme
    {
        public override int TargetItemID => ItemID.IronShortsword;

        protected override string GsDescFallback =>
            "Reforged: the recovery stance holds a guard that blunts incoming blows by 30%;" +
            "\nblock a hit to ready a riposte, and your next thrust within a second strikes true";

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

            //格挡成功反馈：金铁交鸣 + 钢屑迸溅
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.7f, Pitch = 0.35f }, Player.Center);
                for (int i = 0; i < 6; i++) {
                    Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 7f);
                    Color c = Main.rand.NextBool() ? GsIronShortswordHeld.SteelBright : GsIronShortswordHeld.RiposteHot;
                    PRTLoader.NewParticle<PRT_Spark>(Player.Center, vel, c, Main.rand.NextFloat(0.35f, 0.6f))
                        ?.Configure(true, Main.rand.Next(12, 20));
                }
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

        //冷锻铁盾色板（与铁宽剑同宗但更冷灰，反刺灼橙做区分）
        internal static readonly Color SteelBright = new(214, 220, 230);
        internal static readonly Color SteelMain = new(152, 160, 172);
        internal static readonly Color RiposteHot = new(255, 152, 78);

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

        protected override Color EdgeColor => SteelBright;
        protected override Color CoreColor => RiposteHot;

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
            //反刺命中升级反馈：重音 + 灼橙闪
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.55f, Pitch = 0.2f }, target.Center);
            PRTLoader.NewParticle<PRT_Light>(Vector2.Lerp(TipPos, target.Center, 0.5f), Vector2.Zero,
                RiposteHot, 0.26f)?.Configure(11, 0.85f);
        }

        /// <summary>命中反馈：冷灰钢屑，反刺换灼橙</summary>
        protected override void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            Vector2 pos = Vector2.Lerp(TipPos, target.Center, 0.5f);
            int count = riposteThrust ? 9 : 5;
            for (int i = 0; i < count; i++) {
                Vector2 vel = stabUnit.RotatedByRandom(0.55) * Main.rand.NextFloat(3.5f, 8f);
                Color c = riposteThrust
                    ? (Main.rand.NextBool() ? RiposteHot : SteelBright)
                    : (Main.rand.NextBool(3) ? SteelMain : SteelBright);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, c, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(true, Main.rand.Next(12, 20));
            }
            if (!CWRLoad.NPCValue.ISTheofSteel(target)) {
                Dust d = Dust.NewDustPerfect(pos, DustID.Blood,
                    stabUnit.RotatedByRandom(0.8) * Main.rand.NextFloat(1.5f, 3.5f), 100, default, Main.rand.NextFloat(0.9f, 1.2f));
                d.noGravity = Main.rand.NextBool();
            }
        }

        /// <summary>就绪期刃身灼亮 + 反刺出刺常亮（owner 端本地表现）</summary>
        protected override float ExtraGlowStrength() {
            if (riposteThrust) {
                return 0.40f;
            }
            return Owner.whoAmI == Main.myPlayer && ModPlayerState.riposteFrames > 0 ? 0.30f : 0f;
        }

        /// <summary>格挡帧可视化：收刀相刃前横一道冷灰盾光（定值，无随机；各端按相位同步可见）。
        /// 注意基类 FanFade 在收刀相衰减，格挡光用自己的收势进度计淡出</summary>
        protected override void DrawOverBlade(SpriteBatch sb) {
            if (CurrentPhase != PhaseRecover) {
                return;
            }
            Texture2D streak = CWRAsset.LightShot?.Value;
            if (streak == null) {
                return;
            }
            float recoverT = MathHelper.Clamp(
                (Elapsed - WindupFrames - ThrustFrames - DwellFrames) / RecoverFrames, 0f, 1f);
            float guardFade = 1f - recoverT * recoverT;//盾面随收势入尾才撤
            if (guardFade <= 0.05f) {
                return;
            }
            //盾光垂直于刺向，横在刀身中段
            Vector2 at = Hand + stabUnit * (holdout + BladeLength * 0.55f) - Main.screenPosition;
            float rot = stabUnit.ToRotation() + MathHelper.PiOver2;
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + Projectile.whoAmI);
            Color c = SteelBright with { A = 0 } * (0.40f * guardFade * pulse);
            sb.Draw(streak, at, null, c, rot, streak.Size() / 2f,
                new Vector2(46f / streak.Width, 0.16f), SpriteEffects.None, 0f);
        }
    }
}
