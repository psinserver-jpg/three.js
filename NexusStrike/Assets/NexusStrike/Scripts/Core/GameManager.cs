using System.Collections.Generic;
using UnityEngine;

namespace NexusStrike
{
    public class FeedEntry
    {
        public string killer, victim, source;
        public Color killerColor, victimColor;
        public float time;
        public bool involvesPlayer;
        public bool headshot;
    }

    public class Announcement
    {
        public string text;
        public Color color;
        public float start, until;
    }

    /// <summary>
    /// Match orchestration: builds the world, runs menus -> hero select -> setup -> escort -> result,
    /// spawns/respawns heroes, tracks time, checkpoints, overtime and the kill feed.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;

        public MatchState state = MatchState.MainMenu;
        public Team playerTeam = Team.Attack;
        public Difficulty difficulty = Difficulty.Normal;
        public GameMode mode = GameMode.Escort;
        public TrainingRange training;
        public bool infiniteUlt;
        public bool noCooldowns;
        readonly List<Vector2> recentPlayerDamage = new List<Vector2>();
        public float totalTrainingDamage;

        public Camera cam;
        public MapData map;
        public NavGraph nav;
        public Payload payload;
        public Hud hud;
        public PortraitStudio portraits;
        public Combatant player;

        public const float SetupTime = 15f;
        public const float InitialTime = 150f;
        public const float CheckpointBonus = 100f;
        public const float RespawnTime = 10f;

        float timeLeft;
        float setupEnd;
        public bool overtime;
        public float overtimeGrace;
        public bool paused;
        public bool heroPickerOpen;
        public Team winner;
        public float endTime;
        string pendingHeroId;
        public string lastHeroId = "vex";

        public readonly List<FeedEntry> feed = new List<FeedEntry>();
        public readonly List<Announcement> announcements = new List<Announcement>();

        static readonly string[] BotNames =
        {
            "Ash", "Brine", "Cobalt", "Dune", "Echo", "Flint", "Gale", "Haven", "Iris", "Jett",
            "Kade", "Lark", "Mesa", "Nyx", "Onyx", "Pike", "Quill", "Rune", "Sable", "Tarn"
        };

        public float TimeLeft { get { return timeLeft; } }
        public float SetupRemaining { get { return Mathf.Max(0f, setupEnd - Time.time); } }
        public bool InMatch { get { return state == MatchState.Setup || state == MatchState.Playing; } }
        public bool InputBlocked { get { return paused || heroPickerOpen || !InMatch; } }

        public bool Training { get { return mode == GameMode.Training; } }

        /// <summary>Player damage per second over the last 5 seconds (training range readout).</summary>
        public float RecentDps
        {
            get
            {
                float sum = 0f;
                for (int i = recentPlayerDamage.Count - 1; i >= 0; i--)
                {
                    if (Time.time - recentPlayerDamage[i].x > 5f) { recentPlayerDamage.RemoveAt(i); continue; }
                    sum += recentPlayerDamage[i].y;
                }
                return sum / 5f;
            }
        }

        public SpawnRoom ActiveSpawn(Team t)
        {
            if (Training) return training.spawn;
            int phase = Mathf.Clamp(payload.checkpointsReached, 0, 2);
            return t == Team.Attack ? map.attackSpawns[phase] : map.defendSpawns[phase];
        }

        // ------------------------------------------------------------------ lifecycle

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 144;
            SetupScene();
            Sfx.Init(transform);
            map = MapBuilder.Build();
            Physics.SyncTransforms();
            nav = NavGraph.Build(map.bounds);
            foreach (Transform child in map.root)
                if (child.name == "Geometry" || child.name == "Decor") StaticBatchingUtility.Combine(child.gameObject);
            Debug.Log("[NexusStrike] Map built. Nav nodes: " + nav.nodes.Count);

            training = TrainingRangeBuilder.Build();
            StaticBatchingUtility.Combine(training.root.gameObject);

            var pgo = new GameObject("Payload");
            payload = pgo.AddComponent<Payload>();
            payload.Init(map.path, map.checkpointFractions);

            hud = gameObject.AddComponent<Hud>();
            portraits = PortraitStudio.Create(transform);
            state = MatchState.MainMenu;
        }

        void SetupScene()
        {
            foreach (var c in Camera.allCameras) c.gameObject.SetActive(false);
            var oldLight = GameObject.Find("Directional Light");
            if (oldLight != null) oldLight.SetActive(false);

            var camGo = new GameObject("MainCamera");
            camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 900f;
            cam.fieldOfView = PlayerBrain.BaseFov;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.56f, 0.74f, 0.93f);
            cam.cullingMask &= ~(1 << PortraitStudio.Layer);
            camGo.AddComponent<AudioListener>();

            var lightGo = new GameObject("Sun");
            var sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            lightGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.86f);
            RenderSettings.ambientEquatorColor = new Color(0.6f, 0.58f, 0.54f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.33f, 0.3f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.68f, 0.79f, 0.92f);
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = 420f;
            QualitySettings.shadowDistance = 120f;
        }

        void Update()
        {
            UpdateCursor();
            portraits.SetActive(state == MatchState.HeroSelect || heroPickerOpen);

            if (InMatch && GameInput.KeyDown(GKey.Escape))
            {
                if (heroPickerOpen) heroPickerOpen = false;
                else SetPaused(!paused);
            }

            switch (state)
            {
                case MatchState.MainMenu:
                case MatchState.HeroSelect:
                    MenuCamera();
                    break;
                case MatchState.Setup:
                case MatchState.Playing:
                    if (!paused) TickMatch(Time.deltaTime);
                    break;
                case MatchState.Ended:
                    break;
            }

            for (int i = announcements.Count - 1; i >= 0; i--)
                if (Time.unscaledTime > announcements[i].until) announcements.RemoveAt(i);
            for (int i = feed.Count - 1; i >= 0; i--)
                if (Time.time - feed[i].time > 7f) feed.RemoveAt(i);
        }

        void UpdateCursor()
        {
            bool locked = InMatch && !paused && !heroPickerOpen;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        public void SetPaused(bool p)
        {
            paused = p;
            Time.timeScale = p ? 0f : 1f;
        }

        void MenuCamera()
        {
            float t = Time.unscaledTime * 0.05f;
            Vector3 c = payload.Position + Vector3.up * 2f;
            Vector3 pos = c + new Vector3(Mathf.Sin(t) * 22f, 9f, Mathf.Cos(t) * 22f);
            cam.transform.position = pos;
            cam.transform.LookAt(c);
            cam.fieldOfView = 60f;
        }

        // ------------------------------------------------------------------ flow

        public void GoToHeroSelect()
        {
            ClearMatch();
            SetPaused(false);
            state = MatchState.HeroSelect;
        }

        public void ReturnToMenu()
        {
            ClearMatch();
            SetPaused(false);
            state = MatchState.MainMenu;
        }

        public void StartMatch(string heroId)
        {
            if (Training) { StartTraining(heroId); return; }
            ClearMatch();
            lastHeroId = heroId;
            payload.ResetState();
            foreach (var hp in map.healthPacks) hp.ResetState();
            RecolorSpawns();

            var playerDef = HeroRoster.Get(heroId);
            var names = new List<string>(BotNames);
            Shuffle(names);
            int nameIdx = 0;

            for (int ti = 0; ti < 2; ti++)
            {
                Team team = (Team)ti;
                var roles = new List<HeroRole> { HeroRole.Tank, HeroRole.Damage, HeroRole.Damage, HeroRole.Support, HeroRole.Support };
                var used = new List<string>();
                int slot = 0;
                if (team == playerTeam)
                {
                    roles.Remove(playerDef.role);
                    used.Add(playerDef.id);
                    var room = ActiveSpawn(team);
                    player = SpawnHero(playerDef, team, true, "YOU", slot++, room);
                }
                foreach (var role in roles)
                {
                    var options = HeroRoster.ByRole(role);
                    options.RemoveAll(h => used.Contains(h.id));
                    if (options.Count == 0) options = HeroRoster.ByRole(role);
                    var def = options[Random.Range(0, options.Count)];
                    used.Add(def.id);
                    SpawnHero(def, team, false, names[nameIdx++ % names.Count], slot++, ActiveSpawn(team));
                }
            }

            timeLeft = InitialTime;
            overtime = false;
            overtimeGrace = 3f;
            setupEnd = Time.time + SetupTime;
            state = MatchState.Setup;
            heroPickerOpen = false;
            SetPaused(false);
            Announce(playerTeam == Team.Attack ? "ESCORT THE PAYLOAD" : "STOP THE PAYLOAD", Color.white, 3f);
            Sfx.Play2D("announce", 0.8f);
        }

        void RecolorSpawns()
        {
            foreach (var s in map.attackSpawns) Recolor(s);
            foreach (var s in map.defendSpawns) Recolor(s);
        }

        void Recolor(SpawnRoom s)
        {
            Color c = TeamColors.Relative(s.team);
            for (int i = 0; i < s.glow.Count; i++)
            {
                Color k = c;
                k.a = i == 0 ? 0.35f : 1f;
                s.glow[i].sharedMaterial = MaterialLib.Unlit(k);
            }
        }

        static void Shuffle<T>(List<T> l)
        {
            for (int i = l.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                T t = l[i]; l[i] = l[j]; l[j] = t;
            }
        }

        void ClearMatch()
        {
            var list = new List<Combatant>(Combatant.All);
            foreach (var c in list) if (c != null) Destroy(c.gameObject);
            Combatant.All.Clear();
            player = null;
            feed.Clear();
            announcements.Clear();
            heroPickerOpen = false;
            pendingHeroId = null;
        }

        void StartTraining(string heroId)
        {
            ClearMatch();
            lastHeroId = heroId;
            playerTeam = Team.Attack;
            foreach (var hp in training.healthPacks) hp.ResetState();
            recentPlayerDamage.Clear();
            totalTrainingDamage = 0f;
            player = SpawnHero(HeroRoster.Get(heroId), Team.Attack, true, "YOU", 0, training.spawn);
            int slot = 1;
            foreach (var spot in training.spots)
                SpawnHero(spot.def, spot.team, false, spot.label, slot++, null, spot);
            state = MatchState.Playing;
            heroPickerOpen = false;
            SetPaused(false);
            Announce("NEXUS TRAINING RANGE", new Color(0.35f, 0.85f, 1f), 3f);
            Sfx.Play2D("announce", 0.8f);
        }

        void TickTraining(float dt)
        {
            if (GameInput.KeyDown(GKey.H) && player != null) heroPickerOpen = !heroPickerOpen;
            if (GameInput.KeyDown(GKey.F2)) { infiniteUlt = !infiniteUlt; Announce("INFINITE ULTIMATE " + (infiniteUlt ? "ON" : "OFF"), Color.white, 1.5f); }
            if (GameInput.KeyDown(GKey.F3)) { noCooldowns = !noCooldowns; Announce("NO COOLDOWNS " + (noCooldowns ? "ON" : "OFF"), Color.white, 1.5f); }
            if (GameInput.KeyDown(GKey.F4)) ResetTrainingBots();

            if (player != null && player.alive)
            {
                if (infiniteUlt && !player.kit.ultActive) player.ultCharge = player.def.ultCost;
                if (noCooldowns)
                {
                    var k = player.kit;
                    if (k.ab1 != null) k.ab1.remaining = 0f;
                    if (k.ab2 != null) k.ab2.remaining = 0f;
                    if (k.secondaryAbility != null) k.secondaryAbility.remaining = 0f;
                }
                if (training.spawn.Contains(player.Feet)) player.ApplyHeal(null, 200f * dt);
            }

            for (int i = Combatant.All.Count - 1; i >= 0; i--)
            {
                var c = Combatant.All[i];
                if (c.alive || Time.time < c.respawnAt) continue;
                if (c == player)
                {
                    if (pendingHeroId != null)
                    {
                        string id = pendingHeroId;
                        pendingHeroId = null;
                        SwapPlayerHero(id, training.spawn.RandomPoint(), training.spawn.yaw);
                    }
                    else c.Respawn(training.spawn.RandomPoint(), training.spawn.yaw);
                    continue;
                }
                var tb = c.brain as TrainingBotBrain;
                if (tb != null) c.Respawn(tb.anchor, tb.homeYaw);
            }
        }

        public void ResetTrainingBots()
        {
            foreach (var c in Combatant.All)
            {
                var tb = c.brain as TrainingBotBrain;
                if (tb == null) continue;
                if (c.alive) c.Respawn(tb.anchor, tb.homeYaw);
                else c.respawnAt = Time.time;
            }
            totalTrainingDamage = 0f;
            recentPlayerDamage.Clear();
            Announce("RANGE RESET", Color.white, 1.5f);
        }

        Combatant SpawnHero(HeroDefinition def, Team team, bool isPlayer, string name, int slot, SpawnRoom room, TrainingSpot spot = null)
        {
            var go = new GameObject((isPlayer ? "Player_" : "Bot_") + def.name + "_" + team);
            go.layer = Layers.Characters;
            var cc = go.AddComponent<CharacterController>();
            cc.height = def.height;
            cc.radius = def.radius;
            cc.center = new Vector3(0, def.height / 2f, 0);
            cc.stepOffset = 0.45f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.05f;

            var c = go.AddComponent<Combatant>();
            c.Setup(def, team, isPlayer, name, slot);

            var eye = new GameObject("Eye").transform;
            eye.SetParent(go.transform, false);
            eye.localPosition = new Vector3(0, def.height - 0.2f, 0);
            c.eye = eye;
            var socket = new GameObject("WeaponSocket").transform;
            socket.SetParent(eye, false);
            socket.localPosition = isPlayer ? new Vector3(0.24f, -0.22f, 0.3f) : new Vector3(0.28f, -0.38f, 0.25f);
            c.weaponSocket = socket;

            c.motor = go.AddComponent<HeroMotor>();
            c.motor.Init(c);
            c.visuals = HeroModel.Build(c);
            c.kit = (HeroKit)go.AddComponent(def.kitType);
            c.kit.Init(c);

            if (isPlayer)
            {
                var pb = go.AddComponent<PlayerBrain>();
                pb.self = c;
                c.brain = pb;
                pb.Bind(cam);
            }
            else if (spot != null)
            {
                var tb = go.AddComponent<TrainingBotBrain>();
                tb.self = c;
                tb.mode = spot.mode;
                tb.anchor = spot.position;
                tb.pointB = spot.pointB;
                tb.homeYaw = spot.yaw;
                tb.speedScale = spot.speedScale;
                tb.jumpy = spot.jumpy;
                tb.aggroCenter = spot.aggroCenter;
                c.brain = tb;
                c.Respawn(spot.position, spot.yaw);
                c.deaths = 0;
                return c;
            }
            else
            {
                var bb = go.AddComponent<BotBrain>();
                bb.self = c;
                bb.Configure(difficulty);
                c.brain = bb;
            }
            Vector3 p = room.RandomPoint();
            c.Respawn(p, room.yaw + Random.Range(-10f, 10f));
            c.deaths = 0;
            return c;
        }

        public void RequestHeroChange(string heroId)
        {
            heroPickerOpen = false;
            if (player == null || heroId == player.def.id) return;
            lastHeroId = heroId;
            if (!player.alive)
            {
                pendingHeroId = heroId;
                Announce("HERO WILL CHANGE ON RESPAWN", Color.white, 2f);
                return;
            }
            SwapPlayerHero(heroId, player.Feet, player.yaw);
        }

        void SwapPlayerHero(string heroId, Vector3 pos, float yaw)
        {
            var old = player;
            var def = HeroRoster.Get(heroId);
            var room = ActiveSpawn(playerTeam);
            var fresh = SpawnHero(def, playerTeam, true, "YOU", old.slot, room);
            fresh.Respawn(pos, yaw);
            fresh.kills = old.kills;
            fresh.deaths = old.deaths;
            fresh.eliminations = old.eliminations;
            fresh.finalBlows = old.finalBlows;
            fresh.damageDone = old.damageDone;
            fresh.healingDone = old.healingDone;
            fresh.damageBlocked = old.damageBlocked;
            Combatant.All.Remove(old);
            Destroy(old.gameObject);
            player = fresh;
            Sfx.Play2D("ability", 0.6f);
        }

        public bool PlayerCanChangeHero
        {
            get
            {
                if (player == null || !InMatch) return false;
                if (!player.alive || Training) return true;
                return ActiveSpawn(playerTeam).Contains(player.Feet) || state == MatchState.Setup;
            }
        }

        // ------------------------------------------------------------------ per-frame match logic

        void TickMatch(float dt)
        {
            if (Training) { TickTraining(dt); return; }
            if (state == MatchState.Setup && Time.time >= setupEnd)
            {
                state = MatchState.Playing;
                Announce(playerTeam == Team.Attack ? "ATTACK!" : "DEFEND!", new Color(1f, 0.85f, 0.3f), 2f);
                Sfx.Play2D("announce", 1f, 1.2f);
            }

            if (GameInput.KeyDown(GKey.H) && PlayerCanChangeHero) heroPickerOpen = !heroPickerOpen;

            int result = payload.Tick(dt, state == MatchState.Setup);
            if (result >= 0) OnCheckpoint(result);
            if (result == -2) { EndMatch(Team.Attack); return; }

            if (state == MatchState.Playing)
            {
                timeLeft -= dt;
                if (timeLeft <= 0f)
                {
                    timeLeft = 0f;
                    if (payload.attackersOn > 0)
                    {
                        if (!overtime) { overtime = true; Announce("OVERTIME", new Color(1f, 0.55f, 0.2f), 2f); Sfx.Play2D("announce", 1f, 0.8f); }
                        overtimeGrace = 3f;
                    }
                    else
                    {
                        if (!overtime) { EndMatch(Team.Defend); return; }
                        overtimeGrace -= dt;
                        if (overtimeGrace <= 0f) { EndMatch(Team.Defend); return; }
                    }
                }
            }

            foreach (var c in Combatant.All)
            {
                if (c.alive)
                {
                    var room = ActiveSpawn(c.team);
                    if (room.Contains(c.Feet)) c.ApplyHeal(null, 200f * dt);
                }
            }

            // respawns
            for (int i = Combatant.All.Count - 1; i >= 0; i--)
            {
                var c = Combatant.All[i];
                if (c.alive || Time.time < c.respawnAt) continue;
                var room = ActiveSpawn(c.team);
                if (c == player && pendingHeroId != null)
                {
                    string id = pendingHeroId;
                    pendingHeroId = null;
                    SwapPlayerHero(id, room.RandomPoint(), room.yaw);
                    continue;
                }
                c.Respawn(room.RandomPoint(), room.yaw + Random.Range(-10f, 10f));
                var bb = c.brain as BotBrain;
                if (bb != null) bb.ResetBrain();
            }
        }

        void OnCheckpoint(int index)
        {
            timeLeft += CheckpointBonus;
            overtime = false;
            overtimeGrace = 3f;
            Announce("CHECKPOINT REACHED  +" + Mathf.RoundToInt(CheckpointBonus) + "s", new Color(1f, 0.85f, 0.3f), 3f);
            Sfx.Play2D("announce", 1f);
            if (index == 0 && payload.checkpoints.Length > 1)
                Debug.Log("[NexusStrike] Checkpoint " + (index + 1) + " reached, spawns advanced.");
        }

        void EndMatch(Team w)
        {
            winner = w;
            state = MatchState.Ended;
            endTime = Time.time;
            heroPickerOpen = false;
            bool won = w == playerTeam;
            Announce(won ? "VICTORY" : "DEFEAT", won ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.35f, 0.3f), 9999f);
            Sfx.Play2D(won ? "ult" : "death", 1f, won ? 1.2f : 0.6f);
        }

        public bool CanAct(Combatant c)
        {
            if (paused) return false;
            if (state == MatchState.Playing) return true;
            if (state == MatchState.Setup) return c.team == Team.Defend;
            return false;
        }

        // ------------------------------------------------------------------ events

        public void Announce(string text, Color color, float duration)
        {
            announcements.Add(new Announcement { text = text, color = color, start = Time.unscaledTime, until = Time.unscaledTime + duration });
        }

        public void OnDamage(Combatant attacker, Combatant victim, float amount, bool headshot)
        {
            if (amount <= 0f) return;
            if (attacker != null && attacker == player && victim != player)
            {
                if (Training)
                {
                    recentPlayerDamage.Add(new Vector2(Time.time, amount));
                    totalTrainingDamage += amount;
                    hud.OnDamageNumber(victim, amount, headshot);
                }
                hud.OnHit(headshot);
                Sfx.Play2D(headshot ? "headshot" : "hit", headshot ? 0.7f : 0.35f);
            }
            if (victim == player && attacker != null && attacker != player)
            {
                hud.OnPlayerDamaged(attacker.ChestPos);
                var pb = player.brain as PlayerBrain;
                if (pb != null) pb.Shake(Mathf.Clamp(amount / 40f, 0.2f, 1.2f));
            }
        }

        public void OnKill(Combatant killer, Combatant victim, string source, List<Combatant> assisters)
        {
            victim.respawnAt = Time.time + (Training ? (victim.isPlayer ? 3f : 2.5f) : RespawnTime);
            if (killer != null && killer != victim)
            {
                killer.kills++;
                killer.finalBlows++;
                killer.eliminations++;
            }
            foreach (var a in assisters) a.eliminations++;

            bool involves = victim == player || killer == player || assisters.Contains(player);
            feed.Add(new FeedEntry
            {
                killer = killer != null && killer != victim ? Label(killer) : "",
                victim = Label(victim),
                killerColor = killer != null ? TeamColors.Relative(killer.team) : Color.white,
                victimColor = TeamColors.Relative(victim.team),
                source = source,
                time = Time.time,
                involvesPlayer = involves
            });
            if (feed.Count > 7) feed.RemoveAt(0);

            if (killer == player && victim != player)
            {
                hud.OnKill(victim);
                Sfx.Play2D("kill", 0.8f);
            }
            else if (player != null && assisters.Contains(player))
            {
                hud.OnAssist(victim);
            }
            if (victim == player)
            {
                string by = killer != null && killer != victim ? killer.def.name + " (" + killer.displayName + ")" : "THE ENVIRONMENT";
                Announce("ELIMINATED BY " + by, new Color(1f, 0.35f, 0.3f), 3f);
            }
        }

        public void OnUltimate(Combatant c)
        {
            if (player == null) return;
            if (c.team == player.team && c != player) hud.OnUltCallout(c, true);
            else if (c.team != player.team && Vector3.Distance(c.Feet, player.Feet) < 40f) hud.OnUltCallout(c, false);
        }

        static string Label(Combatant c) { return c.def.name + "  " + c.displayName; }

        void OnDestroy()
        {
            if (I == this) I = null;
            Time.timeScale = 1f;
        }
    }
}
