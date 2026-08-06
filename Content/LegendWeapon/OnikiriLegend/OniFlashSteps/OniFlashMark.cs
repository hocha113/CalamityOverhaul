using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions.Deeds;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using OKF = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFlashSteps.OniKamuiFlowRenderer;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFlashSteps
{
    /// <summary>神威标记/墨痕. 冲刺扫掠挂点,纳刀帧结算</summary>
    internal class OniFlashMark : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int RendFadeFrames = 14;   //引爆后墨裂蒸发时长

        private const int DamageWindow = 3;      //引爆帧起的伤害窗口

        private const int ForetellFrames = 6;    //引爆前增亮预告

        private bool initialized;
        private bool detonated;
        private bool executeRefunded;
        private int timer;
        private int detonateFrame;
        private float seed;
        private float brandAngle;
        private float sizeMul = 1f;
        //风樋:痕带收窄(伤害已降,视觉强度与威力一致;纯表现,判定不变)
        private float windSlimMul = 1f;
        private Vector2 lastCenter;
        private float rendHalfLen;

        private int BoundNPC => (int)Projectile.ai[0];
        private float DashAngle => Projectile.ai[2];
        private OniMeiActionContext ActionContext => OniMeiActionContext.Get(Projectile);

        /// <summary>绑定目标的存活实例，死亡/失效返回 null</summary>
        private NPC BoundInstance {
            get {
                int idx = BoundNPC;
                if (idx < 0 || idx >= Main.maxNPCs) {
                    return null;
                }
                NPC npc = Main.npc[idx];
                return npc.active ? npc : null;
            }
        }

        /// <summary>触发接口、在持有者客户端调用（冲刺主控扫描命中时）</summary>
        /// <param name="player">攻击发起者</param>
        /// <param name="npc">被标记目标（痕随其移动）</param>
        /// <param name="detonateDelay">引爆延迟（帧）；主控传"距纳刀帧数"使全部墨痕同帧裂开</param>
        /// <param name="damage">引爆伤害</param>
        /// <param name="knockback">击退</param>
        /// <param name="dashAngle">冲刺方向角（决定墨裂走向）</param>
        /// <param name="source">生成源，null 则回退 Misc 源</param>
        public static Projectile Fire(Player player, NPC npc, int detonateDelay,
            int damage, float knockback, float dashAngle, IEntitySource source = null) {
            source ??= player.GetSource_Misc("CWR_OniFlashMark");
            return Projectile.NewProjectileDirect(source, npc.Center, Vector2.Zero
                , ModContent.ProjectileType<OniFlashMark>(), damage, knockback, player.whoAmI
                , ai0: npc.whoAmI, ai1: Math.Max(detonateDelay, 4), ai2: dashAngle);
        }

        public override void SetStaticDefaults() {
            CWRLoad.ProjValue.ImmuneFrozen[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = CWRRef.GetTrueMeleeNoSpeedDamageClass();
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;   //Initialize 按引爆帧重设

            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;   //窗口仅数帧，单次结算

        }

        public override bool ShouldUpdatePosition() => false;

        private void Initialize() {
            initialized = true;
            detonateFrame = (int)Projectile.ai[1];
            Projectile.timeLeft = detonateFrame + RendFadeFrames + 8;
            seed = Projectile.identity * 0.6180339887f % 1f;
            //铭档随物品同步,各端解析一致
            OniMeiActionContext context = OniMeiActionContext.Get(Projectile);
            if (context?.HasSnapshot == true && context.Profile.WindGroove) {
                windSlimMul = 0.68f;
            }
            //痕的走向在冲刺方向上带一点确定性偏斜，敌群里不会全员平行

            brandAngle = DashAngle + (seed - 0.5f) * 0.42f;
            lastCenter = Projectile.Center;

            NPC npc = BoundInstance;
            if (npc != null) {
                lastCenter = npc.Center;
                rendHalfLen = 80f + MathF.Max(npc.width, npc.height) * 0.45f;
                sizeMul = MathHelper.Clamp(0.8f + MathF.Max(npc.width, npc.height) / 220f, 0.8f, 1.8f);
            }
            else {
                rendHalfLen = 90f;
            }
        }

        public override void AI() {
            if (!initialized) {
                Initialize();
            }
            timer++;

            NPC npc = BoundInstance;
            if (npc != null) {
                lastCenter = npc.Center;
                Projectile.Center = lastCenter;
            }
            else if (!detonated) {
                //目标提前死亡、痕无声散去

                Fizzle();
                return;
            }

            if (!detonated && timer >= detonateFrame) {
                Detonate();
            }

            float glow = detonated ? 0.85f : 0.30f;
            Lighting.AddLight(lastCenter, new Vector3(0.75f, 0.13f, 0.11f) * glow);
        }

        /// <summary>目标死亡的兜底退场、一缕墨烟</summary>
        private void Fizzle() {
            if (!Main.dedServ) {
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_CrimsonSmoke>(lastCenter + Main.rand.NextVector2Circular(10f, 10f)
                        , Main.rand.NextVector2Circular(0.8f, 0.8f) - Vector2.UnitY * 0.5f
                        , Color.White, Main.rand.NextFloat(0.05f, 0.08f))
                        ?.Configure(Main.rand.Next(14, 22), new Color(110, 24, 32), new Color(30, 14, 22));
                }
            }
            Projectile.Kill();
        }

        /// <summary>引爆、伤害窗开启 + 墨裂过曝白闪 + 碎晶垂直喷出（视觉沿冲刺方向定向蒸发）</summary>
        private void Detonate() {
            detonated = true;

            //綴樋：把落点报给资源层，同一次疾走的墨痕攒齐后连缀成串
            if (Projectile.IsOwnedByLocalPlayer()) {
                OniMeiCombatProfile stitchProfile = ActionContext?.HasSnapshot == true
                    ? ActionContext.Profile
                    : OniMeiCombatProfile.Identity;
                if (stitchProfile.MarkStitch) {
                    Main.player[Projectile.owner].GetModPlayer<OnikiriPlayer>()
                        .NotifyMarkDetonated(lastCenter,
                            ActionContext?.BaseWeaponDamage ?? Projectile.damage, in stitchProfile);
                }
            }

            SoundEngine.PlaySound(CWRSound.MeatySlash with {
                Pitch = 0.12f + seed * 0.3f,
                Volume = 0.46f,
                MaxInstances = 3,   //齐裂同帧多痕限流，防爆音

            }, lastCenter);

            if (Main.dedServ) {
                return;
            }
            //痕裂逐个高频,只推Bloom
            CrimsonImpactFX.PushAmbience(lastCenter, 0.12f);

            Vector2 along = brandAngle.ToRotationVector2();
            bool steel = BoundNPC.TryGetNPC(out NPC marked) && CWRLoad.NPCValue.ISTheofSteel(marked);
            //引爆材质分流:血肉可贴血渍 / 金属碎晶火花
            CrimsonRendHitVFX.SpawnImpactBurst(lastCenter, along, 0.85f, sizeMul, steel);
            Vector2 perp = (brandAngle + MathHelper.PiOver2).ToRotationVector2();
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(lastCenter + Main.rand.NextVector2Circular(16f, 16f)
                    , perp * Main.rand.NextFloat(-1.5f, 1.5f) + Main.rand.NextVector2Circular(0.6f, 0.6f)
                    , Color.White, Main.rand.NextFloat(0.06f, 0.11f) * sizeMul)
                    ?.Configure(Main.rand.Next(18, 28), new Color(130, 28, 36), new Color(32, 14, 22));
            }
        }

        /// <summary>只伤被标记者本人、重叠敌群互不误伤</summary>
        public override bool? CanHitNPC(NPC target) {
            if (!detonated || target.whoAmI != BoundNPC) {
                return false;
            }
            return null;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!detonated || timer > detonateFrame + DamageWindow) {
                return false;
            }
            Vector2 along = brandAngle.ToRotationVector2() * rendHalfLen;
            float cp = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , lastCenter - along, lastCenter + along, 46f * sizeMul, ref cp);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = MathF.Cos(DashAngle) >= 0f ? 1 : -1;
            OnikiriItem.ApplySlashPenetration(target, ref modifiers);
            if (CWRLoad.WormBodys.Contains(target.type)) {
                modifiers.FinalDamage *= 0.5f;
            }
            if (CWRLoad.ExoMechAresSegments.Contains(target.type)) {
                modifiers.FinalDamage *= 0.75f;
            }
            //对双子魔眼造成1.25倍伤害
            if (target.type == NPCID.Spazmatism || target.type == NPCID.Retinazer) {
                modifiers.FinalDamage *= 1.25f;
            }
            //对塔纳托斯头造成2.85倍伤害
            if (target.type == CWRID.NPC_ThanatosHead) {
                modifiers.FinalDamage *= 2.85f;
            }
            //对星流双子造成1.66倍伤害
            if (target.type == CWRID.NPC_Apollo || target.type == CWRID.NPC_Artemis) {
                modifiers.FinalDamage *= 1.66f;
            }
            if (Projectile.IsOwnedByLocalPlayer()) {
                OniMeiCombatProfile profile = ActionContext?.HasSnapshot == true
                    ? ActionContext.Profile
                    : OniMeiCombatProfile.Identity;
                OnikiriPlayer onikiri = Main.player[Projectile.owner].GetModPlayer<OnikiriPlayer>();
                float meiMul = onikiri.BuildMeiHitMultiplier(target, in profile,
                    ActionContext?.ActionSerial ?? 0, allowPlanted: false,
                    allowIron: false, zanshin: false,
                    armedConditionMul: ActionContext?.ArmedConditionMul ?? 1f,
                    tideOnBeatSnapshot: ActionContext?.TideOnBeat == true);
                if (OniMeiCombat.TryGetExecuteBonus(in profile, target, out float executeMul)) {
                    meiMul *= executeMul;
                }
                modifiers.FinalDamage *= OniMeiCombat.ClampConditionalDamage(
                    meiMul, in profile, target);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.25f, Volume = 0.6f, MaxInstances = 3 }, target.Center);

            if (Projectile.IsOwnedByLocalPlayer()) {
                OniMeiCombatProfile profile = ActionContext?.HasSnapshot == true
                    ? ActionContext.Profile
                    : OniMeiCombatProfile.Identity;
                Player owner = Main.player[Projectile.owner];
                OnikiriPlayer onikiri = owner.GetModPlayer<OnikiriPlayer>();
                onikiri.OnPrimaryBladeHit(target, in profile);
                OniMeiCombat.OnExecuteStrikeHit(owner, target, brandAngle, ref executeRefunded,
                    in profile, ActionContext?.ActionSerial ?? 0);
                if (!target.active || target.life <= 0) {
                    onikiri.TryPetalPruneOnKill(target,
                        ActionContext?.BaseWeaponDamage ?? Projectile.damage,
                        Projectile.knockBack, Projectile, in profile);
                    OniMeiDeedEvents.NotifyKill(owner, target, OniMeiDeedKillSource.FlashMark);
                }
            }

            if (Main.dedServ) {
                return;
            }
            bool steel = CWRLoad.NPCValue.ISTheofSteel(target);
            CrimsonRendHitVFX.SpawnHitTick(target.Center, brandAngle.ToRotationVector2(), sizeMul, steel);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || !initialized) {
                return;
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (!OKF.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                return;
            }

            if (!detonated) {
                DrawBrand(device, fx);
            }
            else {
                DrawRend(device, fx);
            }

            OKF.EndDraw(device, pb, pr, pd);
        }

        /// <summary>潜伏期、缠身细痕，微脉动；引爆前 6 帧增亮增宽预告</summary>
        private void DrawBrand(GraphicsDevice device, Effect fx) {
            float pulse = 0.42f + 0.10f * MathF.Sin(timer * 0.34f + seed * 9f);
            float foretell = MathHelper.Clamp((timer - (detonateFrame - ForetellFrames)) / (float)ForetellFrames, 0f, 1f);
            float opacity = MathHelper.Lerp(pulse, 0.92f, foretell);
            //出生白闪速落

            float flash = timer <= 1 ? 0.8f : MathF.Pow(0.5f, timer - 1) * 0.8f;
            flash = MathF.Max(flash, foretell * 0.35f);

            float halfLen = rendHalfLen * 0.58f;
            Vector2 along = brandAngle.ToRotationVector2() * halfLen;
            Vector2[] pts = [lastCenter - along, lastCenter, lastCenter + along];

            OKF.RibbonDef def = new() {
                HalfWidth = (8.5f + foretell * 4.5f) * sizeMul * windSlimMul,
                PerpOffset = 0f,
                Seed = seed,
                FlowMul = 0.85f,
                TearAmp = 0.55f,
                HeadBoost = 0.35f + foretell * 0.65f,
                OpacityMul = 1f,
            };
            OKF.DrawRibbon(device, fx, pts, in def, retract: 0f, flash: flash, opacity: opacity);
        }

        /// <summary>引爆后、全宽墨裂，过曝一拍后沿冲刺方向定向蒸发</summary>
        private void DrawRend(GraphicsDevice device, Effect fx) {
            int dt = timer - detonateFrame;
            float fadeT = MathHelper.Clamp(dt / (float)RendFadeFrames, 0f, 1f);
            float flash = MathF.Pow(0.60f, dt);
            float opacity = 1f - fadeT * fadeT * 0.4f;

            Vector2 along = brandAngle.ToRotationVector2() * rendHalfLen;
            Vector2[] pts = [lastCenter - along, lastCenter, lastCenter + along];

            OKF.RibbonDef def = new() {
                HalfWidth = 40f * sizeMul * windSlimMul,
                PerpOffset = 0f,
                Seed = seed,
                FlowMul = 1.25f,
                TearAmp = 1.05f,
                HeadBoost = 0.9f,
                OpacityMul = 1f,
            };
            OKF.DrawRibbon(device, fx, pts, in def, retract: fadeT, flash: flash, opacity: opacity);
        }
    }
}
