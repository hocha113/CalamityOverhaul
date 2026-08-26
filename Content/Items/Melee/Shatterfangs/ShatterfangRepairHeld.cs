using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Shatterfangs
{
    /// <summary>
    /// 右键修补持械，一场小小的接骨敲打：横持检视剑身，工位光点从刃根走到刃尖，
    /// 每记敲击刀身点头、后手压下、崩掉的牙屑被吸回断口；修满上举亮相叮一声<br/>
    /// 期间不可攻击，松手保留进度<br/>
    /// ai[0]=起始是否半刃 ai[1]=起始稳固度，进度=ai1+timer*速率，各端可独立重演
    /// </summary>
    internal class ShatterfangRepairHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<Shatterfang>();

        /// <summary>敲击节拍间隔(帧)</summary>
        private const int TinkInterval = 16;
        /// <summary>刃根→刃尖的粒子锚定长度(px)</summary>
        private const float BladeLen = 96f;

        private int timer;
        private int tinkTimer;
        /// <summary>敲击冲程 1→0，驱动刀身点头与后手压下</summary>
        private float knockAnim;
        /// <summary>工位白闪余帧</summary>
        private int glintTimer;
        private bool completed;
        /// <summary>完成后上举亮相的余帧</summary>
        private int finishLinger;

        private bool StartBroken => Projectile.ai[0] > 0.5f;
        private float StartStability => MathHelper.Clamp(Projectile.ai[1], 0f, 1f);
        /// <summary>0~1 修补进度，按拍推演，远端与本机同源</summary>
        private float Progress => MathHelper.Clamp(StartStability + timer * ShatterfangPlayer.RepairRate, 0f, 1f);
        /// <summary>当前修补工位，沿刃从根到尖推进</summary>
        private float WorkT => MathHelper.Lerp(0.2f, 0.92f, Progress);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 30;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>刀身角度：基础持位+呼吸+敲击点头+完成上举亮相</summary>
        private float CurrentBladeAngle() {
            int dir = Owner.direction;
            float ang = dir >= 0 ? -0.62f : MathHelper.Pi + 0.62f;
            //呼吸微晃
            ang += MathF.Sin(timer * 0.07f) * 0.016f * dir;
            //敲击瞬间刀尖向下点头，随冲程回弹
            ang += knockAnim * knockAnim * 0.085f * dir;
            if (completed) {
                //上举亮相，过冲缓出
                float t = MathHelper.Clamp((14f - finishLinger) / 9f, 0f, 1f);
                ang -= EaseOutBack(t) * 0.55f * dir;
            }
            return ang;
        }

        public override void AI() {
            if (Item.type != ModContent.ItemType<Shatterfang>() || Owner.dead || !Owner.active) {
                Projectile.Kill();
                return;
            }

            knockAnim *= 0.82f;
            if (glintTimer > 0) {
                glintTimer--;
            }

            //完成后的上举亮相
            if (completed) {
                if (--finishLinger <= 0) {
                    Projectile.Kill();
                    return;
                }
            }
            else if (!Owner.controlUseTile) {
                //松手中止，已修进度已逐拍入账
                Projectile.Kill();
                return;
            }
            else {
                timer++;
            }

            Projectile.timeLeft = 2;
            UpdatePose();

            //逐拍入账，进度权威在持有者本机
            if (!completed && Projectile.IsOwnedByLocalPlayer()) {
                ShatterfangPlayer sp = Owner.GetModPlayer<ShatterfangPlayer>();
                sp.Stability = Progress;
                sp.RegenDelay = 30;
            }

            if (!completed && Progress >= 1f) {
                Complete();
            }

            if (!completed) {
                HandleRepairFX();
            }
        }

        /// <summary>前手横持剑身，后手随敲击节拍向刃身压下</summary>
        private void UpdatePose() {
            int dir = Owner.direction;
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            float bladeAngle = CurrentBladeAngle();
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.ThreeQuarters, bladeAngle - MathHelper.PiOver2);
            //后手敲击：抬起→压向工位
            float backRest = bladeAngle - MathHelper.PiOver2 + dir * 0.55f;
            float backStrike = bladeAngle - MathHelper.PiOver2 + dir * 0.05f;
            Player.CompositeArmStretchAmount backStretch = knockAnim > 0.35f
                ? Player.CompositeArmStretchAmount.ThreeQuarters
                : Player.CompositeArmStretchAmount.Quarter;
            Owner.SetCompositeArmBack(true, backStretch, MathHelper.Lerp(backRest, backStrike, knockAnim));
            Projectile.Center = Owner.GetPlayerStabilityCenter();
        }

        /// <summary>修满：换回完整剑身，上举亮相，叮一声</summary>
        private void Complete() {
            completed = true;
            finishLinger = 14;

            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.GetModPlayer<ShatterfangPlayer>().CompleteRepair();
            }
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.95f, Pitch = 0.12f }, Owner.Center);
            Vector2 bladeMid = BladeAnchor(0.55f);
            for (int i = 0; i < 10; i++) {
                Dust d = Dust.NewDustPerfect(bladeMid + Main.rand.NextVector2Circular(20f, 14f)
                    , DustID.Bone, Main.rand.NextVector2Circular(1.4f, 1.4f) - new Vector2(0f, 1.2f), 60, default, 1f);
                d.noGravity = true;
            }
        }

        /// <summary>剑身上某比例处的世界坐标</summary>
        private Vector2 BladeAnchor(float t)
            => Owner.GetPlayerStabilityCenter() + CurrentBladeAngle().ToRotationVector2() * (BladeLen * t);

        /// <summary>敲击节拍：声阶爬升+工位火花+牙屑吸回；平时骨屑向工位汇聚</summary>
        private void HandleRepairFX() {
            //敲击拍
            if (++tinkTimer >= TinkInterval) {
                tinkTimer = 0;
                knockAnim = 1f;
                glintTimer = 6;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.5f, Pitch = -0.15f + Progress * 0.5f }, Owner.Center);
                    Vector2 wp = BladeAnchor(WorkT);
                    //敲击火花
                    for (int i = 0; i < 4; i++) {
                        Dust d = Dust.NewDustPerfect(wp, DustID.Bone
                            , Main.rand.NextVector2Circular(1.8f, 1.2f) - new Vector2(0f, 1f), 70, default, Main.rand.NextFloat(0.8f, 1.1f));
                        d.noGravity = true;
                    }
                    //崩掉的牙屑被吸回断口
                    for (int i = 0; i < 2; i++) {
                        Vector2 from = wp + Main.rand.NextVector2CircularEdge(30f, 30f);
                        PRTLoader.NewParticle<PRT_ToothChip>(from, (wp - from) * 0.11f
                            , ShatterfangFX.Ivory, Main.rand.NextFloat(0.14f, 0.22f))
                            ?.Configure(11, 0f);
                    }
                }
            }

            if (VaultUtils.isServer) {
                return;
            }
            //骨屑持续向工位汇聚
            if (timer % 2 == 0) {
                Vector2 anchor = BladeAnchor(WorkT + Main.rand.NextFloat(-0.14f, 0.14f));
                Vector2 offset = Main.rand.NextVector2CircularEdge(40f, 40f);
                Dust d = Dust.NewDustPerfect(anchor + offset, DustID.Bone, -offset * 0.085f, 90, default, 0.95f);
                d.noGravity = true;
            }
            //断口偶发血丝
            if (StartBroken && Main.rand.NextBool(5)) {
                Dust d = Dust.NewDustPerfect(BladeAnchor(Main.rand.NextFloat(0.45f, 0.7f))
                    , DustID.Blood, new Vector2(0f, Main.rand.NextFloat(0.5f, 1.5f)), 60, default, 0.9f);
                d.noGravity = true;
            }
            Lighting.AddLight(BladeAnchor(WorkT), ShatterfangFX.BoneLead.ToVector3() * (0.22f + Progress * 0.3f));
        }

        public override bool PreDraw(ref Color lightColor) {
            //修补期画半刃(或完整)剑身横持，工位光点沿刃走，敲击时崩出白闪
            bool drawBroken = StartBroken && !completed;
            Texture2D tex = (drawBroken ? ShatterfangAssets.BrokenBlade : ShatterfangAssets.FullBlade)?.Value;
            if (tex == null) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            int dir = Owner.direction;
            float bladeAngle = CurrentBladeAngle();
            bool flip = dir < 0;
            //朝左垂直镜像，刃轴按贴图真实对角走
            float axis = ShatterfangFX.BladeAxisOffset(tex);
            float rot = bladeAngle + (flip ? -axis : axis);
            Vector2 drawPos = Owner.GetPlayerStabilityCenter() + bladeAngle.ToRotationVector2() * 42f - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;

            sb.Draw(tex, drawPos, null, lightColor, rot, origin, 1.5f
                , flip ? SpriteEffects.FlipVertically : SpriteEffects.None, 0f);

            //愈合缝亮线，进度越高越亮；完成瞬间整刃白闪
            float seam = completed ? 1f : Progress;
            Color seamCol = ShatterfangFX.BoneLead * (completed
                ? MathHelper.Clamp(finishLinger / 10f, 0f, 1f) * 0.85f
                : 0.1f + seam * 0.25f);
            seamCol.A = 0;
            sb.Draw(tex, drawPos, null, seamCol, rot, origin, 1.52f
                , flip ? SpriteEffects.FlipVertically : SpriteEffects.None, 0f);

            if (completed) {
                return false;
            }

            //工位热点：小团米白辉光钉在当前修补处
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Vector2 wp = BladeAnchor(WorkT) - Main.screenPosition;
            if (glow != null) {
                Color hot = ShatterfangFX.BoneLead * (0.3f + knockAnim * 0.35f);
                hot.A = 0;
                sb.Draw(glow, wp, null, hot, 0f, glow.Size() * 0.5f, 0.24f + knockAnim * 0.08f, SpriteEffects.None, 0f);
            }
            //敲击瞬间的四芒星白闪
            if (glintTimer > 0) {
                Texture2D star = CWRAsset.StarTexture?.Value;
                if (star != null) {
                    float t = glintTimer / 6f;
                    Color glint = ShatterfangFX.BoneLead * (t * 0.7f);
                    glint.A = 0;
                    sb.Draw(star, wp, null, glint, timer * 0.2f, star.Size() * 0.5f, 0.07f + (1f - t) * 0.03f, SpriteEffects.None, 0f);
                }
            }
            return false;
        }

        /// <summary>过冲缓出</summary>
        private static float EaseOutBack(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float xm = x - 1f;
            return 1f + c3 * xm * xm * xm + c1 * xm * xm;
        }
    }
}
