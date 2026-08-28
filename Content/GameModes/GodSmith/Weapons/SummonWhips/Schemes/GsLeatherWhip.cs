using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Schemes
{
    /// <summary>
    /// 皮鞭「驯兽教鞭」：全族教学位。材质：鞍革编鞭，棕金色泽。<br/>
    /// 签名行为：①踩拍挥鞭响鞭声逐层爬升，满转时鞭梢甩出鞍革金火星
    /// ②踩拍挥击的鞭梢最远点炸一记响鞭气浪（节拍的可视拍点）
    /// ③处决 = 皮革响鞭冲击（2.0x 单段爆）+ 两秒驯兽令（自家仆从对该目标 +15%），
    /// 转印瞬间驯兽哨响、目标烙上棕金印。<br/>
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

        /// <summary>鞍革棕金</summary>
        public override Color MarkColor => new(214, 154, 82);

        /// <summary>鞍革亮金（满转鞭梢与气浪拍点用）</summary>
        private static readonly Color LeatherBright = new(255, 230, 180);

        protected override string GsDescFallback =>
            "Reforged: chain swings on the beat to build tempo and lash speed; " +
            "3 lash scars seal the mark, and the next on-beat hit cracks it for 200% damage, " +
            "then your minions maul the target for 2 seconds";

        /// <summary>踩拍起手：响鞭声随节拍层爬升 + 鞍革碎屑从手位甩出（owner 端个人节奏反馈）</summary>
        protected override void OnSwingStart(Item item, Player player, GsWhipPlayer mp, bool onBeat) {
            if (!onBeat || VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item152 with {
                Volume = 0.28f + 0.05f * mp.BeatCombo,
                Pitch = -0.3f + 0.12f * mp.BeatCombo
            }, player.Center);
            for (int i = 0; i < 2; i++) {
                Dust d = Dust.NewDustPerfect(player.MountedCenter + new Vector2(player.direction * 12f, -4f),
                    DustID.WoodFurniture, new Vector2(player.direction * Main.rand.NextFloat(1f, 2.5f),
                    -Main.rand.NextFloat(0.5f, 1.5f)));
                d.noGravity = false;
                d.scale = Main.rand.NextFloat(0.8f, 1.1f);
            }
        }

        /// <summary>踩拍鞭梢拍点：最远点炸一记响鞭气浪（教学鞭的节拍可视化，owner 端）</summary>
        protected override void OnWhipApex(Player player, Projectile whipProj, GodSmithProjRouter router, Vector2 tipPos) {
            if (router.MarkData2 < 1f || VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item153 with { Volume = 0.35f, Pitch = 0.35f }, tipPos);
            PRTLoader.NewParticle<PRT_Light>(tipPos, Vector2.Zero, LeatherBright, 0.12f)?.Configure(7, 0.8f);
            for (int i = 0; i < 4; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 3.5f);
                PRTLoader.NewParticle<PRT_Spark>(tipPos, vel,
                    i % 2 == 0 ? LeatherBright : MarkColor,
                    Main.rand.NextFloat(0.22f, 0.36f))?.Configure(false, Main.rand.Next(9, 14));
            }
        }

        /// <summary>满转鞭身：节拍满层时鞭梢后段甩鞍革金火星（升温拖尾之上的皮鞭专属层）</summary>
        protected override void OnWhipProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != WhipProjType || (int)router.MarkData < BeatComboCap
                || VaultUtils.isServer || proj.owner != Main.myPlayer
                || Main.GameUpdateCount % 4 != 0) {
                return;
            }
            System.Collections.Generic.List<Vector2> pts = proj.GetWhipControlPoints();
            if (pts.Count < 4) {
                return;
            }
            PRTLoader.NewParticle<PRT_GsWhipBeatSpark>(pts[^1],
                Main.rand.NextVector2Circular(0.8f, 0.8f), LeatherBright, 0.6f);
        }

        /// <summary>转印瞬间：驯兽哨响 + 目标烙棕金印（owner 端确认反馈）</summary>
        protected override void OnSealLit(Player player, NPC target, WhipMarkState st) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item24 with { Volume = 0.5f, Pitch = 0.8f }, target.Center);
            PRTLoader.NewParticle<PRT_Sparkle>(target.Top, -Vector2.UnitY * 0.5f,
                MarkColor, 0.55f)?.Configure(LeatherBright, 16, 0.25f);
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
