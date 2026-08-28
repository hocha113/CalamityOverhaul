using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Schemes
{
    /// <summary>
    /// 火鞭「爆竹连环」：原版 2.75x 仆从引爆机制原样保留（经典机制不毁）。<br/>
    /// 深化 = 只有踩拍命中叠「引信」（3 层即满层态，处决印在此鞭上就是引信满层，
    /// 不另设第二套印）；满层后的下一次原版爆炸升级连环爆：追加 3 记延迟错位爆
    /// （每爆 1.0x 鞭面板，间隔 10f，半径 90px），爆后清引信开余韵。<br/>
    /// 引爆链路挂在爆炸弹幕 918 的类型通道首帧（原版爆炸由仆从命中触发、
    /// 出生源非已打标弹幕，承签不覆盖），不碰仆从侧代码。强度目标 118%
    /// </summary>
    internal class GsFirecracker : GsWhipScheme
    {
        public override int TargetItemID => ItemID.FireWhip;

        public override int WhipProjType => ProjectileID.FireWhip;

        public override int BaseWindowFrames => 13;

        public override int MarkCap => 3;

        public override float DamageTweak => 1.05f;

        /// <summary>爆竹橙红</summary>
        public override Color MarkColor => new(255, 110, 40);

        /// <summary>引信由原版爆炸引爆，鞭击只叠层不引爆</summary>
        protected override bool ExecuteByWhipHit => false;

        protected override string GsDescFallback =>
            "Reforged: keeps the classic minion-detonated blast; " +
            "on-beat lashes load fuses into the target, and at 3 fuses " +
            "the next blast chains into 3 extra staggered explosions";

        public override void GsSetStaticDefaults()
            //原版爆炸弹幕走类型通道：由仆从命中触发生成，无打标源可用
            => GsRegisterProjChannel(ProjectileID.FireWhipProj);

        /// <summary>引信满层态的连环爆：三记延迟错位爆，各 1.0x 登记面板</summary>
        protected override void OnExecute(Player player, NPC target, Projectile whipProj, WhipMarkState st) {
            int dmg = Math.Max(1, st.MarkDamage);
            float baseRot = (whipProj?.identity ?? target.whoAmI) * 0.71f;
            for (int i = 0; i < 3; i++) {
                Vector2 offset = (baseRot + i * MathHelper.TwoPi / 3f).ToRotationVector2() * (24f + 20f * i);
                Projectile.NewProjectile(player.GetSource_Misc("GsWhipExecute"),
                    target.Center + offset, Vector2.Zero,
                    ModContent.ProjectileType<GsWhipFirecrackerChainProj>(), dmg, 3f,
                    player.whoAmI, 10 + i * 10);
            }
        }

        /// <summary>火鞭深化：只有踩拍命中才装引信</summary>
        protected override int MarkGainOnHit(bool onBeat) => onBeat ? 1 : 0;

        /// <summary>爆炸弹幕的每弹幕闩：一枚爆炸至多引一次连环</summary>
        private sealed class FuseLocal
        {
            public bool Checked;
        }

        protected override void OnWhipProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.FireWhipProj) {
                return;
            }
            FuseLocal local = router.GetOrCreateState<FuseLocal>();
            if (local.Checked) {
                return;
            }
            local.Checked = true;
            //权威判定守 owner 端：爆炸 owner = 触发仆从的主人，只有自家引信可引
            if (proj.owner != Main.myPlayer) {
                return;
            }
            Player player = Main.player[proj.owner];
            GsWhipPlayer mp = player.GetModPlayer<GsWhipPlayer>();
            if (mp.Marks.Count == 0) {
                return;
            }
            foreach (NPC npc in Main.ActiveNPCs) {
                if (Vector2.Distance(npc.Center, proj.Center) > 120f
                    || !mp.TryGetMark(npc, out WhipMarkState st) || !st.ExecuteReady) {
                    continue;
                }
                DetonateSeal(player, npc, proj, st);
                break;   //一枚原版爆炸只升级一处引信
            }
        }
    }
}
