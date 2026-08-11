using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.PvP.Protocols
{
    /// <summary>
    /// 强制卸载（默认档，基础反制之二：拔已落地效果）。对自己施放（SelfRig 位）。<br/>
    /// 服务端裁决：拔掉施术者身上<b>最早落地</b>的一条敌方效果（授予账与防守方帐本
    /// 同为落地序，按账头拔即对齐）——服务端撤账并广播 PlayerEffectRemove(Uninstalled)，
    /// 防守方本机收包执行 OnDefenderRemove，HUD 条目碎裂退场，
    /// 攻击方植入物面板对应卡爆裂 + "被卸载"标签。<br/>
    /// 刻意贵（4 RAM）+ 长冷却 1800f：它是解创口的绷带不是免疫开关——
    /// 面对叠加上限 3 条的满压制，一次只能拔一条
    /// </summary>
    internal class ForceUninstall : QuickHackDef
    {
        public override void SetDefaults() {
            UploadTime = 30;
            RamCost = 4;
            Category = QuickHackCategory.Covert;
            SupportedTargets = HackTargetKind.SelfRig;
            UnlockedByDefault = true;
        }

        public override int GetDuration() => 0;

        public override bool CanApplyTo(IHackTarget target) {
            if (target is not SelfRigScannable rig) return false;
            Player player = rig.ResolvePlayer();
            if (player == null
                || !player.TryGetModPlayer(out PlayerHackLedger ledger)
                || ledger.UninstallCooldown > 0) {
                return false;
            }
            //本机端读自己的帐本；权威端帐本是空的，读授予账
            if (Main.netMode != NetmodeID.Server) {
                return ledger.HasHostileEffects;
            }
            return PlayerHackAuthority.HasGrantsOn(player.whoAmI);
        }

        public override bool CanApplyTo(IHackTarget target, Player caster) {
            //自我目标恒等：目标只能是施术者本人
            return CanApplyTo(target)
                && target is SelfRigScannable rig && caster?.whoAmI == rig.PlayerIndex;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not SelfRigScannable rig
                || caster?.whoAmI != rig.PlayerIndex) {
                return false;
            }
            //没账可拔返回 false（竞态：上传完成与最后一条效果到期同帧，窗口不到一帧）。
            //注意：追踪器的 OnApply 失败路径没有退款管线（EndAuthorityEffect 不退），
            //这 4 RAM 按沉没处理——冷却不吃（ExecuteUninstall 未跑），代价可接受
            if (!PlayerHackAuthority.ExecuteUninstall(caster)) return false;
            if (Main.netMode != NetmodeID.Server) EmitCue(caster);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (target is not SelfRigScannable rig) return;
            Player player = rig.ResolvePlayer();
            if (player == null) return;
            if (player.whoAmI == Main.myPlayer
                && player.TryGetModPlayer(out PlayerHackLedger ledger)) {
                ledger.UninstallCooldown = PlayerHackAuthority.UninstallCooldownFrames;
            }
            EmitCue(player);
        }

        /// <summary>拔除起手式：躯干向下抖落一串故障渣 + 断连闷响</summary>
        private static void EmitCue(Player player) {
            SoundEngine.PlaySound(CWRSound.Hacker with { Volume = 0.7f, Pitch = -0.45f },
                player.Center);
            for (int i = 0; i < 14; i++) {
                Vector2 pos = player.Center + new Vector2(
                    Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(-18f, 6f));
                Vector2 vel = new(Main.rand.NextFloat(-0.8f, 0.8f),
                    Main.rand.NextFloat(1.2f, 2.6f));
                PRTLoader.NewParticle<PRT_TBUGGlitch>(pos, vel, default,
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(26);
            }
        }
    }
}
