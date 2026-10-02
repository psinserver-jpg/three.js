using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
using IKey = UnityEngine.InputSystem.Key;
#endif

namespace NexusStrike
{
    public enum GKey
    {
        W, A, S, D, Space, LeftShift, E, Q, R, H, Tab, Escape, Enter, F1, F2, F3, F4,
        Alpha1, Alpha2, Alpha3, Alpha4, Alpha5, Alpha6, Alpha7, Alpha8
    }

    /// <summary>
    /// Thin wrapper so the game works with either the legacy Input Manager or the
    /// new Input System package (whichever "Active Input Handling" is set to).
    /// </summary>
    public static class GameInput
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        static IKey Map(GKey k)
        {
            switch (k)
            {
                case GKey.W: return IKey.W;
                case GKey.A: return IKey.A;
                case GKey.S: return IKey.S;
                case GKey.D: return IKey.D;
                case GKey.Space: return IKey.Space;
                case GKey.LeftShift: return IKey.LeftShift;
                case GKey.E: return IKey.E;
                case GKey.Q: return IKey.Q;
                case GKey.R: return IKey.R;
                case GKey.H: return IKey.H;
                case GKey.Tab: return IKey.Tab;
                case GKey.Escape: return IKey.Escape;
                case GKey.Enter: return IKey.Enter;
                case GKey.F1: return IKey.F1;
                case GKey.F2: return IKey.F2;
                case GKey.F3: return IKey.F3;
                case GKey.F4: return IKey.F4;
                case GKey.Alpha1: return IKey.Digit1;
                case GKey.Alpha2: return IKey.Digit2;
                case GKey.Alpha3: return IKey.Digit3;
                case GKey.Alpha4: return IKey.Digit4;
                case GKey.Alpha5: return IKey.Digit5;
                case GKey.Alpha6: return IKey.Digit6;
                case GKey.Alpha7: return IKey.Digit7;
                default: return IKey.Digit8;
            }
        }

        public static bool Key(GKey k)
        {
            var kb = Keyboard.current;
            return kb != null && kb[Map(k)].isPressed;
        }

        public static bool KeyDown(GKey k)
        {
            var kb = Keyboard.current;
            return kb != null && kb[Map(k)].wasPressedThisFrame;
        }

        public static bool MouseHeld(int button)
        {
            var m = Mouse.current;
            if (m == null) return false;
            return button == 0 ? m.leftButton.isPressed : m.rightButton.isPressed;
        }

        public static bool MouseDown(int button)
        {
            var m = Mouse.current;
            if (m == null) return false;
            return button == 0 ? m.leftButton.wasPressedThisFrame : m.rightButton.wasPressedThisFrame;
        }

        /// <summary>Mouse movement this frame, in legacy "Mouse X/Y" axis units.</summary>
        public static Vector2 MouseDelta
        {
            get
            {
                var m = Mouse.current;
                return m == null ? Vector2.zero : m.delta.ReadValue() * 0.1f;
            }
        }
#else
        static KeyCode Map(GKey k)
        {
            switch (k)
            {
                case GKey.W: return KeyCode.W;
                case GKey.A: return KeyCode.A;
                case GKey.S: return KeyCode.S;
                case GKey.D: return KeyCode.D;
                case GKey.Space: return KeyCode.Space;
                case GKey.LeftShift: return KeyCode.LeftShift;
                case GKey.E: return KeyCode.E;
                case GKey.Q: return KeyCode.Q;
                case GKey.R: return KeyCode.R;
                case GKey.H: return KeyCode.H;
                case GKey.Tab: return KeyCode.Tab;
                case GKey.Escape: return KeyCode.Escape;
                case GKey.Enter: return KeyCode.Return;
                case GKey.F1: return KeyCode.F1;
                case GKey.F2: return KeyCode.F2;
                case GKey.F3: return KeyCode.F3;
                case GKey.F4: return KeyCode.F4;
                case GKey.Alpha1: return KeyCode.Alpha1;
                case GKey.Alpha2: return KeyCode.Alpha2;
                case GKey.Alpha3: return KeyCode.Alpha3;
                case GKey.Alpha4: return KeyCode.Alpha4;
                case GKey.Alpha5: return KeyCode.Alpha5;
                case GKey.Alpha6: return KeyCode.Alpha6;
                case GKey.Alpha7: return KeyCode.Alpha7;
                default: return KeyCode.Alpha8;
            }
        }

        public static bool Key(GKey k) { return Input.GetKey(Map(k)); }
        public static bool KeyDown(GKey k) { return Input.GetKeyDown(Map(k)); }
        public static bool MouseHeld(int button) { return Input.GetMouseButton(button); }
        public static bool MouseDown(int button) { return Input.GetMouseButtonDown(button); }

        /// <summary>Mouse movement this frame, in legacy "Mouse X/Y" axis units.</summary>
        public static Vector2 MouseDelta
        {
            get { return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")); }
        }
#endif

        public static bool AnyKeyDown(GKey a, GKey b) { return KeyDown(a) || KeyDown(b); }
    }
}
