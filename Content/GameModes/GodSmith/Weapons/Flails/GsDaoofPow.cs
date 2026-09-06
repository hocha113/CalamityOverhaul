using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Flails
{
    /// <summary>
    /// 【连枷·帕武道】阴阳玉太极锤：墨黑皓白双相、玉青点缀。签名行为：①逐掷交替阴阳两相
    /// ②阴击挂阴印并迟缓目标 ③阳击命中带印目标清印引爆阴阳环
    /// </summary>
    internal class GsDaoofPow : GsFlailScheme
    {
        public override int TargetItemID => ItemID.DaoofPow;

        protected override int FlailProjType => ModContent.ProjectileType<GsDaoofPowHead>();

        protected override string GsDescFallback =>
            "Reforged: throws alternate between Yin and Yang; a Yin strike brands and slows the target\nA Yang strike on a branded target detonates the brand into a burst of light and ink";
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;

        /// <summary>阴印持续帧数（6 秒）</summary>
        private const int MarkFrames = 360;

        /// <summary>下一掷是否为阳相；方案跨玩家共享单例，只在 myPlayer 守门路径翻转</summary>
        private bool yangNext;

        /// <summary>阴印计时表 npc.whoAmI→剩余帧；只在 myPlayer 守门路径读写</summary>
        private readonly Dictionary<int, int> yinMarks = [];

        protected override float LaunchAi2(Player player, int index) {
            if (player.whoAmI != Main.myPlayer) {
                return 0f;//GsShoot 只在 owner 端跑，此分支纯防御
            }
            bool yang = yangNext;
            yangNext = !yangNext;
            return yang ? 1f : 0f;
        }

        public override void GsHoldItem(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer || yinMarks.Count == 0) {
                return;
            }
            //阴印衰减：过期或目标失效即除名
            foreach (int key in new List<int>(yinMarks.Keys)) {
                if (--yinMarks[key] <= 0 || !Main.npc[key].active) {
                    yinMarks.Remove(key);
                }
            }
        }

        /// <summary>挂阴印（owner 端调用）</summary>
        internal void MarkYin(NPC npc) => yinMarks[npc.whoAmI] = MarkFrames;

        /// <summary>目标带印则清印返回 true（owner 端调用）</summary>
        internal bool TryConsumeMark(NPC npc) => yinMarks.Remove(npc.whoAmI);
    }

    /// <summary>
    /// 帕武道锤头。ai[2]：0=阴 1=阳（随生成包过线，全端可读）；命中逻辑全在 owner 端 OnHeadHit
    /// </summary>
    internal class GsDaoofPowHead : GsFlailHeadProj
    {
        public override int SourceItemID => ItemID.DaoofPow;
        public override int VanillaProjID => ProjectileID.TheDaoofPow;
        //跟随原版弹幕 63 的链贴图 Chain7（brief 写的 154/Chain13 是肉丸的，查 TML 源纠正）
        public override Asset<Texture2D> ChainTexture => TextureAssets.Chain7;

        public override float MaxChainLength => 340f;

        /// <summary>本掷是否阳相</summary>
        private bool IsYang => WeaponAi2 >= 0.5f;

        protected override void OnHeadHit(NPC target, NPC.HitInfo hit, int damageDone, bool headHit) {
            if (!headHit || Owner.whoAmI != Main.myPlayer
                || !GodSmithScheme.TryGetScheme(SourceItemID, out var s) || s is not GsDaoofPow scheme) {
                return;
            }
            if (!IsYang) {
                //阴击：挂印+迟缓
                scheme.MarkYin(target);
                target.AddBuff(BuffID.Slow, 120);
                return;
            }
            //阳击带印：清印引爆阴阳环（75% 小 AOE）
            if (scheme.TryConsumeMark(target)) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsDaoofPowBurstProj>(),
                    Math.Max(1, (int)(Projectile.damage * 0.75f)), 3f, Projectile.owner);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.55f, Pitch = 0.5f }, target.Center);
                }
            }
        }
    }

    /// <summary>
    /// 阴阳环爆：圆域扩张，早窗结伤后淡出；
    /// 用原版泡泡贴图按当前半径画一笔作范围提示
    /// </summary>
    internal class GsDaoofPowBurstProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bubble;

        private const int LifeFrames = 26;
        /// <summary>只在扩张早窗结伤</summary>
        private const int DamageWindow = 10;

        private float LifeT => 1f - Projectile.timeLeft / (float)LifeFrames;
        /// <summary>环半径：先快后慢的扩张曲线</summary>
        private float RingRadius => MathHelper.Lerp(14f, 78f, 1f - (1f - LifeT) * (1f - LifeT));

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 40;//一环对同一目标只结一次
            Projectile.timeLeft = LifeFrames;
        }

        public override bool? CanDamage() => Projectile.timeLeft > LifeFrames - DamageWindow ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= RingRadius;

        /// <summary>范围提示：原版泡泡贴图按当前半径缩放画一笔，随寿命淡出</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float scale = RingRadius * 2f / MathF.Max(tex.Width, 1);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * (1f - LifeT),
                0f, tex.Size() / 2f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
