using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsHard
{
    /// <summary>
    /// 鳄鱼机关枪重铸：公认弱枪，吃 125% 目标线。<br/>
    /// [沼泽乱舞]：原版狂散保留，但每发子弹可在砖面弹跳一次（沼泽弹球），
    /// 乱枪在洞窟里横飞乱撞，散布劣势变覆盖优势。<br/>
    /// [死亡咬合]：4 连点射、散布收束 80%，链末发咬合撕裂挂 1.5 秒流血。<br/>
    /// 跳弹不换弹幕载体，特种子弹身份全保留
    /// </summary>
    internal class GsGatligator : GsFireModeScheme
    {
        public override int TargetItemID => ItemID.Gatligator;

        public override string GsFamily => "GunsHard";

        protected override string GsDescFallback =>
            "Reforged: right click to switch temper\n" +
            "Swamp Riot keeps the wild spray but every round skips once off blocks\n" +
            "Death Roll fires tight four round strings; the last bite tears a bleeding wound";

        /// <summary>咬合末发私有 flag</summary>
        private const int FlagBite = 1;

        /// <summary>沼泽绿</summary>
        private static readonly Color SwampGreen = new(126, 178, 82);

        /// <summary>本次射击为咬合末发的世界帧（打标窗口消费）</summary>
        private uint biteShotTick = uint.MaxValue;

        /// <summary>跳弹剩余次数（每弹幕本地状态包，各端确定性同源模拟）</summary>
        private class BounceState
        {
            public int Left = 1;
        }

        public override GsFireMode[] Modes { get; } = [
            new GsFireMode {
                Key = "ModeSwampRiot", EnName = "Swamp Riot",
                DamageMul = 1.15f,
            },
            new GsFireMode {
                Key = "ModeDeathRoll", EnName = "Death Roll",
                DamageMul = 1.10f, Converge = 0.8f,
                BurstCount = 4, BurstRest = 30,
            },
        ];

        protected override void GsGunModifyShoot(Item item, Player player, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback,
            GsFireMode mode, GsGunsHardPlayer mp) {
            //咬合链末发：BurstShots 尚未自增，等于 BurstCount-1 即本发是第 4 发
            if (mp.ModeIndex == 1 && mp.BurstShots == mode.BurstCount - 1) {
                biteShotTick = Main.GameUpdateCount;
                damage = (int)(damage * 1.20f);
            }
        }

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            GsGunsHardPlayer mp = Main.player[proj.owner].GetModPlayer<GsGunsHardPlayer>();
            router.MarkData = PackMark(mp.ModeIndex, biteShotTick == Main.GameUpdateCount ? FlagBite : 0);
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            //沼泽乱舞档：砖面跳弹一次。PostAI 先于本帧位移判定，预测撞砖提前反弹，
        //原版子弹不会触发 tileCollide 自灭；跳弹次数放本地状态包，各端按同步的位置速度同源模拟
            if (MarkModeOf(router.MarkData) != 0) {
                return;
            }
            BounceState state = router.GetOrCreateState<BounceState>();
            if (state.Left <= 0) {
                return;
            }
            Vector2 allowed = Collision.TileCollision(proj.position, proj.velocity, proj.width, proj.height);
            if (allowed == proj.velocity) {
                return;
            }
            state.Left--;
            if (allowed.X != proj.velocity.X) {
                proj.velocity.X = -proj.velocity.X * 0.9f;
            }
            if (allowed.Y != proj.velocity.Y) {
                proj.velocity.Y = -proj.velocity.Y * 0.9f;
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.35f, Pitch = 0.6f }, proj.Center);
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(proj.Center,
                        proj.velocity.SafeNormalize(Vector2.UnitX).RotatedByRandom(0.7) * Main.rand.NextFloat(1.5f, 4f),
                        SwampGreen, Main.rand.NextFloat(0.24f, 0.38f))?.Configure(true, Main.rand.Next(8, 14));
                }
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            //只在攻击方端执行：咬合末发撕裂（客户端 AddBuff 自动请求服务器落地）
            if (MarkFlagOf(router.MarkData) != FlagBite || target.friendly) {
                return;
            }
            target.AddBuff(BuffID.Bleeding, 90);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.5f, Pitch = 0.2f }, target.Center);
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(target.Center,
                        Main.rand.NextVector2Circular(2.5f, 1.5f) - Vector2.UnitY * Main.rand.NextFloat(0.5f, 2f),
                        new Color(186, 34, 40), Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(16, 26));
                }
            }
        }

        internal override void GsGunHeldReset(Player player) => biteShotTick = uint.MaxValue;
    }
}
