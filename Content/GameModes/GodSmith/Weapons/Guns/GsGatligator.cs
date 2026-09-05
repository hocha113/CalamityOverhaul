using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns
{
    /// <summary>
    /// 鳄鱼机关枪重铸：公认弱枪，吃 125% 目标线。<br/>
    /// [沼泽乱舞]：原版狂散保留，但每发子弹可在砖面弹跳一次（沼泽弹球），
    /// 乱枪在洞窟里横飞乱撞，散布劣势变覆盖优势。<br/>
    /// 跳弹不换弹幕载体，特种子弹身份全保留
    /// </summary>
    internal class GsGatligator : GsFireModeScheme
    {
        public override int TargetItemID => ItemID.Gatligator;

        public override string GsFamily => "Guns";

        protected override string GsDescFallback =>
            "Reforged: keeps the wild spray but every round skips once off blocks";
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
        ];

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            //砖面跳弹一次。PostAI 先于本帧位移判定，预测撞砖提前反弹，
            //原版子弹不会触发 tileCollide 自灭；跳弹次数放本地状态包，各端按同步的位置速度同源模拟
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
            }
        }
    }
}
