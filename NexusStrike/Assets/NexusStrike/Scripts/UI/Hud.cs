using System.Collections.Generic;
using UnityEngine;

namespace NexusStrike
{
    /// <summary>All UI drawn with IMGUI (no UI assets needed): menus, hero select, combat HUD, scoreboard.</summary>
    public class Hud : MonoBehaviour
    {
        float scale = 1f;
        float VW, VH = 1080f;
        GUIStyle style;
        Texture2D ring, disc;
        string selectedHero = "vex";
        bool showHelp;

        float hitTime = -9f, killTime = -9f;
        bool hitHead;
        readonly List<KeyValuePair<string, float>> elims = new List<KeyValuePair<string, float>>();
        readonly List<KeyValuePair<Vector3, float>> damageDirs = new List<KeyValuePair<Vector3, float>>();
        readonly List<KeyValuePair<string, float>> callouts = new List<KeyValuePair<string, float>>();
        readonly List<Color> calloutColors = new List<Color>();

        GameManager GM { get { return GameManager.I; } }

        void Update()
        {
            if (GM != null && GM.InMatch && GameInput.KeyDown(GKey.F1)) showHelp = !showHelp;
        }

        /// <summary>True once for a key press delivered to IMGUI; consumes the event so it can't chain into the next screen.</summary>
        static bool KeyEvent(KeyCode k)
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown || e.keyCode != k) return false;
            e.Use();
            return true;
        }

        // ------------------------------------------------------------------ events from GameManager

        public void OnHit(bool head) { hitTime = Time.unscaledTime; hitHead = head; }

        public void OnKill(Combatant victim)
        {
            killTime = Time.unscaledTime;
            elims.Add(new KeyValuePair<string, float>("ELIMINATED  " + victim.def.name, Time.unscaledTime));
            if (elims.Count > 4) elims.RemoveAt(0);
        }

        public void OnAssist(Combatant victim)
        {
            elims.Add(new KeyValuePair<string, float>("ASSIST  " + victim.def.name, Time.unscaledTime));
            if (elims.Count > 4) elims.RemoveAt(0);
        }

        public void OnPlayerDamaged(Vector3 from)
        {
            damageDirs.Add(new KeyValuePair<Vector3, float>(from, Time.unscaledTime));
            if (damageDirs.Count > 6) damageDirs.RemoveAt(0);
        }

        public void OnUltCallout(Combatant c, bool ally)
        {
            callouts.Add(new KeyValuePair<string, float>((ally ? "ALLY " : "ENEMY ") + c.def.name + ":  " + c.kit.ultName.ToUpper() + "!", Time.unscaledTime));
            calloutColors.Add(ally ? TeamColors.Ally : TeamColors.Enemy);
            if (callouts.Count > 4) { callouts.RemoveAt(0); calloutColors.RemoveAt(0); }
        }

        // ------------------------------------------------------------------ drawing helpers

        void EnsureResources()
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label);
                style.richText = false;
                style.wordWrap = false;
                style.clipping = TextClipping.Overflow;
            }
            if (ring == null)
            {
                ring = MakeCircle(64, 0.82f);
                disc = MakeCircle(64, 0f);
            }
        }

        static Texture2D MakeCircle(int size, float inner)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    float a = Mathf.Clamp01((1f - d) * r);
                    if (inner > 0f) a *= Mathf.Clamp01((d - inner) * r);
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            return t;
        }

        void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        void Tex(Rect r, Texture t, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, t);
            GUI.color = old;
        }

        void Frame(Rect r, Color c, float w = 2f)
        {
            Fill(new Rect(r.x, r.y, r.width, w), c);
            Fill(new Rect(r.x, r.yMax - w, r.width, w), c);
            Fill(new Rect(r.x, r.y, w, r.height), c);
            Fill(new Rect(r.xMax - w, r.y, w, r.height), c);
        }

        void Text(Rect r, string s, int size, Color c, TextAnchor a = TextAnchor.MiddleCenter, bool bold = true, bool shadow = true)
        {
            style.fontSize = size;
            style.alignment = a;
            style.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            if (shadow)
            {
                style.normal.textColor = new Color(0, 0, 0, c.a * 0.7f);
                GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), s, style);
            }
            style.normal.textColor = c;
            GUI.Label(r, s, style);
        }

        void WrapText(Rect r, string s, int size, Color c)
        {
            style.wordWrap = true;
            style.clipping = TextClipping.Clip;
            Text(r, s, size, c, TextAnchor.UpperLeft, false, false);
            style.wordWrap = false;
            style.clipping = TextClipping.Overflow;
        }

        bool Button(Rect r, string label, bool selected = false, int size = 24, Color? accent = null)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            Color acc = accent ?? new Color(1f, 0.75f, 0.25f);
            Fill(r, selected ? new Color(acc.r, acc.g, acc.b, 0.9f) : hover ? new Color(1, 1, 1, 0.25f) : new Color(0.08f, 0.1f, 0.14f, 0.75f));
            Frame(r, selected ? Color.white : new Color(1, 1, 1, hover ? 0.7f : 0.25f), 2f);
            Text(r, label, size, selected ? new Color(0.08f, 0.08f, 0.1f) : Color.white, TextAnchor.MiddleCenter, true, !selected);
            bool clicked = GUI.Button(r, GUIContent.none, GUIStyle.none);
            if (clicked) Sfx.Play2D("hit", 0.4f, 0.7f);
            return clicked;
        }

        bool WorldToGui(Vector3 world, out Vector2 gui)
        {
            var cam = GM.cam;
            Vector3 sp = cam.WorldToScreenPoint(world);
            gui = new Vector2(sp.x / scale, (Screen.height - sp.y) / scale);
            return sp.z > 0.1f;
        }

        static string Clock(float t)
        {
            t = Mathf.Max(0f, t);
            int m = (int)(t / 60f);
            int s = (int)(t % 60f);
            return m + ":" + s.ToString("00");
        }

        // ------------------------------------------------------------------ root

        void OnGUI()
        {
            if (GM == null) return;
            EnsureResources();
            scale = Screen.height / 1080f;
            VW = Screen.width / scale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            switch (GM.state)
            {
                case MatchState.MainMenu: DrawMainMenu(); break;
                case MatchState.HeroSelect: DrawHeroSelect(false); break;
                case MatchState.Setup:
                case MatchState.Playing:
                    DrawGameHud();
                    if (GM.heroPickerOpen) DrawHeroSelect(true);
                    else if (GameInput.Key(GKey.Tab)) DrawScoreboard();
                    if (GM.paused) DrawPause();
                    break;
                case MatchState.Ended:
                    DrawEnd();
                    break;
            }
        }

        // ------------------------------------------------------------------ menus

        void DrawMainMenu()
        {
            Fill(new Rect(0, 0, VW, VH), new Color(0.02f, 0.04f, 0.08f, 0.45f));
            float cx = VW / 2f;
            Text(new Rect(cx - 600, 120, 1200, 120), "NEXUS STRIKE", 110, Color.white);
            Text(new Rect(cx - 600, 225, 1200, 40), "5v5 HERO SHOOTER  ·  PAYLOAD ESCORT  ·  " + GM.map.name, 26, new Color(1f, 0.8f, 0.35f));

            float y = 330;
            Text(new Rect(cx - 300, y, 600, 40), "SIDE", 24, new Color(1, 1, 1, 0.7f));
            if (Button(new Rect(cx - 260, y + 45, 250, 60), "ATTACK", GM.playerTeam == Team.Attack, 26, TeamColors.Attack)) GM.playerTeam = Team.Attack;
            if (Button(new Rect(cx + 10, y + 45, 250, 60), "DEFEND", GM.playerTeam == Team.Defend, 26, TeamColors.Defend)) GM.playerTeam = Team.Defend;

            y += 140;
            Text(new Rect(cx - 300, y, 600, 40), "BOT DIFFICULTY", 24, new Color(1, 1, 1, 0.7f));
            string[] diffs = { "EASY", "NORMAL", "HARD" };
            for (int i = 0; i < 3; i++)
                if (Button(new Rect(cx - 260 + i * 177, y + 45, 165, 56), diffs[i], (int)GM.difficulty == i, 22)) GM.difficulty = (Difficulty)i;

            y += 150;
            if (Button(new Rect(cx - 200, y, 400, 80), "PLAY", false, 40, new Color(1f, 0.75f, 0.25f)) || KeyEvent(KeyCode.Return))
                GM.GoToHeroSelect();

            DrawControls(new Rect(cx - 330, y + 120, 660, 220));
        }

        void DrawControls(Rect r)
        {
            Fill(r, new Color(0, 0, 0, 0.45f));
            string[] lines =
            {
                "WASD  Move        SPACE  Jump        MOUSE  Aim",
                "LMB  Primary fire        RMB  Secondary / Ability",
                "SHIFT  Ability 1        E  Ability 2        Q  Ultimate",
                "R  Reload        TAB  Scoreboard        H  Change hero (in spawn)",
                "ESC  Pause        F1  Toggle controls"
            };
            for (int i = 0; i < lines.Length; i++)
                Text(new Rect(r.x, r.y + 18 + i * 38, r.width, 34), lines[i], 21, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleCenter, false);
        }

        void DrawHeroSelect(bool inGame)
        {
            Fill(new Rect(0, 0, VW, VH), new Color(0.02f, 0.03f, 0.06f, inGame ? 0.8f : 0.6f));
            Text(new Rect(0, 40, VW, 70), inGame ? "CHANGE HERO" : "CHOOSE YOUR HERO", 56, Color.white);
            Text(new Rect(0, 108, VW, 36), (GM.playerTeam == Team.Attack ? "ATTACK" : "DEFENSE") + "  ·  " + GM.map.name + "  ·  1 TANK / 2 DAMAGE / 2 SUPPORT", 22,
                TeamColors.Ally);

            var heroes = HeroRoster.All;
            int n = heroes.Count;
            float gap = 12f;
            float cw = Mathf.Min(230f, (VW - 80f - gap * (n - 1)) / n);
            float ch = 300f;
            float x0 = (VW - (cw * n + gap * (n - 1))) / 2f;
            float y0 = 170f;
            for (int i = 0; i < n; i++)
            {
                var h = heroes[i];
                var r = new Rect(x0 + i * (cw + gap), y0, cw, ch);
                bool sel = selectedHero == h.id;
                bool current = inGame && GM.player != null && GM.player.def.id == h.id;
                bool hover = r.Contains(Event.current.mousePosition);
                Fill(r, new Color(0.07f, 0.09f, 0.13f, 0.92f));
                Fill(new Rect(r.x, r.y, r.width, 6), HeroRoster.RoleColor(h.role));
                var portrait = new Rect(r.x + 10, r.y + 16, r.width - 20, 130);
                Fill(portrait, Color.Lerp(h.color, Color.black, 0.35f));
                DrawPortrait(portrait, h);
                Text(new Rect(r.x, r.y + 152, r.width, 36), h.name, 28, Color.white);
                Text(new Rect(r.x, r.y + 186, r.width, 24), h.title, 16, new Color(1, 1, 1, 0.65f), TextAnchor.MiddleCenter, false);
                Text(new Rect(r.x, r.y + 214, r.width, 24), HeroRoster.RoleName(h.role), 18, HeroRoster.RoleColor(h.role));
                Text(new Rect(r.x, r.y + 244, r.width, 22), HpLine(h), 15, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleCenter, false);
                Text(new Rect(r.x, r.y + 270, r.width, 22), "[" + (i + 1) + "]", 15, new Color(1, 1, 1, 0.4f), TextAnchor.MiddleCenter, false);
                Frame(r, sel ? new Color(1f, 0.8f, 0.3f) : current ? TeamColors.Ally : new Color(1, 1, 1, hover ? 0.6f : 0.15f), sel ? 4f : 2f);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { selectedHero = h.id; Sfx.Play2D("hit", 0.4f, 0.8f); }
                if (KeyEvent(KeyCode.Alpha1 + i)) selectedHero = h.id;
            }

            // details
            var sh = HeroRoster.Get(selectedHero);
            var dr = new Rect(x0, y0 + ch + 20, cw * n + gap * (n - 1), 360);
            Fill(dr, new Color(0.05f, 0.07f, 0.1f, 0.88f));
            Text(new Rect(dr.x + 24, dr.y + 14, 600, 44), sh.name + "  —  " + sh.title, 32, Color.white, TextAnchor.MiddleLeft);
            WrapText(new Rect(dr.x + 24, dr.y + 60, dr.width * 0.42f, 120), sh.description, 20, new Color(1, 1, 1, 0.85f));
            Text(new Rect(dr.x + 24, dr.y + 170, 600, 30), "HP " + HpLine(sh) + "   ·   SPEED " + sh.speed.ToString("0.0"), 18, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleLeft, false);
            float ax = dr.x + dr.width * 0.46f;
            for (int i = 0; i < sh.abilities.Length; i++)
            {
                var a = sh.abilities[i];
                float ay = dr.y + 18 + i * 64;
                Fill(new Rect(ax, ay, 80, 50), new Color(1, 1, 1, 0.1f));
                Text(new Rect(ax, ay, 80, 50), a.key, 18, new Color(1f, 0.8f, 0.35f));
                Text(new Rect(ax + 92, ay - 2, 500, 28), a.name, 21, Color.white, TextAnchor.MiddleLeft);
                Text(new Rect(ax + 92, ay + 24, dr.xMax - ax - 110, 24), a.desc, 16, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleLeft, false);
            }

            float by = dr.yMax + 22;
            if (inGame)
            {
                if (Button(new Rect(VW / 2 - 210, by, 200, 64), "CANCEL", false, 26)) GM.heroPickerOpen = false;
                if (Button(new Rect(VW / 2 + 10, by, 200, 64), "SWAP", false, 28, new Color(1f, 0.75f, 0.25f)) || KeyEvent(KeyCode.Return))
                    GM.RequestHeroChange(selectedHero);
            }
            else
            {
                if (Button(new Rect(VW / 2 - 330, by, 200, 64), "BACK", false, 26)) GM.ReturnToMenu();
                if (Button(new Rect(VW / 2 - 110, by, 440, 64), "LOCK IN " + sh.name, true, 30) || KeyEvent(KeyCode.Return))
                    GM.StartMatch(selectedHero);
            }
        }

        static string HpLine(HeroDefinition h)
        {
            string s = h.health.ToString("0");
            if (h.armor > 0) s += " + " + h.armor.ToString("0") + " ARMOR";
            if (h.shield > 0) s += " + " + h.shield.ToString("0") + " SHIELD";
            return s;
        }

        void DrawPortrait(Rect r, HeroDefinition h)
        {
            // stylised bust built from rectangles in the hero's palette
            float cx = r.center.x;
            Color main = h.color, dark = Color.Lerp(h.color, Color.black, 0.5f);
            float w = h.role == HeroRole.Tank ? 1.35f : 1f;
            Fill(new Rect(cx - 48 * w, r.yMax - 52, 96 * w, 52), main);
            Fill(new Rect(cx - 56 * w, r.yMax - 58, 26 * w, 22), dark);
            Fill(new Rect(cx + 30 * w, r.yMax - 58, 26 * w, 22), dark);
            Tex(new Rect(cx - 30, r.y + 18, 60, 64), disc, new Color(0.85f, 0.72f, 0.6f));
            Fill(new Rect(cx - 24, r.y + 42, 48, 9), TeamColors.Ally);
            Text(new Rect(r.x, r.y + 2, r.width - 6, 24), HeroRoster.RoleName(h.role), 12, new Color(1, 1, 1, 0.5f), TextAnchor.UpperRight, true, false);
        }

        void DrawPause()
        {
            Fill(new Rect(0, 0, VW, VH), new Color(0, 0, 0, 0.65f));
            float cx = VW / 2f;
            Text(new Rect(cx - 300, 200, 600, 80), "PAUSED", 64, Color.white);
            if (Button(new Rect(cx - 180, 330, 360, 64), "RESUME", false, 28)) GM.SetPaused(false);
            Text(new Rect(cx - 300, 420, 600, 30), "MOUSE SENSITIVITY  " + PlayerBrain.sensitivity.ToString("0.00"), 20, Color.white);
            PlayerBrain.sensitivity = GUI.HorizontalSlider(new Rect(cx - 180, 460, 360, 24), PlayerBrain.sensitivity, 0.2f, 6f);
            Text(new Rect(cx - 300, 490, 600, 30), "VOLUME  " + Mathf.RoundToInt(Sfx.masterVolume * 100) + "%", 20, Color.white);
            Sfx.masterVolume = GUI.HorizontalSlider(new Rect(cx - 180, 530, 360, 24), Sfx.masterVolume, 0f, 1f);
            if (Button(new Rect(cx - 180, 580, 360, 64), "RESTART (HERO SELECT)", false, 22)) GM.GoToHeroSelect();
            if (Button(new Rect(cx - 180, 660, 360, 64), "QUIT TO MENU", false, 24)) GM.ReturnToMenu();
            DrawControls(new Rect(cx - 330, 760, 660, 220));
        }

        void DrawEnd()
        {
            bool won = GM.winner == GM.playerTeam;
            Fill(new Rect(0, 0, VW, VH), new Color(0, 0, 0, 0.5f));
            float t = Mathf.Clamp01((Time.time - GM.endTime) * 2f);
            Text(new Rect(0, 120, VW, 160), won ? "VICTORY" : "DEFEAT", (int)Mathf.Lerp(60, 140, t),
                won ? new Color(1f, 0.82f, 0.3f) : new Color(1f, 0.35f, 0.3f));
            Text(new Rect(0, 270, VW, 40), GM.winner == Team.Attack ? "THE PAYLOAD REACHED ITS DESTINATION" : "THE DEFENSE HELD", 26, Color.white);
            DrawScoreboardTable(new Rect(VW / 2 - 560, 340, 1120, 520));
            if (Button(new Rect(VW / 2 - 330, 900, 320, 70), "PLAY AGAIN", true, 28) || KeyEvent(KeyCode.Return)) GM.GoToHeroSelect();
            if (Button(new Rect(VW / 2 + 10, 900, 320, 70), "MAIN MENU", false, 28)) GM.ReturnToMenu();
        }

        // ------------------------------------------------------------------ combat HUD

        void DrawGameHud()
        {
            var p = GM.player;
            if (p == null) return;

            DrawWorldMarkers(p);
            DrawDamageIndicators(p);
            DrawObjective();
            DrawTeamBars();
            DrawKillFeed();
            DrawAnnouncements();
            DrawCallouts();

            if (p.alive)
            {
                if (p.HealthFrac < 0.35f)
                {
                    float a = (0.35f - p.HealthFrac) * 1.3f * (0.8f + 0.2f * Mathf.Sin(Time.time * 6f));
                    Fill(new Rect(0, 0, VW, 60), new Color(0.8f, 0, 0, a));
                    Fill(new Rect(0, VH - 60, VW, 60), new Color(0.8f, 0, 0, a));
                    Fill(new Rect(0, 0, 60, VH), new Color(0.8f, 0, 0, a));
                    Fill(new Rect(VW - 60, 0, 60, VH), new Color(0.8f, 0, 0, a));
                }
                DrawCrosshair(p);
                DrawHealth(p);
                DrawWeapon(p);
                DrawAbilities(p);
                DrawStatus(p);
                if (GM.PlayerCanChangeHero && !GM.heroPickerOpen)
                    Text(new Rect(VW / 2 - 300, VH - 250, 600, 30), "PRESS [H] TO CHANGE HERO", 18, new Color(1, 1, 1, 0.7f));
            }
            else
            {
                float remain = Mathf.Max(0f, GM.player.respawnAt - Time.time);
                Fill(new Rect(0, VH / 2 + 120, VW, 110), new Color(0, 0, 0, 0.45f));
                Text(new Rect(0, VH / 2 + 125, VW, 60), "RESPAWNING IN " + Mathf.CeilToInt(remain), 44, Color.white);
                Text(new Rect(0, VH / 2 + 185, VW, 34), "PRESS [H] TO CHANGE HERO", 20, new Color(1, 1, 1, 0.75f));
            }

            float now = Time.unscaledTime;
            for (int i = 0; i < elims.Count; i++)
            {
                float age = now - elims[i].Value;
                if (age > 2.5f) continue;
                float a = Mathf.Clamp01(2.5f - age);
                bool isElim = elims[i].Key.StartsWith("ELIM");
                Text(new Rect(VW / 2 - 300, VH / 2 + 60 + i * 34, 600, 34), elims[i].Key, isElim ? 26 : 20,
                    isElim ? new Color(1f, 0.3f, 0.25f, a) : new Color(1f, 1f, 1f, a * 0.8f));
            }
            if (showHelp) DrawControls(new Rect(VW / 2 - 330, 200, 660, 220));
        }

        void DrawCrosshair(Combatant p)
        {
            Vector2 c = new Vector2(VW / 2f, VH / 2f);
            Color col = new Color(0.4f, 1f, 0.9f, 0.95f);
            switch (p.kit.crosshair)
            {
                case CrosshairStyle.Dot:
                    Tex(new Rect(c.x - 4, c.y - 4, 8, 8), disc, col);
                    Tex(new Rect(c.x - 18, c.y - 18, 36, 36), ring, new Color(col.r, col.g, col.b, 0.35f));
                    break;
                case CrosshairStyle.Circle:
                    Tex(new Rect(c.x - 26, c.y - 26, 52, 52), ring, col);
                    Tex(new Rect(c.x - 2.5f, c.y - 2.5f, 5, 5), disc, col);
                    break;
                case CrosshairStyle.Wide:
                    Tex(new Rect(c.x - 45, c.y - 45, 90, 90), ring, new Color(col.r, col.g, col.b, 0.6f));
                    Tex(new Rect(c.x - 3, c.y - 3, 6, 6), disc, col);
                    break;
                default:
                    float gap = p.kit.zoomFov > 0f ? 3f : 6f;
                    Fill(new Rect(c.x - 1, c.y - gap - 10, 2, 10), col);
                    Fill(new Rect(c.x - 1, c.y + gap, 2, 10), col);
                    Fill(new Rect(c.x - gap - 10, c.y - 1, 10, 2), col);
                    Fill(new Rect(c.x + gap, c.y - 1, 10, 2), col);
                    Fill(new Rect(c.x - 1, c.y - 1, 2, 2), col);
                    break;
            }
            if (p.kit.reloading)
            {
                Fill(new Rect(c.x - 40, c.y + 40, 80, 5), new Color(0, 0, 0, 0.5f));
                Fill(new Rect(c.x - 40, c.y + 40, 80 * p.kit.ReloadProgress, 5), Color.white);
            }

            // hit marker
            float age = Time.unscaledTime - hitTime;
            float kAge = Time.unscaledTime - killTime;
            if (age < 0.18f || kAge < 0.35f)
            {
                bool kill = kAge < 0.35f;
                Color hc = kill ? new Color(1f, 0.2f, 0.2f) : hitHead ? new Color(1f, 0.85f, 0.2f) : Color.white;
                float len = kill ? 16f : 11f;
                var m = GUI.matrix;
                GUIUtility.RotateAroundPivot(45f, c);
                Fill(new Rect(c.x - 2, c.y - 12 - len, 4, len), hc);
                Fill(new Rect(c.x - 2, c.y + 12, 4, len), hc);
                Fill(new Rect(c.x - 12 - len, c.y - 2, len, 4), hc);
                Fill(new Rect(c.x + 12, c.y - 2, len, 4), hc);
                GUI.matrix = m;
            }
        }

        void DrawHealth(Combatant p)
        {
            float x = 60, y = VH - 150, w = 380, h = 26;
            Text(new Rect(x, y - 78, 400, 44), p.def.name, 34, Color.white, TextAnchor.MiddleLeft);
            Text(new Rect(x, y - 42, 400, 30), Mathf.CeilToInt(p.Total + p.overHealth) + " / " + Mathf.CeilToInt(p.MaxTotal), 26, Color.white, TextAnchor.MiddleLeft);

            float maxTotal = p.MaxTotal + p.overHealth;
            int segments = Mathf.CeilToInt(maxTotal / 25f);
            float segW = w / segments;
            for (int i = 0; i < segments; i++)
            {
                float lo = i * 25f, hi = lo + 25f;
                var r = new Rect(x + i * segW, y, segW - 2, h);
                Fill(r, new Color(0, 0, 0, 0.45f));
                // pool boundaries
                float hEnd = p.maxHealth, aEnd = hEnd + p.maxArmor, sEnd = aEnd + p.maxShield;
                Color c; float filled;
                if (lo < hEnd) { c = Color.white; filled = Mathf.Clamp01((p.health - lo) / 25f); }
                else if (lo < aEnd) { c = new Color(1f, 0.75f, 0.25f); filled = Mathf.Clamp01((p.armor - (lo - hEnd)) / 25f); }
                else if (lo < sEnd) { c = new Color(0.35f, 0.75f, 1f); filled = Mathf.Clamp01((p.shield - (lo - aEnd)) / 25f); }
                else { c = new Color(0.4f, 1f, 0.5f); filled = Mathf.Clamp01((p.overHealth - (lo - sEnd)) / 25f); }
                if (filled > 0f) Fill(new Rect(r.x, r.y, r.width * filled, r.height), c);
                if (hi > maxTotal + 0.1f) break;
            }
            if (p.DamageReduction > 0f) Text(new Rect(x, y + 30, 400, 26), "DAMAGE REDUCTION " + Mathf.RoundToInt(p.DamageReduction * 100) + "%", 18, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleLeft);
        }

        void DrawWeapon(Combatant p)
        {
            var k = p.kit;
            float x = VW - 330, y = VH - 170;
            if (k.maxAmmo > 0)
            {
                Text(new Rect(x, y, 170, 80), k.reloading ? "--" : k.ammo.ToString(), 64, Color.white, TextAnchor.MiddleRight);
                Text(new Rect(x + 175, y + 20, 120, 50), "/ " + k.maxAmmo, 28, new Color(1, 1, 1, 0.65f), TextAnchor.MiddleLeft);
            }
            Text(new Rect(x - 100, y + 70, 390, 30), k.primaryName.ToUpper(), 18, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleRight);
            if (!string.IsNullOrEmpty(k.statusText))
            {
                Text(new Rect(x - 100, y - 50, 390, 30), k.statusText, 20, new Color(0.6f, 0.9f, 1f), TextAnchor.MiddleRight);
                if (k.statusFrac >= 0f)
                {
                    Fill(new Rect(x + 90, y - 18, 200, 6), new Color(0, 0, 0, 0.5f));
                    Fill(new Rect(x + 90, y - 18, 200 * Mathf.Clamp01(k.statusFrac), 6), new Color(0.6f, 0.9f, 1f));
                }
            }
        }

        void DrawAbilities(Combatant p)
        {
            var k = p.kit;
            var list = new List<Ability>();
            if (k.secondaryAbility != null) list.Add(k.secondaryAbility);
            if (k.ab1 != null) list.Add(k.ab1);
            if (k.ab2 != null) list.Add(k.ab2);
            float bw = 110, bh = 84, gap = 12;
            float x = VW - 360 - list.Count * (bw + gap);
            float y = VH - 120;
            foreach (var a in list)
            {
                var r = new Rect(x, y, bw, bh);
                bool ready = a.Ready;
                Fill(r, ready ? new Color(0.1f, 0.14f, 0.2f, 0.8f) : new Color(0.05f, 0.05f, 0.07f, 0.85f));
                if (!ready) Fill(new Rect(r.x, r.y + r.height * (1f - a.Fraction), r.width, r.height * a.Fraction), new Color(1, 1, 1, 0.08f));
                if (a.Active) Frame(r, new Color(1f, 0.85f, 0.3f), 3f);
                else Frame(r, ready ? new Color(1, 1, 1, 0.6f) : new Color(1, 1, 1, 0.15f), 2f);
                Text(new Rect(r.x, r.y + 4, r.width, 22), a.key, 15, new Color(1f, 0.8f, 0.35f));
                if (ready) Text(new Rect(r.x + 4, r.y + 26, r.width - 8, 50), a.name.ToUpper(), 14, Color.white);
                else Text(new Rect(r.x, r.y + 26, r.width, 50), Mathf.CeilToInt(a.remaining).ToString(), 34, Color.white);
                x += bw + gap;
            }

            // ultimate meter
            var ur = new Rect(VW / 2 - 60, VH - 150, 120, 120);
            bool ultReady = p.UltReady;
            float pulse = ultReady ? 0.75f + 0.25f * Mathf.Sin(Time.time * 6f) : 1f;
            Tex(ur, disc, new Color(0.05f, 0.07f, 0.1f, 0.75f));
            Tex(ur, ring, ultReady ? new Color(1f, 0.82f, 0.3f, pulse) : new Color(1, 1, 1, 0.25f));
            if (k.ultActive) Text(ur, "ACTIVE", 22, new Color(1f, 0.85f, 0.3f));
            else if (ultReady) Text(new Rect(ur.x, ur.y + 20, ur.width, 50), "Q", 46, new Color(1f, 0.85f, 0.3f, pulse));
            else Text(new Rect(ur.x, ur.y + 20, ur.width, 50), Mathf.FloorToInt(p.UltFrac * 100f) + "%", 34, Color.white);
            Text(new Rect(ur.x - 60, ur.y + 72, ur.width + 120, 30), k.ultName.ToUpper(), 14, new Color(1, 1, 1, 0.7f));
            // charge bar under the dial
            Fill(new Rect(ur.x + 10, ur.yMax + 4, ur.width - 20, 5), new Color(0, 0, 0, 0.5f));
            Fill(new Rect(ur.x + 10, ur.yMax + 4, (ur.width - 20) * p.UltFrac, 5), new Color(1f, 0.82f, 0.3f));
        }

        void DrawStatus(Combatant p)
        {
            string s = null;
            if (p.IsStunned) s = "STUNNED";
            else if (p.IsRooted) s = "ROOTED";
            if (s != null) Text(new Rect(VW / 2 - 200, VH / 2 - 120, 400, 50), s, 36, new Color(1f, 0.6f, 0.2f));
            if (GM.state == MatchState.Setup && p.team == Team.Attack)
                Text(new Rect(VW / 2 - 400, VH / 2 - 180, 800, 50), "DOORS OPEN IN " + Mathf.CeilToInt(GM.SetupRemaining), 34, Color.white);
        }

        void DrawObjective()
        {
            var pl = GM.payload;
            float w = 520, x = VW / 2 - w / 2, y = 26;
            bool attack = GM.playerTeam == Team.Attack;
            Color mine = TeamColors.Ally;
            Fill(new Rect(x - 10, y - 8, w + 20, 108), new Color(0.03f, 0.05f, 0.08f, 0.55f));
            string title = GM.state == MatchState.Setup ? (attack ? "PREPARE TO ATTACK" : "SET UP DEFENSES") : (attack ? "ESCORT THE PAYLOAD" : "STOP THE PAYLOAD");
            Text(new Rect(x, y, w, 26), title, 18, new Color(1, 1, 1, 0.85f));
            string time = GM.state == MatchState.Setup ? Clock(GM.SetupRemaining) : GM.overtime ? "OVERTIME" : Clock(GM.TimeLeft);
            Color timeCol = GM.overtime ? new Color(1f, 0.55f, 0.2f) : GM.TimeLeft < 30f && GM.state == MatchState.Playing ? new Color(1f, 0.4f, 0.35f) : Color.white;
            Text(new Rect(x, y + 22, w, 40), time, 32, timeCol);

            var bar = new Rect(x + 10, y + 70, w - 20, 12);
            Fill(bar, new Color(0, 0, 0, 0.6f));
            Fill(new Rect(bar.x, bar.y, bar.width * pl.Progress, bar.height), attack ? mine : TeamColors.Enemy);
            foreach (var cp in pl.checkpoints)
            {
                float fx = bar.x + bar.width * (cp / pl.total);
                Fill(new Rect(fx - 2, bar.y - 5, 4, bar.height + 10), Color.white);
            }
            float px = bar.x + bar.width * pl.Progress;
            Tex(new Rect(px - 10, bar.y - 4, 20, 20), disc, Color.white);

            string status;
            Color sc = Color.white;
            if (pl.Contested) { status = "CONTESTED"; sc = new Color(1f, 0.6f, 0.2f); }
            else if (pl.Moving) { status = ">>  x" + pl.attackersOn; sc = attack ? mine : TeamColors.Enemy; }
            else if (pl.RollingBack) { status = "<<  ROLLING BACK"; sc = new Color(1, 1, 1, 0.8f); }
            else status = Mathf.RoundToInt(pl.Progress * 100f) + "%";
            Text(new Rect(x, y + 84, w, 26), status, 18, sc);
            if (GM.overtime)
            {
                Fill(new Rect(x + 10, y + 108, (w - 20) * Mathf.Clamp01(GM.overtimeGrace / 3f), 4), new Color(1f, 0.55f, 0.2f));
            }
        }

        void DrawTeamBars()
        {
            var p = GM.player;
            DrawRoster(p.team, new Vector2(VW / 2 - 290 - 5 * 70, 30), true);
            DrawRoster(p.team == Team.Attack ? Team.Defend : Team.Attack, new Vector2(VW / 2 + 290, 30), false);
        }

        void DrawRoster(Team t, Vector2 origin, bool ally)
        {
            int i = 0;
            Color tc = ally ? TeamColors.Ally : TeamColors.Enemy;
            foreach (var c in Combatant.All)
            {
                if (c.team != t) continue;
                var r = new Rect(origin.x + i * 70, origin.y, 64, 64);
                Fill(r, c.alive ? new Color(tc.r * 0.35f, tc.g * 0.35f, tc.b * 0.35f, 0.85f) : new Color(0.1f, 0.1f, 0.1f, 0.85f));
                Fill(new Rect(r.x, r.y, r.width, 4), HeroRoster.RoleColor(c.def.role));
                string tag = c.def.name.Length > 4 ? c.def.name.Substring(0, 4) : c.def.name;
                Text(new Rect(r.x, r.y + 6, r.width, 26), tag, 15, c.alive ? Color.white : new Color(1, 1, 1, 0.4f));
                if (!c.alive)
                {
                    Text(new Rect(r.x, r.y + 28, r.width, 30), Mathf.CeilToInt(Mathf.Max(0f, c.respawnAt - Time.time)).ToString(), 22, new Color(1f, 0.4f, 0.35f));
                }
                else
                {
                    Fill(new Rect(r.x + 6, r.y + 50, r.width - 12, 6), new Color(0, 0, 0, 0.6f));
                    Fill(new Rect(r.x + 6, r.y + 50, (r.width - 12) * c.HealthFrac, 6), c.HealthFrac < 0.4f ? new Color(1f, 0.35f, 0.3f) : Color.white);
                    if (ally) Text(new Rect(r.x, r.y + 28, r.width, 20), c.UltReady ? "ULT" : Mathf.FloorToInt(c.UltFrac * 100) + "%", 13,
                        c.UltReady ? new Color(1f, 0.85f, 0.3f) : new Color(1, 1, 1, 0.6f));
                }
                if (c.isPlayer) Frame(r, new Color(1f, 0.85f, 0.3f), 2f);
                i++;
            }
        }

        void DrawKillFeed()
        {
            float y = 120;
            for (int i = GM.feed.Count - 1; i >= 0; i--)
            {
                var f = GM.feed[i];
                float a = Mathf.Clamp01(7f - (Time.time - f.time));
                var r = new Rect(VW - 560, y, 520, 32);
                Fill(r, f.involvesPlayer ? new Color(1f, 0.85f, 0.3f, 0.25f * a) : new Color(0, 0, 0, 0.4f * a));
                Text(new Rect(r.x + 10, r.y, 200, 32), f.killer, 17, new Color(f.killerColor.r, f.killerColor.g, f.killerColor.b, a), TextAnchor.MiddleLeft);
                Text(new Rect(r.x + 190, r.y, 140, 32), "[" + Shorten(f.source) + "]", 13, new Color(1, 1, 1, 0.7f * a));
                Text(new Rect(r.x + 320, r.y, 190, 32), f.victim, 17, new Color(f.victimColor.r, f.victimColor.g, f.victimColor.b, a), TextAnchor.MiddleRight);
                y += 36;
            }
        }

        static string Shorten(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length > 16 ? s.Substring(0, 16) : s;
        }

        void DrawAnnouncements()
        {
            float y = 170;
            foreach (var a in GM.announcements)
            {
                if (GM.state == MatchState.Ended) continue;
                float age = Time.unscaledTime - a.start;
                float alpha = Mathf.Clamp01(Mathf.Min(age * 4f, (a.until - Time.unscaledTime) * 2f));
                float size = Mathf.Lerp(54f, 40f, Mathf.Clamp01(age * 3f));
                Text(new Rect(0, y, VW, 60), a.text, (int)size, new Color(a.color.r, a.color.g, a.color.b, alpha));
                y += 58;
            }
        }

        void DrawCallouts()
        {
            float y = VH / 2 - 80;
            for (int i = 0; i < callouts.Count; i++)
            {
                float age = Time.unscaledTime - callouts[i].Value;
                if (age > 3f) continue;
                float a = Mathf.Clamp01(3f - age);
                Color c = calloutColors[i];
                Text(new Rect(40, y, 600, 30), callouts[i].Key, 19, new Color(c.r, c.g, c.b, a), TextAnchor.MiddleLeft);
                y += 32;
            }
        }

        void DrawWorldMarkers(Combatant p)
        {
            var cam = GM.cam;
            // payload marker
            Vector3 pw = GM.payload.Position + Vector3.up * 3.2f;
            Vector2 g;
            bool front = WorldToGui(pw, out g);
            if (!front) { g.x = VW - g.x; g.y = VH - 40; }
            g.x = Mathf.Clamp(g.x, 40, VW - 40);
            g.y = Mathf.Clamp(g.y, 140, VH - 200);
            Color pc = GM.payload.Contested ? new Color(1f, 0.6f, 0.2f) : GM.payload.Moving ? (GM.playerTeam == Team.Attack ? TeamColors.Ally : TeamColors.Enemy) : Color.white;
            Tex(new Rect(g.x - 16, g.y - 16, 32, 32), ring, pc);
            Tex(new Rect(g.x - 7, g.y - 7, 14, 14), disc, pc);
            Text(new Rect(g.x - 60, g.y + 16, 120, 22), Mathf.RoundToInt(Vector3.Distance(p.Feet, GM.payload.Position)) + "m", 15, pc);

            Vector3 eye = cam.transform.position;
            foreach (var c in Combatant.All)
            {
                if (c == p || !c.alive) continue;
                Vector3 head = c.Feet + Vector3.up * (c.def.height + 0.45f);
                float dist = Vector3.Distance(eye, head);
                bool ally = c.team == p.team;
                if (!ally)
                {
                    if (dist > 45f) continue;
                    if (!CombatUtil.LineOfSight(eye, c.ChestPos) && !CombatUtil.LineOfSight(eye, c.HeadPos)) continue;
                }
                if (!WorldToGui(head, out g)) continue;
                float bw = Mathf.Lerp(80f, 40f, Mathf.Clamp01(dist / 40f));
                Color tc = ally ? TeamColors.Ally : TeamColors.Enemy;
                Fill(new Rect(g.x - bw / 2 - 1, g.y - 1, bw + 2, 7), new Color(0, 0, 0, 0.6f));
                Fill(new Rect(g.x - bw / 2, g.y, bw * Mathf.Clamp01(c.HealthFrac), 5), tc);
                if (c.overHealth > 0f) Fill(new Rect(g.x - bw / 2, g.y - 3, bw * Mathf.Clamp01(c.overHealth / c.MaxTotal), 2), new Color(0.4f, 1f, 0.5f));
                if (ally && dist < 60f) Text(new Rect(g.x - 80, g.y - 22, 160, 20), c.def.name, 13, new Color(tc.r, tc.g, tc.b, 0.9f));
                if (c.IsStunned) Text(new Rect(g.x - 60, g.y - 40, 120, 20), "STUNNED", 13, new Color(1f, 0.6f, 0.2f));
                if (ally && p.def.role == HeroRole.Support && c.HealthFrac < 0.5f)
                    Text(new Rect(g.x - 20, g.y - 46, 40, 24), "+", 26, new Color(1f, 0.3f, 0.3f, 0.7f + 0.3f * Mathf.Sin(Time.time * 8f)));
            }
        }

        void DrawDamageIndicators(Combatant p)
        {
            Vector2 c = new Vector2(VW / 2f, VH / 2f);
            for (int i = damageDirs.Count - 1; i >= 0; i--)
            {
                float age = Time.unscaledTime - damageDirs[i].Value;
                if (age > 1.2f) { damageDirs.RemoveAt(i); continue; }
                Vector3 to = damageDirs[i].Key - p.Feet;
                float worldAng = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                float rel = Mathf.DeltaAngle(p.yaw, worldAng);
                var m = GUI.matrix;
                GUIUtility.RotateAroundPivot(rel, c);
                Fill(new Rect(c.x - 50, c.y - 170, 100, 10), new Color(1f, 0.15f, 0.1f, 0.85f * (1f - age / 1.2f)));
                GUI.matrix = m;
            }
        }

        // ------------------------------------------------------------------ scoreboard

        void DrawScoreboard()
        {
            Fill(new Rect(0, 0, VW, VH), new Color(0, 0, 0, 0.35f));
            DrawScoreboardTable(new Rect(VW / 2 - 560, 170, 1120, 560));
        }

        void DrawScoreboardTable(Rect r)
        {
            Fill(r, new Color(0.04f, 0.05f, 0.08f, 0.88f));
            string[] heads = { "HERO", "PLAYER", "E", "FB", "D", "DMG", "HEAL", "BLOCK" };
            float[] cols = { 0, 200, 420, 500, 580, 660, 820, 960 };
            float y = r.y + 16;
            for (int ti = 0; ti < 2; ti++)
            {
                Team t = ti == 0 ? GM.playerTeam : (GM.playerTeam == Team.Attack ? Team.Defend : Team.Attack);
                Color tc = ti == 0 ? TeamColors.Ally : TeamColors.Enemy;
                Text(new Rect(r.x + 20, y, 400, 30), (ti == 0 ? "YOUR TEAM — " : "ENEMY TEAM — ") + TeamColors.Name(t), 20, tc, TextAnchor.MiddleLeft);
                y += 34;
                for (int h = 0; h < heads.Length; h++)
                    Text(new Rect(r.x + 20 + cols[h], y, 150, 24), heads[h], 15, new Color(1, 1, 1, 0.5f), TextAnchor.MiddleLeft);
                y += 26;
                foreach (var c in Combatant.All)
                {
                    if (c.team != t) continue;
                    Fill(new Rect(r.x + 10, y, r.width - 20, 32), c.isPlayer ? new Color(1f, 0.85f, 0.3f, 0.15f) : new Color(tc.r, tc.g, tc.b, 0.08f));
                    string[] vals =
                    {
                        c.def.name, c.displayName, c.eliminations.ToString(), c.finalBlows.ToString(), c.deaths.ToString(),
                        Mathf.RoundToInt(c.damageDone).ToString(), Mathf.RoundToInt(c.healingDone).ToString(), Mathf.RoundToInt(c.damageBlocked).ToString()
                    };
                    for (int h = 0; h < vals.Length; h++)
                        Text(new Rect(r.x + 20 + cols[h], y, 180, 32), vals[h], 18, c.alive ? Color.white : new Color(1, 1, 1, 0.5f), TextAnchor.MiddleLeft, h < 2);
                    y += 36;
                }
                y += 16;
            }
        }
    }
}
