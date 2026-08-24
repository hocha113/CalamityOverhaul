using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.MainMenus.Characters
{
    /// <summary>硫火女巫，达成"永恒燃烧的现在"结局后解锁</summary>
    internal sealed class SupCalCharacter : MenuCharacter
    {
        public override string Key => "SupCal";
        public override bool Unlocked => MenuSave.ADV_SupCal_EBN;
        public override IList<Texture2D> ChipFrames => ADVAsset.SupCalsADV;
        public override float ChipScale => 0.5f;//74x92->37x46

        private List<Texture2D> expressions;
        public override IList<Texture2D> Expressions {
            get {
                if (expressions == null && ADVAsset.SupCalADV != null) {
                    expressions = [
                        ADVAsset.SupCalADV,
                        ADVAsset.SupCal_closeEyesADV ?? ADVAsset.SupCalADV,
                        ADVAsset.SupCal_smileADV ?? ADVAsset.SupCalADV
                    ];
                }
                return expressions;
            }
        }

        //硫火系列身份色，深红到亮橙
        public override Color AccentDark => new Color(180, 60, 30);
        public override Color AccentBright => new Color(255, 140, 70);
        public override Color BaseShade => new Color(25, 5, 5);
        public override string FallbackName => "硫火女巫";

        /// <summary>每 5s 扫一遍 6 帧眨眼，其余停帧 0</summary>
        public override int GetChipFrame(float timeSeconds) {
            const float cycle = 5f;
            const float sweep = 0.5f;
            float t = timeSeconds % cycle;
            if (t >= sweep) {
                return 0;
            }
            int count = ChipFrames?.Count ?? 1;
            return Math.Clamp((int)(t / sweep * count), 0, count - 1);
        }

        private readonly List<EmberPRT> embers = [];
        private readonly List<FlameWispPRT> wisps = [];
        private int emberSpawnTimer;
        private int wispSpawnTimer;

        public override void UpdateAmbient(in MenuCharacterScene scene) {
            //芯片底缘升起余烬
            if (scene.ChipAlpha > 0.05f) {
                emberSpawnTimer++;
                if (emberSpawnTimer >= 12 && embers.Count < 12) {
                    emberSpawnTimer = 0;
                    Vector2 spawn = scene.ChipCenter + new Vector2(
                        Main.rand.NextFloat(-0.5f, 0.5f) * scene.ChipSize.X,
                        scene.ChipSize.Y * 0.5f);
                    embers.Add(new EmberPRT(spawn,
                        Main.rand.NextFloat(1.5f, 3f),
                        Main.rand.NextFloat(0.3f, 0.8f),
                        Main.rand.NextFloat(-0.25f, 0.25f),
                        0f, Main.rand.NextFloat(50f, 90f)));
                }
            }
            for (int i = embers.Count - 1; i >= 0; i--) {
                if (embers[i].Update()) {
                    embers.RemoveAt(i);
                }
            }

            //立绘周围游焰，向立绘中心归巢
            if (scene.PortraitVisible) {
                wispSpawnTimer++;
                if (wispSpawnTimer >= 30 && wisps.Count < 12) {
                    wispSpawnTimer = 0;
                    Vector2 center = scene.PortraitRect.Center.ToVector2();
                    Vector2 spawn = center + Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2()
                        * Main.rand.NextFloat(160f, 300f);
                    FlameWispPRT wisp = new(spawn);
                    wisp.Size *= 0.5f;
                    wisps.Add(wisp);
                }
            }
            Vector2 home = scene.PortraitVisible
                ? scene.PortraitRect.Center.ToVector2()
                : scene.ChipCenter;
            for (int i = wisps.Count - 1; i >= 0; i--) {
                if (wisps[i].Update(home, 340f)) {
                    wisps.RemoveAt(i);
                }
            }
        }

        public override void DrawAmbient(SpriteBatch sb, in MenuCharacterScene scene) {
            foreach (FlameWispPRT wisp in wisps) {
                wisp.Draw(sb, scene.PortraitAlpha * 0.5f);
            }
            foreach (EmberPRT ember in embers) {
                ember.Draw(sb, scene.ChipAlpha * 0.85f);
            }
        }

        public override void ClearRuntime() {
            expressions = null;
            embers.Clear();
            wisps.Clear();
            emberSpawnTimer = 0;
            wispSpawnTimer = 0;
        }
    }
}
