using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Schemes
{
    /// <summary>
    /// 皮鞭「驯兽教鞭」：全族教学位。<br/>
    /// 签名行为：①踩拍挥鞭响鞭声逐层爬升
    /// ②踩拍挥击的鞭梢最远点一记响鞭声（节拍的可听拍点）
    /// ③处决 = 皮革响鞭冲击（2.0x 单段爆）+ 两秒驯兽令（自家仆从对该目标 +15%），
    /// 转印瞬间驯兽哨响。<br/>
    /// 最宽 on-beat 窗（20f 基准）、空挥不罚（唯一 None）、三层转印。强度目标 135%（最弱鞭上限档）
    /// </summary>
    internal class GsLeatherWhip : GsWhipScheme
    {
        public override int TargetItemID => ItemID.BlandWhip;

        public override int WhipProjType => ProjectileID.BlandWhip;

        public override int BaseWindowFrames => 20;

        public override int MarkCap => 3;

        public override MissPolicyKind MissPolicy => MissPolicyKind.None;

        public override float DamageTweak => 1.10f;

        protected override string GsDescFallback =>
            "Reforged: chain swings on the beat to build tempo and lash speed; 3 lash scars seal the mark, and the next on-beat hit cracks it for 200% damage, then your minions maul the target for 2 seconds";
        /// <summary>踩拍起手：响鞭声随节拍层爬升（owner 端个人节奏反馈）</summary>
        protected override void OnSwingStart(Item item, Player player, GsWhipPlayer mp, bool onBeat) {
            if (!onBeat || VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item152 with {
                Volume = 0.28f + 0.05f * mp.BeatCombo,
                Pitch = -0.3f + 0.12f * mp.BeatCombo
            }, player.Center);
        }

        /// <summary>踩拍鞭梢拍点：最远点一记响鞭声（教学鞭的节拍可听化，owner 端）</summary>
        protected override void OnWhipApex(Player player, Projectile whipProj, GodSmithProjRouter router, Vector2 tipPos) {
            if (router.MarkData2 < 1f || VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item153 with { Volume = 0.35f, Pitch = 0.35f }, tipPos);
        }

        /// <summary>转印瞬间：驯兽哨响（owner 端确认反馈）</summary>
        protected override void OnSealLit(Player player, NPC target, WhipMarkState st) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item24 with { Volume = 0.5f, Pitch = 0.8f }, target.Center);
        }

        protected override void OnExecute(Player player, NPC target, Projectile whipProj, WhipMarkState st) {
            int dmg = Math.Max(1, (int)MathF.Round(st.MarkDamage * 2.0f));
            Projectile.NewProjectile(player.GetSource_Misc("GsWhipExecute"), target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsWhipLeatherCrackProj>(), dmg, 4f, player.whoAmI);
            //驯兽令：处决后两秒自家仆从对该目标再 +15%（余韵 +10% 之外的皮鞭专属）
            st.LeatherBoostUntil = Main.GameUpdateCount + 120;
        }
    }
}
