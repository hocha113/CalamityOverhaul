using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Shatterfangs
{
    /// <summary>
    /// 右键修补持械。按住右键横持检视剑身，骨屑向断口汇聚逐步补齐，
    /// 期间不可攻击；松手保留进度，修满换回完整剑身并叮一声<br/>
    /// ai[0]=起始是否半刃 ai[1]=起始稳固度，进度=ai1+timer*速率，各端可独立重演
    /// </summary>
    internal class ShatterfangRepairHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<Shatterfang>();

        private int timer;
        private int tinkTimer;
        private bool completed;
        /// <summary>完成后停驻展示的余帧</summary>
        private int finishLinger;

        private bool StartBroken => Projectile.ai[0] > 0.5f;
        private float StartStability => MathHelper.Clamp(Projectile.ai[1], 0f, 1f);
        /// <summary>0~1 修补进度，按拍推演，远端与本机同源</summary>
        private float Progress => MathHelper.Clamp(StartStability + timer * ShatterfangPlayer.RepairRate, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 30;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Item.type != ModContent.ItemType<Shatterfang>() || Owner.dead || !Owner.active) {
                Projectile.Kill();
                return;
            }

            //完成后的短暂定格展示
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

        /// <summary>横持检视姿态，剑尖斜前上</summary>
        private void UpdatePose() {
            int dir = Owner.direction;
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            float armAngle = dir >= 0 ? -0.62f : MathHelper.Pi + 0.62f;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.ThreeQuarters, armAngle - MathHelper.PiOver2);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Quarter, armAngle - MathHelper.PiOver2 + dir * 0.4f);
            Projectile.Center = Owner.GetPlayerStabilityCenter();
        }

        /// <summary>修满：换回完整剑身，叮一声，白闪定格</summary>
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
        private Vector2 BladeAnchor(float t) {
            float bladeAngle = Owner.direction >= 0 ? -0.62f : MathHelper.Pi + 0.62f;
            return Owner.GetPlayerStabilityCenter() + bladeAngle.ToRotationVector2() * (96f * t);
        }

        /// <summary>骨屑向断口汇聚+敲击声阶</summary>
        private void HandleRepairFX() {
            if (VaultUtils.isServer) {
                return;
            }
            //骨屑被拽向剑身
            if (timer % 2 == 0) {
                Vector2 anchor = BladeAnchor(Main.rand.NextFloat(0.3f, 0.95f));
                Vector2 offset = Main.rand.NextVector2CircularEdge(42f, 42f);
                Dust d = Dust.NewDustPerfect(anchor + offset, DustID.Bone, -offset * 0.08f, 90, default, 0.95f);
                d.noGravity = true;
            }
            //断口偶发血丝
            if (StartBroken && Main.rand.NextBool(5)) {
                Dust d = Dust.NewDustPerfect(BladeAnchor(Main.rand.NextFloat(0.45f, 0.7f))
                    , DustID.Blood, new Vector2(0f, Main.rand.NextFloat(0.5f, 1.5f)), 60, default, 0.9f);
                d.noGravity = true;
            }
            //敲击声阶随进度爬升
            if (++tinkTimer >= 16) {
                tinkTimer = 0;
                SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.5f, Pitch = -0.15f + Progress * 0.5f }, Owner.Center);
            }
            Lighting.AddLight(BladeAnchor(0.55f), ShatterfangFX.BoneLead.ToVector3() * (0.25f + Progress * 0.3f));
        }

        public override bool PreDraw(ref Color lightColor) {
            //修补期画半刃(或完整)剑身横持，断口处随进度亮起愈合缝
            bool drawBroken = StartBroken && !completed;
            Texture2D tex = (drawBroken ? ShatterfangAssets.BrokenBlade : ShatterfangAssets.FullBlade)?.Value;
            if (tex == null) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            int dir = Owner.direction;
            float bladeAngle = dir >= 0 ? -0.62f : MathHelper.Pi + 0.62f;
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
                : 0.12f + seam * 0.3f);
            seamCol.A = 0;
            sb.Draw(tex, drawPos, null, seamCol, rot, origin, 1.52f
                , flip ? SpriteEffects.FlipVertically : SpriteEffects.None, 0f);
            return false;
        }
    }
}
