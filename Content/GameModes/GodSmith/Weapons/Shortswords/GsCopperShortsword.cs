using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
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
    /// 铜短剑重铸「燧火」。<br/>
    /// 材质：粗锻铜刃，刃口如燧石般越击越烫。签名行为：①命中积攒火花计数，刃身随计数升温发亮
    /// ②第 4 次命中在刺尖迸小型火花爆，点燃目标与近旁敌人 ③全族最轻快的刺击手感，音调清脆上挑
    /// </summary>
    internal class GsCopperShortsword : GsShortswordScheme
    {
        public override int TargetItemID => ItemID.CopperShortsword;

        protected override string GsDescFallback =>
            "Reforged: every hit strikes a spark off the copper edge;" +
            "\nthe fourth spark bursts into flame at the point, igniting nearby foes";

        protected override int HeldProjType => ModContent.ProjectileType<GsCopperShortswordHeld>();

        /// <summary>火花计数 0~3。方案是跨玩家共享单例，但命中判定只在 owner 端发生，
        /// 调用方再守一层 myPlayer，等效本地独占；远端看不到计数辉光（纯表现，无碍）</summary>
        internal int FlintCount { get; private set; }

        /// <summary>命中记账（myPlayer 路径调用）：第 4 次命中返回 true 并清零</summary>
        internal bool RegisterFlintHit() {
            if (++FlintCount >= 4) {
                FlintCount = 0;
                return true;
            }
            return false;
        }

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.30f;//公认最弱开局武器（原版 5 伤），按公约弱势条款放宽至 135% 内取 130%
    }

    /// <summary>
    /// 铜短剑手持突刺：全族最轻的时间线（出 2 刺 3 驻 2 收 5）。
    /// 火花计数由方案持有跨刺存续，本类负责升温辉光与第 4 击的燧火爆反馈
    /// </summary>
    internal class GsCopperShortswordHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.CopperShortsword;

        //燧火铜色板
        internal static readonly Color CopperBright = new(255, 196, 138);
        internal static readonly Color CopperMain = new(224, 126, 62);
        internal static readonly Color EmberHot = new(255, 116, 40);

        protected override float WindupFrames => 2f;
        protected override float ThrustFrames => 4f;
        protected override float DwellFrames => 2f;
        protected override float RecoverFrames => 5f;
        protected override float PullbackDist => 8f;
        protected override float StabReach => 28f;
        protected override float BladeLength => 40f;
        protected override float ThrustEasePower => 2.5f;
        protected override int HitstopFrames => 1;
        protected override float LeanAmp => 0.024f;
        protected override float ThrustPitch => 0.30f;

        protected override Color EdgeColor => CopperBright;
        protected override Color CoreColor => EmberHot;

        private GsCopperShortsword Scheme =>
            GodSmithScheme.TryGetScheme(ItemID.CopperShortsword, out GodSmithScheme s) ? s as GsCopperShortsword : null;

        private int FlintCount => Scheme?.FlintCount ?? 0;

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            //火花记账：一刺对同一目标只记一次；命中检测只在 owner 端发生，再守一层 myPlayer
            if (!firstOnTarget || Owner.whoAmI != Main.myPlayer) {
                return;
            }
            GsCopperShortsword scheme = Scheme;
            if (scheme == null || !scheme.RegisterFlintHit()) {
                return;
            }

            //第 4 击：刺尖燧火爆——点燃目标与近旁敌人（AddBuff 客户端请求会过线）
            target.AddBuff(BuffID.OnFire, 180);
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.whoAmI == target.whoAmI || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                if (npc.Distance(TipPos) <= 80f) {
                    npc.AddBuff(BuffID.OnFire, 90);
                }
            }

            if (VaultUtils.isServer) {
                return;
            }
            //燧火爆升级反馈：点火声 + 焰心闪 + 一蓬带重力的火星
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.6f, Pitch = 0.15f }, TipPos);
            SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.4f, Pitch = 0.3f }, TipPos);
            PRTLoader.NewParticle<PRT_Light>(TipPos, Vector2.Zero, EmberHot, 0.30f)?.Configure(12, 0.85f);
            for (int i = 0; i < 12; i++) {
                Vector2 vel = stabUnit.RotatedByRandom(1.1) * Main.rand.NextFloat(3f, 9f);
                Color c = Main.rand.NextBool(3) ? CopperBright : EmberHot;
                PRTLoader.NewParticle<PRT_Spark>(TipPos, vel, c, Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(true, Main.rand.Next(14, 24));
            }
        }

        /// <summary>命中反馈：铜色摩擦火星，计数越高火星越烫</summary>
        protected override void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            Vector2 pos = Vector2.Lerp(TipPos, target.Center, 0.5f);
            int count = 3 + FlintCount;
            for (int i = 0; i < count; i++) {
                Vector2 vel = stabUnit.RotatedByRandom(0.6) * Main.rand.NextFloat(3f, 7f);
                Color c = Main.rand.NextBool(2 + FlintCount) ? CopperMain : EmberHot;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, c, Main.rand.NextFloat(0.3f, 0.55f))
                    ?.Configure(true, Main.rand.Next(10, 18));
            }
            if (!CWRLoad.NPCValue.ISTheofSteel(target)) {
                Dust d = Dust.NewDustPerfect(pos, DustID.Blood,
                    stabUnit.RotatedByRandom(0.8) * Main.rand.NextFloat(1.5f, 3f), 100, default, Main.rand.NextFloat(0.8f, 1.1f));
                d.noGravity = Main.rand.NextBool();
            }
        }

        /// <summary>升温可视化：计数越高刃身越亮（owner 端本地表现）</summary>
        protected override float ExtraGlowStrength() => FlintCount * 0.09f;

        /// <summary>计数满 3（下一击即爆）时刺尖缀一粒余烬呼吸光，定值脉动无随机</summary>
        protected override void DrawOverBlade(SpriteBatch sb) {
            if (FlintCount < 3 || FanFade <= 0.05f) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f + Projectile.whoAmI);
            Vector2 at = TipPos - Main.screenPosition;
            Color c = EmberHot with { A = 0 } * (0.5f * FanFade * pulse);
            sb.Draw(glow, at, null, c, 0f, glow.Size() / 2f, 0.16f * pulse, SpriteEffects.None, 0f);
        }
    }
}
