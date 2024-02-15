using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Common.Effects
{
    public class ManagedRenderTarget : IDisposable
    {
        private RenderTarget2D target;

        internal bool WaitingForFirstInitialization {
            get;
            private set;
        } = true;

        internal RenderTargetCreationCondition CreationCondition {
            get;
            private set;
        }

        public bool IsUninitialized => target is null || target.IsDisposed;

        public bool IsDisposed {
            get;
            private set;
        }

        public bool ShouldResetUponScreenResize {
            get;
            private set;
        }

        public RenderTarget2D Target {
            get {
                if (IsUninitialized) {
                    target = CreationCondition(Main.screenWidth, Main.screenHeight);
                    WaitingForFirstInitialization = false;
                }

                return target;
            }
            private set => target = value;
        }

        public int Width => Target.Width;

        public int Height => Target.Height;

        public delegate RenderTarget2D RenderTargetCreationCondition(int screenWidth, int screenHeight);

        public ManagedRenderTarget(bool shouldResetUponScreenResize, RenderTargetCreationCondition creationCondition) {
            ShouldResetUponScreenResize = shouldResetUponScreenResize;
            CreationCondition = creationCondition;
            RenderTargetManager.ManagedTargets.Add(this);
        }

        public void Dispose() {
            if (IsDisposed)
                return;

            IsDisposed = true;
            target?.Dispose();
            GC.SuppressFinalize(this);
        }

        public void Recreate(int screenWidth, int screenHeight) {
            Dispose();
            IsDisposed = false;

            target = CreationCondition(screenWidth, screenHeight);
        }
    }
}
