using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【时髦剪刀】材质：理发师的银亮剪刃。签名：①全族最快的两拍开合剪，
    /// 小几何高频出手 ②对同一目标第 6 次命中触发「咔嚓」处决剪：该次伤害翻倍、
    /// 剪刀脆响
    /// </summary>
    internal class GsStylishScissors : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.StylistKilLaKillScissorsIWish;

        protected override int HeldProjID => ModContent.ProjectileType<GsStylishScissorsHeld>();

        protected override int ComboBeats => 2;

        protected override string GsDescFallback =>
            "Reforged: snips in rapid two-beat cuts; every sixth cut on the same target lands a finishing snip for double damage";
        internal static readonly Color SilverBright = new(240, 243, 248); //剪刃银白
        internal static readonly Color SilverMain = new(186, 192, 202);   //钢身银灰
        internal static readonly Color StylistRed = new(255, 72, 92);     //理发师红

        //公认弱势趣味武器，包络放宽到 130%：底伤 +12%，
        //第 6 剪 x2 对单目标均摊约 +17%，综合 DPS 约为原版 124%~130%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.12f;
    }

    /// <summary>
    /// 时髦剪刀手持：两拍极速开合（全族最快，Raise 3/Slash 2/Recover 5），
    /// 对同一目标计数到第 6 次命中触发处决剪。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsStylishScissorsHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.StylistKilLaKillScissorsIWish;
        protected override Color EdgeBright => GsStylishScissors.SilverBright;
        protected override Color BodyMain => GsStylishScissors.SilverMain;
        protected override Color HotAccent => GsStylishScissors.StylistRed;

        protected override int BeatCount => 2;
        //剪刀是小几何：短触及、窄判定，靠频率吃饭
        protected override float BaseReach => 86f;
        protected override float CollisionWidth => 30f;
        protected override float PointBlankRadius => 36f;

        /// <summary>
        /// 对同一目标的命中计数（键=NPC whoAmI）。只在 owner 端命中路径读写：
        /// 每个客户端只为本地玩家的剪刀计数，互不越权；伤害翻倍随命中包过线
        /// </summary>
        private static readonly Dictionary<int, int> snipCounts = [];
        private const int ExecuteAt = 6;

        /// <summary>本次命中是处决剪（ModifyHitExtra 置位，OnHitTarget 消费后复位）</summary>
        private bool executeSnip;

        protected override GsBroadBeat GetBeat(int stage) {
            //两拍开合：一开一合互为镜像节奏，只差音高与微幅跟进
            return new GsBroadBeat {
                Raise = 3, Hold = 1, Slash = 2, Recover = 5,
                RaiseBack = 1.15f, Follow = 0.8f, ReachScale = 1f, LeanAmp = 0.02f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f,
                SwingPitch = stage == 0 ? 0.5f : 0.66f,//开、合两个音
            };
        }

        //极快小剪没有终结拍概念
        protected override bool IsFinisher => false;

        /// <summary>第 6 次命中同一目标：处决剪 x2</summary>
        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            snipCounts.TryGetValue(target.whoAmI, out int n);
            n++;
            if (n >= ExecuteAt) {
                n = 0;
                executeSnip = true;
                modifiers.FinalDamage *= 2f;
            }
            snipCounts[target.whoAmI] = n;
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            //目标死亡清计数，防 whoAmI 复用串档
            if (Owner.whoAmI == Main.myPlayer && (!target.active || target.life <= 0)) {
                snipCounts.Remove(target.whoAmI);
            }
            //处决剪：「咔嚓」脆响
            if (executeSnip) {
                executeSnip = false;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item16 with { Volume = 0.9f, Pitch = 0.45f }, target.Center);
                }
            }
        }
    }
}
