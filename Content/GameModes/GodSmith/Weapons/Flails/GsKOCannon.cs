using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Flails
{
    /// <summary>
    /// 【连枷·KO 炮】猩红拳套加农：拳套棕红皮革褐。签名行为：①蓄压姿态收拳肩后、直拳咬速度方向
    /// ②读秒 KO：同一目标 4 秒内挨第 3 记实拳伤害击退翻倍、炸冲击环并弹「KO!」
    /// </summary>
    internal class GsKOCannon : GsFlailScheme
    {
        public override int TargetItemID => ItemID.KOCannon;

        protected override int FlailProjType => ModContent.ProjectileType<GsKOCannonHead>();

        protected override string GsDescFallback =>
            "Reforged: a braced straight punch; land three solid hits on the same target within four seconds\nThe third punch is a KO: double damage, double knockback, and a concussive ring";
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;

        /// <summary>读秒窗口帧数（4 秒）</summary>
        private const int CountWindow = 240;

        /// <summary>「KO!」弹字文案</summary>
        internal LocalizedText TipKO { get; private set; }

        public override void GsSetStaticDefaults()
            => TipKO = this.GetLocalization("TipKO", () => "KO!");

        /// <summary>读秒表 npc.whoAmI→(实拳数, 剩余帧)；只在 myPlayer 守门路径读写</summary>
        private readonly Dictionary<int, (int Count, int Timer)> koRegistry = [];

        public override void GsHoldItem(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer || koRegistry.Count == 0) {
                return;
            }
            //读秒衰减：窗口过期或目标失效即清账
            foreach (int key in new List<int>(koRegistry.Keys)) {
                (int count, int timer) = koRegistry[key];
                if (--timer <= 0 || !Main.npc[key].active) {
                    koRegistry.Remove(key);
                }
                else {
                    koRegistry[key] = (count, timer);
                }
            }
        }

        /// <summary>当前实拳数（owner 端查表）</summary>
        internal int PeekCount(int npcId) => koRegistry.TryGetValue(npcId, out var e) ? e.Count : 0;

        /// <summary>记一记实拳并刷新窗口，返回累计数（owner 端调用）</summary>
        internal int RegisterHit(int npcId) {
            int count = PeekCount(npcId) + 1;
            koRegistry[npcId] = (count, CountWindow);
            return count;
        }

        /// <summary>KO 后清零读秒（owner 端调用）</summary>
        internal void ResetCount(int npcId) => koRegistry.Remove(npcId);
    }

    /// <summary>
    /// KO 炮锤头。Brace 蓄压：拳收肩后蓄劲，出手直拳咬速度方向；
    /// 第 3 实拳的翻倍在 ModifyFlailHit 里查方案读秒表判定（owner 端），链身擦伤不计数
    /// </summary>
    internal class GsKOCannonHead : GsFlailHeadProj
    {
        /// <summary>KO 金（弹字颜色）</summary>
        internal static readonly Color KOGold = new(255, 214, 92);

        public override int SourceItemID => ItemID.KOCannon;
        public override int VanillaProjID => ProjectileID.BoxingGlove;
        //原版拳击手套走链贴图默认分支 Chain3，金属扣链衬皮革拳套，沿用
        public override Asset<Texture2D> ChainTexture => TextureAssets.Chain3;

        //直拳参数：出手快、链短、蓄压短促
        public override float LaunchSpeed => 19f;
        public override float MaxChainLength => 340f;
        public override int ChargeFrames => 36;
        public override GsFlailSpinMode SpinMode => GsFlailSpinMode.Brace;
        /// <summary>拳面咬速度方向</summary>
        public override bool SelfSpinHead => false;

        private GsKOCannon Scheme =>
            GodSmithScheme.TryGetScheme(SourceItemID, out var s) ? s as GsKOCannon : null;

        /// <summary>与基类 Colliding 的锤头盒同一几何：判定本次命中是否实拳（链身擦伤不算）</summary>
        private bool HeadBoxHits(NPC target) {
            Rectangle headBox = Projectile.Hitbox;
            headBox.Inflate(8, 8);
            return headBox.Intersects(target.Hitbox);
        }

        protected override void ModifyFlailHit(NPC target, ref NPC.HitModifiers modifiers) {
            //第 3 实拳 KO：伤害与击退翻倍（owner 端查表安全）
            if (!Projectile.IsOwnedByLocalPlayer() || Scheme is not GsKOCannon ko || !HeadBoxHits(target)) {
                return;
            }
            if (ko.PeekCount(target.whoAmI) >= 2) {
                modifiers.SourceDamage *= 2f;
                modifiers.Knockback *= 2f;
            }
        }

        protected override void OnHeadHit(NPC target, NPC.HitInfo hit, int damageDone, bool headHit) {
            if (!headHit || Owner.whoAmI != Main.myPlayer || Scheme is not GsKOCannon ko) {
                return;
            }
            int count = ko.RegisterHit(target.whoAmI);
            if (count < 3) {
                return;
            }
            //KO：清账、冲击环、高音重响、本地弹字
            ko.ResetCount(target.whoAmI);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsKOCannonShockProj>(),
                Math.Max(1, (int)(Projectile.damage * 0.25f)), 6f, Projectile.owner);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f, Pitch = 0.55f }, target.Center);
                CombatText.NewText(target.Hitbox, KOGold, ko.TipKO.Value, true);
            }
        }
    }

    /// <summary>
    /// KO 冲击环：拳劲外扩的空气震圈，早窗结伤（25% 小 AOE）；
    /// 用原版泡泡贴图按当前半径画一笔作范围提示
    /// </summary>
    internal class GsKOCannonShockProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bubble;

        private const int LifeFrames = 18;
        private const int DamageWindow = 8;

        private float LifeT => 1f - Projectile.timeLeft / (float)LifeFrames;
        /// <summary>震圈半径：拳劲先猛后缓</summary>
        private float RingRadius => MathHelper.Lerp(12f, 64f, 1f - (1f - LifeT) * (1f - LifeT));

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
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
