using System.Collections.Generic;
using UnityEngine;

namespace SamediRouge
{
    /// <summary>
    /// Tapes et glissés au doigt (ou à la souris), plus le clavier pour l'éditeur.
    /// Fonctionne avec l'ancien Input Manager ; le nouvel Input System est pris en charge s'il est le seul actif.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class InputCtl : MonoBehaviour
    {
        public const int Left = 0, Right = 1, Up = 2, Down = 3;

        public readonly List<Vector2> taps = new List<Vector2>();
        public readonly List<int> swipes = new List<int>();
        public bool keySwing, keyPause, keyStart;

        class Track { public Vector2 start; public float t; public bool done; }
        readonly Dictionary<int, Track> tracks = new Dictionary<int, Track>();
        float threshold;

        void Awake()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            Input.simulateMouseWithTouches = false;
#endif
        }

        void Update()
        {
            taps.Clear();
            swipes.Clear();
            keySwing = keyPause = keyStart = false;
            threshold = Mathf.Max(22f, Screen.height * 0.028f);

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch t = Input.GetTouch(i);
                    switch (t.phase)
                    {
                        case TouchPhase.Began: Begin(t.fingerId, t.position); break;
                        case TouchPhase.Moved:
                        case TouchPhase.Stationary: Move(t.fingerId, t.position); break;
                        case TouchPhase.Ended: End(t.fingerId, t.position, false); break;
                        case TouchPhase.Canceled: End(t.fingerId, t.position, true); break;
                    }
                }
            }
            else
            {
                Vector2 mp = Input.mousePosition;
                if (Input.GetMouseButtonDown(0)) Begin(-1, mp);
                else if (Input.GetMouseButton(0)) Move(-1, mp);
                if (Input.GetMouseButtonUp(0)) End(-1, mp, false);
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.Q)) swipes.Add(Left);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) swipes.Add(Right);
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Space)) swipes.Add(Up);
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) swipes.Add(Down);
            if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.J)) keySwing = true;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)) keyPause = true;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) keyStart = true;
#elif SR_INPUTSYS
            var ts = UnityEngine.InputSystem.Touchscreen.current;
            bool touching = false;
            if (ts != null)
            {
                foreach (var tc in ts.touches)
                {
                    var ph = tc.phase.ReadValue();
                    if (ph == UnityEngine.InputSystem.TouchPhase.None) continue;
                    touching = true;
                    int id = tc.touchId.ReadValue();
                    Vector2 p = tc.position.ReadValue();
                    if (ph == UnityEngine.InputSystem.TouchPhase.Began) Begin(id, p);
                    else if (ph == UnityEngine.InputSystem.TouchPhase.Moved || ph == UnityEngine.InputSystem.TouchPhase.Stationary) Move(id, p);
                    else if (ph == UnityEngine.InputSystem.TouchPhase.Ended) End(id, p, false);
                    else if (ph == UnityEngine.InputSystem.TouchPhase.Canceled) End(id, p, true);
                }
            }
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (!touching && mouse != null)
            {
                Vector2 mp = mouse.position.ReadValue();
                if (mouse.leftButton.wasPressedThisFrame) Begin(-1, mp);
                else if (mouse.leftButton.isPressed) Move(-1, mp);
                if (mouse.leftButton.wasReleasedThisFrame) End(-1, mp, false);
            }
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame) swipes.Add(Left);
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) swipes.Add(Right);
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) swipes.Add(Up);
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) swipes.Add(Down);
                if (kb.fKey.wasPressedThisFrame || kb.jKey.wasPressedThisFrame) keySwing = true;
                if (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame) keyPause = true;
                if (kb.enterKey.wasPressedThisFrame) keyStart = true;
            }
#endif
        }

        void Begin(int id, Vector2 p)
        {
            tracks[id] = new Track { start = p, t = Time.unscaledTime, done = false };
        }

        void Move(int id, Vector2 p)
        {
            if (!tracks.TryGetValue(id, out Track tr) || tr.done) return;
            Vector2 d = p - tr.start;
            if (d.magnitude < threshold) return;
            tr.done = true;
            if (Mathf.Abs(d.x) > Mathf.Abs(d.y)) swipes.Add(d.x > 0f ? Right : Left);
            else swipes.Add(d.y > 0f ? Up : Down);
        }

        void End(int id, Vector2 p, bool canceled)
        {
            if (!tracks.TryGetValue(id, out Track tr)) return;
            if (!tr.done && !canceled)
            {
                Move(id, p);
                if (!tr.done && Time.unscaledTime - tr.t < 0.35f) taps.Add(p);
            }
            tracks.Remove(id);
        }
    }
}
