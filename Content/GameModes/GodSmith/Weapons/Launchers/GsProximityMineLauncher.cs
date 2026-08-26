using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Launchers
{
    /// <summary>
    /// 近战雷发射器重铸：智能雷场。地雷带 owner 可见的感应圈（旁人不见），
    /// 敌人触发原样自爆；右键按布设顺序 0.12 秒间隔多米诺连爆（波浪推进）；
    /// 布设超过 60 秒未爆的雷自动引爆并返还 1 发所用火箭（每分钟封顶 8 发防囤积）。<br/>
    /// 原版落雷 AI 一概不动；MarkData = 布设序号，MarkData2 = 所耗弹药物品 ID
    /// </summary>
    internal class GsProximityMineLauncher : GsLauncherScheme
    {
        public override int TargetItemID => ItemID.ProximityMineLauncher;

        protected override string GsDescFallback =>
            "Reforged: mines show their trigger radius to you; right click chains them in placement order; mines idle for 60s refund a rocket and self-clear";

        /// <summary>地雷主弹全家</summary>
        internal static readonly HashSet<int> MineTypes = [
            ProjectileID.ProximityMineI, ProjectileID.ProximityMineII,
            ProjectileID.ProximityMineIII, ProjectileID.ProximityMineIV,
            ProjectileID.ClusterMineI, ProjectileID.ClusterMineII,
            ProjectileID.WetMine, ProjectileID.LavaMine, ProjectileID.HoneyMine,
            ProjectileID.MiniNukeMineI, ProjectileID.MiniNukeMineII, ProjectileID.DryMine,
        ];

        /// <summary>雷场红</summary>
        internal static readonly Color MineRed = new(255, 96, 70);

        /// <summary>多米诺拍间隔（0.12 秒）</summary>
        private const int DominoGap = 7;

        /// <summary>最近一次 PickAmmo 的弹药物品 ID，owner 端射击链内消费</summary>
        private int pendingAmmoType;

        private LocalizedText tipChain;
        private LocalizedText tipRecycle;

        /// <summary>每雷本地包：布设龄计数（回收判定）</summary>
        private class MineState
        {
            public int age;
        }

        public override void GsSetStaticDefaults() {
            tipChain = this.GetLocalization("TipChain", () => "Domino!");
            tipRecycle = this.GetLocalization("TipRecycle", () => "Mine reclaimed");
        }

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.10f;

        public override void GsPickAmmo(Item weapon, Item ammo, Player player,
            ref int type, ref float speed, ref StatModifier damage, ref float knockback) {
            if (player.whoAmI == Main.myPlayer) {
                pendingAmmoType = ammo.type;
            }
        }

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            if (!MineTypes.Contains(proj.type)) {
                return;
            }
            GsLaunchersPlayer mp = Main.player[proj.owner].GetModPlayer<GsLaunchersPlayer>();
            router.MarkData = ++mp.mineSeq;
            router.MarkData2 = pendingAmmoType;
        }

        protected override void OnAltAction(Item item, Player player, GsLaunchersPlayer mp) {
            //收集自己的雷按布设序号升序，进多米诺调度队列
            List<(float seq, int index, int identity)> mines = [];
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner != player.whoAmI || !MineTypes.Contains(proj.type) || proj.timeLeft <= 3
                    || !proj.TryGetGlobalProjectile(out GodSmithProjRouter router)
                    || router.MarkScheme != this) {
                    continue;
                }
                mines.Add((router.MarkData, proj.whoAmI, proj.identity));
            }
            if (mines.Count == 0) {
                return;
            }
            mines.Sort((a, b) => a.seq.CompareTo(b.seq));
            for (int i = 0; i < mines.Count; i++) {
                mp.dominoQueue.Add((mines[i].index, mines[i].identity, 1 + i * DominoGap));
            }
            SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.75f, Pitch = 0.55f }, player.Center);
            LocalTip(player, tipChain, MineRed);
        }

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            LaunchPresentation(player, position, velocity, 1.2f, MineRed);
            return null;
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (!MineTypes.Contains(proj.type) || proj.timeLeft <= 3) {
                return;
            }
            //回收计龄：60 秒未爆自动引爆并返还弹药（owner 端权威）
            MineState st = router.GetOrCreateState<MineState>();
            st.age++;
            if (st.age < 3600 || !proj.IsOwnedByLocalPlayer()) {
                return;
            }
            Player player = Main.player[proj.owner];
            GsLaunchersPlayer mp = player.GetModPlayer<GsLaunchersPlayer>();
            int ammoType = (int)router.MarkData2;
            if (mp.mineRecycleBudget > 0 && ammoType > ItemID.None) {
                mp.mineRecycleBudget--;
                player.QuickSpawnItem(proj.GetSource_FromThis(), ammoType);
                LocalTip(player, tipRecycle, MineRed);
            }
            GsDetonate(proj);
        }

        public override void GsProjPostDraw(Projectile proj, Color lightColor, GodSmithProjRouter router) {
            //感应圈：只有布雷者本人可见的个人读数
            if (proj.owner != Main.myPlayer || !MineTypes.Contains(proj.type) || proj.timeLeft <= 3) {
                return;
            }
            Texture2D circle = CWRAsset.DiffusionCircle?.Value;
            if (circle == null) {
                return;
            }
            //呼吸相位用 identity 定种，绘制路径不掷随机
            float pulse = 0.82f + 0.18f * MathF.Sin(Main.GlobalTimeWrappedHourly * 3.1f + proj.identity * 0.83f);
            float radius = 78f * pulse;
            float scale = radius / (circle.Width * 0.5f);
            Main.EntitySpriteDraw(circle, proj.Center - Main.screenPosition, null,
                MineRed * 0.17f, 0f, circle.Size() * 0.5f, scale, SpriteEffects.None, 0);
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            if (MineTypes.Contains(proj.type)) {
                float scale = proj.type is ProjectileID.MiniNukeMineI or ProjectileID.MiniNukeMineII ? 1.5f : 1f;
                ExplosionAftermath(proj.Center, MineRed, scale);
            }
        }
    }
}
