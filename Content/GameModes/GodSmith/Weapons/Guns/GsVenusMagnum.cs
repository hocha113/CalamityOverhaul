using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns
{
    /// <summary>
    /// 维纳斯万能枪重铸：藤蔓串刺节奏。<br/>
    /// [万能精准]：打出暴击后下一发穿透 +1。<br/>
    /// 高速弹转换身份保留
    /// </summary>
    internal class GsVenusMagnum : GsFireModeScheme
    {
        public override int TargetItemID => ItemID.VenusMagnum;

        public override string GsFamily => "Guns";

        protected override string GsDescFallback =>
            "Reforged: after a crit the next round pierces one extra foe, the aim line lights up for the window";
        /// <summary>串刺弹私有 flag</summary>
        private const int FlagPierce = 1;

        /// <summary>暴击已就绪的下一发穿透；只在 owner 路径读写（命中回调即 owner 端）</summary>
        private bool pierceReady;

        /// <summary>本次射击为串刺弹的世界帧（打标窗口消费）</summary>
        private uint pierceShotTick = uint.MaxValue;

        public override GsFireMode[] Modes { get; } = [
            new GsFireMode {
                Key = "ModeFocus", EnName = "Magnum Focus",
                Converge = 0.5f,
            },
        ];

        //==================== 精准：暴击点亮串刺 ====================

        protected override void GsGunModifyShoot(Item item, Player player, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback,
            GsFireMode mode, GsGunsHardPlayer mp) {
            if (pierceReady) {
                pierceReady = false;
                pierceShotTick = Main.GameUpdateCount;
            }
        }

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            bool piercing = pierceShotTick == Main.GameUpdateCount;
            router.MarkData = PackMark(piercing ? FlagPierce : 0);
            //穿透 +1：命中 owner 端裁决；>0 守卫防 -1 无限穿写坏
            if (piercing && proj.penetrate > 0) {
                proj.penetrate++;
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            //只在攻击方端执行：暴击点亮下一发串刺
            if (!hit.Crit || target.friendly) {
                return;
            }
            pierceReady = true;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.7f, Pitch = 0.5f }, target.Center);
            }
        }

        internal override void GsGunHeldReset(Player player) {
            pierceReady = false;
            pierceShotTick = uint.MaxValue;
        }
    }
}
