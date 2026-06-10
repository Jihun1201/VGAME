using System;
using System.Collections.Generic;
using Raylib_cs;
using EntityGroup;
using CombatSystem;
using UpgradeLogic;
using WeaponData;
using FieldItem;
using SaveData;

namespace GameCore
{
    public struct Vector2 { public float X; public float Y; public Vector2(float x, float y) { X = x; Y = y; } public static float Distance(Vector2 a, Vector2 b) { float dx = a.X - b.X; float dy = a.Y - b.Y; return (float)Math.Sqrt(dx * dx + dy * dy); } }

    public enum GameState { Title, Shop, RecipeBook, Playing, LevelUp, ChestReward, Pause, GameOver, Victory }

    public class Engine
    {
        // Engine 클래스 내부
        private float _bgmVolume = 1.0f; // 0.0f ~ 1.0f
        private Music _bgm; 
        
        
        private int _titleMenuIdx = 0;
        private Player        _player;
        private List<Enemy>   _enemies;
        private Weapon        _weapon;
        private List<ExpGem>  _gems;
        private LevelSystem   _levelSystem;
        private CardDeck      _cardDeck;        

        private List<DropItem> _dropItems  = new List<DropItem>();
        private List<MapChest> _mapChests  = new List<MapChest>();
        
        private List<string> _chestRewards = new List<string>();
        private float _chestAnimTimer = 0f;
        private SaveFile _save;

        private int _shopCursor = 0;
        private int _recipePage = 0;

        private Random _rand = new Random();

        private GameState _currentState = GameState.Title;
        private float _spawnTimer  = 0f;
        private float _survivalTime= 0f;

        private bool _midBoss1Spawned = false; // 1:00
        private bool _midBoss2Spawned = false; // 2:00
        private bool _midBoss3Spawned = false; // 2:30
        private bool _midBoss4Spawned = false; // 3:00
        private bool _midBoss5Spawned = false; // 3:30
        private bool _midBoss6Spawned = false; // 4:00
        private bool _midBoss7Spawned = false; // 4:30

        private bool  _finalBossSpawned = false;
        private Enemy _finalBoss        = null;

        private List<BossZone>       _bossZones       = new List<BossZone>();
        private List<BossProjectile> _bossProjectiles = new List<BossProjectile>();

        private List<BossZone>  _floorHazards    = new List<BossZone>();
        private float           _floorHazardTimer= 0f;

        private List<DamageText> _damageTexts;
        private Camera2D _camera;
        private Texture2D _texIdle, _texTitleIdle, _texWalk, _texEnemy, _texFloor;
        private Texture2D[] _shopIcons = Array.Empty<Texture2D>();
        private Texture2D _texStaffIcon, _texHellFireIcon, _texShoesIcon, _texFireball;
        private Texture2D _texStaffBullet;
        private Texture2D _texGarlicIcon, _texOrbitalIcon, _texAxeIcon, _texShurikenIcon;
        private Texture2D _texMagicCircleIcon, _texBlackHoleIcon, _texAxeStormIcon, _texInfiniteShurikenIcon;
        private Texture2D _texArmorIcon, _texRingIcon, _texGloveIcon, _texNecklaceIcon;
        private Texture2D _texFireZone;   // 장판 불꽃 스프라이트시트 (fire.png, 8x8 grid)
        private Texture2D _texItems32;    // 32x32 아이템 스프라이트시트 (32x32.png)
        private float _fireAnimTimer = 0f;
        private int   _fireFrame     = 0;
        private Font _fontKR;  
        private List<Texture2D> _gemTextures = new List<Texture2D>();
        private string[] _shopIconFileNames =
        {
            "image/skill_icons10.png",
            "image/skill_icons30.png",
            "image/skill_icons41.png",
            "image/skill_icons31.png",
            "image/skill_icons48.png",
            "image/skill_icons16.png"
        };
        private string[] _gemFileNames =
        {
            "image/MonedaP.png","image/MonedaD.png","image/MonedaR.png",
            "image/spr_coin_gri.png","image/spr_coin_strip4.png",
            "image/spr_coin_azu.png","image/spr_coin_ama.png","image/spr_coin_roj.png"
        };

        public Engine()
        {
            _save = SaveFile.Load();

            _player      = new Player { Position = new Vector2(400, 300) };
            _enemies     = new List<Enemy>();
            _weapon      = new Weapon();
            _gems        = new List<ExpGem>();
            _levelSystem = new LevelSystem();
            _damageTexts = new List<DamageText>();

            _cardDeck = new CardDeck();
            _cardDeck.InitStartingWeapons(hasStaff: true, hasGarlic: false, hasOrbital: false);
            _weapon.HasGarlic = false;
            _weapon.HasOrbital = false;
            _weapon.ApplyLevel(WeaponType.Staff, 1);

            _camera = new Camera2D();
            _camera.Offset = new System.Numerics.Vector2(800f / 2f, 600f / 2f);
            _camera.Zoom   = 1.0f;
        }

        public void Run()
        {
            Raylib.InitWindow(800, 600, "Vampire Comanndo");
            Raylib.SetTargetFPS(60);

            Raylib.SetExitKey(KeyboardKey.Null);
            Raylib.InitAudioDevice();
            _bgm = Raylib.LoadMusicStream("sound.ogg");

            _texIdle      = Raylib.LoadTexture("image/idle.png");
            _texTitleIdle = Raylib.LoadTexture("image/ups_idle.png");
            _texWalk      = Raylib.LoadTexture("image/walk.png");
            _texEnemy     = Raylib.LoadTexture("image/Basic 1x.png");
            _texFloor     = Raylib.LoadTexture("image/floor.png");
            _texStaffIcon = Raylib.LoadTexture("image/icon_staff.png");
            _texHellFireIcon = Raylib.LoadTexture("image/icon_hellfire.png");
            _texShoesIcon = Raylib.LoadTexture("image/icon_shoes.png");
            _texFireball = Raylib.LoadTexture("image/projectile_fireball.png");
            _texStaffBullet = Raylib.LoadTexture("image/projectile_staff.png");
            _texGarlicIcon = Raylib.LoadTexture("image/icon_garlic.png");
            _texOrbitalIcon = Raylib.LoadTexture("image/icon_orbital.png");
            _texAxeIcon = Raylib.LoadTexture("image/icon_axe.png");
            _texShurikenIcon = Raylib.LoadTexture("image/icon_shuriken.png");
            _texMagicCircleIcon = Raylib.LoadTexture("image/icon_magic_circle.png");
            _texBlackHoleIcon = Raylib.LoadTexture("image/icon_blackhole.png");
            _texAxeStormIcon = Raylib.LoadTexture("image/icon_axe_storm.png");
            _texInfiniteShurikenIcon = Raylib.LoadTexture("image/icon_infinite_shuriken.png");
            _texArmorIcon = Raylib.LoadTexture("image/icon_armor.png");
            _texRingIcon = Raylib.LoadTexture("image/icon_ring.png");
            _texGloveIcon = Raylib.LoadTexture("image/icon_glove.png");
            _texNecklaceIcon = Raylib.LoadTexture("image/icon_necklace.png");
            _texFireZone  = Raylib.LoadTexture("image/fire.png");
            _texItems32   = Raylib.LoadTexture("image/32x32.png");
            Raylib.SetTextureFilter(_texFireZone,  TextureFilter.Bilinear);
            Raylib.SetTextureFilter(_texItems32,   TextureFilter.Point);
            _shopIcons    = new Texture2D[_shopIconFileNames.Length];
            for (int i = 0; i < _shopIconFileNames.Length; i++)
                _shopIcons[i] = Raylib.LoadTexture(_shopIconFileNames[i]);

            Raylib.SetTextureFilter(_texIdle,      TextureFilter.Point);
            Raylib.SetTextureFilter(_texWalk,      TextureFilter.Point);
            Raylib.SetTextureFilter(_texEnemy,     TextureFilter.Point);
            Raylib.SetTextureFilter(_texTitleIdle, TextureFilter.Bilinear);
            Raylib.SetTextureFilter(_texStaffIcon, TextureFilter.Point);
            Raylib.SetTextureFilter(_texHellFireIcon, TextureFilter.Point);
            Raylib.SetTextureFilter(_texShoesIcon, TextureFilter.Point);
            Raylib.SetTextureFilter(_texFireball, TextureFilter.Bilinear);
            Raylib.SetTextureFilter(_texStaffBullet, TextureFilter.Bilinear);
            Raylib.SetTextureFilter(_texGarlicIcon, TextureFilter.Point);
            Raylib.SetTextureFilter(_texOrbitalIcon, TextureFilter.Point);
            Raylib.SetTextureFilter(_texAxeIcon, TextureFilter.Point);
            Raylib.SetTextureFilter(_texShurikenIcon, TextureFilter.Point);
            Raylib.SetTextureFilter(_texMagicCircleIcon, TextureFilter.Bilinear);
            Raylib.SetTextureFilter(_texBlackHoleIcon, TextureFilter.Point);
            Raylib.SetTextureFilter(_texAxeStormIcon, TextureFilter.Point);
            Raylib.SetTextureFilter(_texInfiniteShurikenIcon, TextureFilter.Point);
            Raylib.SetTextureFilter(_texArmorIcon, TextureFilter.Point);
            Raylib.SetTextureFilter(_texRingIcon, TextureFilter.Point);
            Raylib.SetTextureFilter(_texGloveIcon, TextureFilter.Point);
            Raylib.SetTextureFilter(_texNecklaceIcon, TextureFilter.Point);
            foreach (var icon in _shopIcons) Raylib.SetTextureFilter(icon, TextureFilter.Point);

            unsafe { _fontKR = Raylib.LoadFontEx("fonts/NanumGothic.ttf", 32, null, 65535); }
            Raylib.SetTextureFilter(_fontKR.Texture, TextureFilter.Bilinear);

            foreach (var f in _gemFileNames) _gemTextures.Add(Raylib.LoadTexture(f));

            while (!Raylib.WindowShouldClose()) { Update(Raylib.GetFrameTime()); Render(); }
            Raylib.StopMusicStream(_bgm);
            Raylib.UnloadMusicStream(_bgm);
            Raylib.CloseAudioDevice();
            Raylib.UnloadTexture(_texIdle); Raylib.UnloadTexture(_texTitleIdle);
            Raylib.UnloadTexture(_texWalk); Raylib.UnloadTexture(_texEnemy);
            Raylib.UnloadTexture(_texFloor);
            Raylib.UnloadTexture(_texStaffIcon); Raylib.UnloadTexture(_texHellFireIcon);
            Raylib.UnloadTexture(_texShoesIcon); Raylib.UnloadTexture(_texFireball); Raylib.UnloadTexture(_texStaffBullet);
            Raylib.UnloadTexture(_texGarlicIcon); Raylib.UnloadTexture(_texOrbitalIcon);
            Raylib.UnloadTexture(_texAxeIcon); Raylib.UnloadTexture(_texShurikenIcon);
            Raylib.UnloadTexture(_texMagicCircleIcon); Raylib.UnloadTexture(_texBlackHoleIcon);
            Raylib.UnloadTexture(_texAxeStormIcon); Raylib.UnloadTexture(_texInfiniteShurikenIcon);
            Raylib.UnloadTexture(_texArmorIcon); Raylib.UnloadTexture(_texRingIcon);
            Raylib.UnloadTexture(_texGloveIcon); Raylib.UnloadTexture(_texNecklaceIcon);
            Raylib.UnloadTexture(_texFireZone);
            Raylib.UnloadTexture(_texItems32);
            foreach (var icon in _shopIcons) Raylib.UnloadTexture(icon);
            Raylib.UnloadFont(_fontKR);
            foreach (var t in _gemTextures) Raylib.UnloadTexture(t);
            Raylib.CloseWindow();
        }

        private void Update(float dt)
        {
            if (Raylib.IsMusicStreamPlaying(_bgm))
            {
                 Raylib.UpdateMusicStream(_bgm);
            }
            if (_currentState == GameState.Title)
            {
                if (Raylib.IsKeyPressed(KeyboardKey.Up))    _titleMenuIdx = (_titleMenuIdx + 2) % 3;
                if (Raylib.IsKeyPressed(KeyboardKey.Down))  _titleMenuIdx = (_titleMenuIdx + 1) % 3;
                if (Raylib.IsKeyPressed(KeyboardKey.Enter)) { ExecuteTitleMenu(_titleMenuIdx); return; }
                var mp = Raylib.GetMousePosition();
                for (int i = 0; i < 3; i++)
                {
                    int by2 = 395 + i * 60;
                    if (mp.X >= 200 && mp.X <= 600 && mp.Y >= by2 && mp.Y <= by2+46)
                    {
                        _titleMenuIdx = i;
                        if (Raylib.IsMouseButtonPressed(MouseButton.Left)) ExecuteTitleMenu(i);
                    }
                }
                return;
            }

            if (_currentState == GameState.Shop)
            {
                var mp = Raylib.GetMousePosition();
                var upgrades = MetaTable.All;
                if (Raylib.IsKeyPressed(KeyboardKey.Escape)) { _currentState = GameState.Title; return; }
                if (Raylib.IsKeyPressed(KeyboardKey.Up))   _shopCursor = (_shopCursor - 1 + upgrades.Count) % upgrades.Count;
                if (Raylib.IsKeyPressed(KeyboardKey.Down)) _shopCursor = (_shopCursor + 1) % upgrades.Count;
                if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Z))
                { _save.BuyUpgrade(upgrades[_shopCursor].Type); _save.Save(); }
                int rowH2 = 74, startY2 = 72;
                for (int i = 0; i < upgrades.Count; i++)
                {
                    int ry2 = startY2 + i * rowH2;
                    if (mp.X >= 30 && mp.X <= 770 && mp.Y >= ry2 && mp.Y <= ry2+rowH2-5)
                    {
                        _shopCursor = i;
                        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                        { _save.BuyUpgrade(upgrades[i].Type); _save.Save(); }
                    }
                }
                return;
            }

            if (_currentState == GameState.RecipeBook)
            {
                if (Raylib.IsKeyPressed(KeyboardKey.Escape)) { _currentState = GameState.Title; return; }
                if (Raylib.IsKeyPressed(KeyboardKey.Left) || Raylib.IsKeyPressed(KeyboardKey.Right))
                    _recipePage = 1 - _recipePage;
                var mp = Raylib.GetMousePosition();
                if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    if (mp.X >= 30  && mp.X <= 370 && mp.Y >= 8 && mp.Y <= 50) _recipePage = 0;
                    if (mp.X >= 430 && mp.X <= 770 && mp.Y >= 8 && mp.Y <= 50) _recipePage = 1;
                }
                return;
            }
            if (_currentState == GameState.GameOver || _currentState == GameState.Victory)
            {
                if (Raylib.IsKeyPressed(KeyboardKey.R))
                {
                    _save.EarnGold(_player.Gold);
                    _save.Save();
                    Raylib.StopMusicStream(_bgm);
                    _currentState = GameState.Title;
                }
                return;
            }
            if (_currentState == GameState.Pause)
            {
                if (Raylib.IsKeyPressed(KeyboardKey.Escape)) _currentState = GameState.Playing;
                if (Raylib.IsKeyPressed(KeyboardKey.Q)) Raylib.CloseWindow();

                // 볼륨 조절 슬라이더 로직
                var mp = Raylib.GetMousePosition();
                // 슬라이더 영역: X=500~700, Y=540
                if (Raylib.IsMouseButtonDown(MouseButton.Left) && mp.X >= 500 && mp.X <= 700 && mp.Y >= 530 && mp.Y <= 550)
                {
                    _bgmVolume = (mp.X - 500) / 200f;
                    _bgmVolume = Math.Clamp(_bgmVolume, 0f, 1f);
                    Raylib.SetMusicVolume(_bgm, _bgmVolume);
                }
                return;
            }

            if (_currentState == GameState.Playing && Raylib.IsKeyPressed(KeyboardKey.Escape))
            {
                _currentState = GameState.Pause;
                return;
            }

            if (_currentState == GameState.LevelUp)
            {
                int chosen = -1;
                if (Raylib.IsKeyPressed(KeyboardKey.One))   chosen = 0;
                else if (Raylib.IsKeyPressed(KeyboardKey.Two))  chosen = 1;
                else if (Raylib.IsKeyPressed(KeyboardKey.Three))chosen = 2;

                if (chosen >= 0)
                {
                    var card = _cardDeck.SelectCard(chosen, _levelSystem);
                    if (card != null)
                    {
                        if (card.IsBonus)
                        {
                            ApplyBonusCard(card);
                        }
                        else if (card.CardType == CardType.Weapon)
                            _weapon.ApplyLevel(card.WeaponType, card.NextLevel);
                        else
                            _weapon.ApplyAccessory(card.AccessoryType, card.NextLevel, _player);
                    }
                    ResumeGame();
                }
                return;
            }

            if (_currentState == GameState.ChestReward)
            {
                _chestAnimTimer += dt; 
                if (_chestAnimTimer > 2.0f && (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Space))) {
                    _currentState = GameState.Playing;
                }
                return;
            }

            _survivalTime += dt;

            // fire.png 애니메이션 타이머 (8x8 = 64프레임, ~0.07s마다 전진)
            _fireAnimTimer += dt;
            if (_fireAnimTimer >= 0.07f) { _fireAnimTimer = 0f; _fireFrame = (_fireFrame + 1) % 64; }

            _player.Update(dt);
            _camera.Target = new System.Numerics.Vector2(_player.Position.X, _player.Position.Y);

            if (Raylib.IsKeyPressed(KeyboardKey.C))
            {
                _mapChests.Add(new MapChest { Position = new Vector2(_player.Position.X + 40, _player.Position.Y) });
            }

            foreach (var e in _enemies)
            {
                if (e.IsDead) continue;
                if (Vector2.Distance(_player.Position, e.Position) < 25.0f)
                {
                    if (!_player.IsShielded)
                    { _player.CurrentHP -= e.Damage * dt; _player.HitTimer = 0.1f; }
                }
            }
            if (_player.IsDead)
            {
                if (_player.ReviveCount > 0)
                {
                    _player.ReviveCount--;
                    _player.CurrentHP = _player.MaxHP * 0.30f;
                    _player.ShieldTimer = 3f;
                    _damageTexts.Add(new DamageText { Position = _player.Position, Damage = -(_player.MaxHP * 0.30f) });
                }
                else
                {
                    _player.CurrentHP = 0;
                    _currentState = GameState.GameOver;
                    return;
                }
            }

            if (_survivalTime >= 300f && !_finalBossSpawned)
            {
                _finalBossSpawned = true;
                _enemies.RemoveAll(e => !e.IsBoss);
                _bossZones.Clear(); _floorHazards.Clear(); _bossProjectiles.Clear();
                _finalBoss = new Enemy
                {
                    Position = new Vector2(_player.Position.X + 500, _player.Position.Y),
                    Damage = 50f, Speed = 85f, Scale = 13f,
                    TintColor = new Color(255,40,40,255), IsBoss = true, IsFinalBoss = true,
                    PatternInterval = 3.0f, FinalBossShotInterval = 1.8f
                };
                _finalBoss.InitBoss(20000f, 3.0f);
                _enemies.Add(_finalBoss);
            }

            if (_finalBossSpawned && (_finalBoss == null || _finalBoss.IsDead))
            { _currentState = GameState.Victory; return; }

            if (!_finalBossSpawned)
            {
                float spawnDelay = Math.Max(0.08f, 0.4f - (_survivalTime / 300f) * 0.32f);
                _spawnTimer += dt;
                if (_spawnTimer >= spawnDelay)
                {
                    _spawnTimer = 0f;
                    float sx = _player.Position.X + (_rand.Next(0,2)==0 ? _rand.Next(-450,-400) : _rand.Next(400,450));
                    float sy = _player.Position.Y + (_rand.Next(0,2)==0 ? _rand.Next(-350,-300) : _rand.Next(300,450));
                    _enemies.Add(new Enemy { Position = new Vector2(sx, sy), HP = 10 + (_survivalTime/60f)*5f });
                }
            }

            int bossSign = (_rand.Next(0,2)==0) ? 1 : -1;
            Vector2 BossPos() => new Vector2(_player.Position.X + bossSign*_rand.Next(420,500), _player.Position.Y + _rand.Next(-80,80));
            Enemy MakeBoss(float hp, float dmg, float spd, float scale, Color col, float interval=5f)
            { var b=new Enemy{Position=BossPos(),Damage=dmg,Speed=spd,Scale=scale,TintColor=col,IsBoss=true,PatternInterval=interval}; b.InitBoss(hp,interval); return b; }

            if (_survivalTime>=60f  &&!_midBoss1Spawned){_midBoss1Spawned=true;_enemies.Add(MakeBoss( 900, 28,115,5.5f,Color.Purple,               6f));}
            if (_survivalTime>=120f &&!_midBoss2Spawned){_midBoss2Spawned=true;_enemies.Add(MakeBoss( 1500, 33,125,6.0f,Color.DarkPurple,            5.5f));}
            if (_survivalTime>=150f &&!_midBoss3Spawned){_midBoss3Spawned=true;_enemies.Add(MakeBoss( 2300, 37,130,6.5f,new Color(255,100,  0,255),  5f));}
            if (_survivalTime>=180f &&!_midBoss4Spawned){_midBoss4Spawned=true;_enemies.Add(MakeBoss( 3300, 41,135,7.0f,new Color(200,  0,200,255),  4.5f));}
            if (_survivalTime>=210f &&!_midBoss5Spawned){_midBoss5Spawned=true;_enemies.Add(MakeBoss( 4500, 45,140,7.5f,new Color(255, 50, 50,255),  4f));}
            if (_survivalTime>=240f &&!_midBoss6Spawned){_midBoss6Spawned=true;_enemies.Add(MakeBoss(6300, 50,148,8.5f,new Color( 50, 50,255,255),  3.5f));}
            if (_survivalTime>=270f &&!_midBoss7Spawned){_midBoss7Spawned=true;_enemies.Add(MakeBoss(9000, 55,155,9.5f,new Color(255,215,  0,255),  3f));}

            foreach (var e in _enemies) e.Update(dt, _player.Position);

            foreach (var e in _enemies)
            {
                if (!e.IsBoss || e.IsDead) continue;
                if (e.SpawnZoneRequest)
                {
                    e.SpawnZoneRequest = false;
                    int zc = _rand.Next(2, 4);
                    for (int z = 0; z < zc; z++)
                        _bossZones.Add(new BossZone {
                            Position = new Vector2(_player.Position.X+_rand.Next(-280,280), _player.Position.Y+_rand.Next(-220,220)),
                            Radius = _rand.Next(50,85), Damage = e.Damage * 0.7f });
                }
                if (e.IsFinalBoss && e.FinalBossShotRequest)
                {
                    e.FinalBossShotRequest = false;
                    float bx=e.Position.X, by2=e.Position.Y;
                    float dx=_player.Position.X-bx, dy=_player.Position.Y-by2;
                    float dist=(float)Math.Sqrt(dx*dx+dy*dy);
                    if (dist > 0) {
                        float[] angs = {-0.3f,0f,0.3f,-0.6f,0.6f};
                        foreach (float ang in angs) {
                            float c=(float)Math.Cos(ang),s=(float)Math.Sin(ang);
                            float ndx=dx/dist*c-dy/dist*s, ndy=dx/dist*s+dy/dist*c;
                            _bossProjectiles.Add(new BossProjectile { Position=new Vector2(bx,by2), Velocity=new Vector2(ndx*150f,ndy*150f), Damage=e.Damage*0.7f, Radius=11f });
                        }
                    }
                }
            }


            foreach (var z in _bossZones) {
                z.Timer += dt; if (z.HitTimer>0) z.HitTimer -= dt;
                if (z.IsActive && z.HitTimer<=0 && Vector2.Distance(_player.Position,z.Position)<z.Radius)
                { if (!_player.IsShielded) { _player.CurrentHP-=z.Damage; _player.HitTimer=0.2f; } z.HitTimer=0.5f; }
            }
            _bossZones.RemoveAll(z => z.IsDone);


            bool anyBossAlive = _enemies.Exists(e => e.IsBoss && !e.IsDead);
            if (anyBossAlive) {
                _floorHazardTimer += dt;
                if (_floorHazardTimer >= 4f) {
                    _floorHazardTimer = 0f;
                    int fc = 2 + (int)(_survivalTime / 60f);
                    for (int i = 0; i < fc; i++)
                        _floorHazards.Add(new BossZone {
                            Position = new Vector2(_player.Position.X+_rand.Next(-380,380), _player.Position.Y+_rand.Next(-280,280)),
                            Radius = _rand.Next(35,65), Damage = 12f + _survivalTime/30f,
                            WarnTime = 1.5f, ActiveTime = 2.5f });
                }
            } else {
                _floorHazardTimer = 0f;
            }
            foreach (var z in _floorHazards) {
                z.Timer += dt; if (z.HitTimer>0) z.HitTimer -= dt;
                if (z.IsActive && z.HitTimer<=0 && Vector2.Distance(_player.Position,z.Position)<z.Radius)
                { if (!_player.IsShielded) { _player.CurrentHP-=z.Damage*dt*3f; _player.HitTimer=0.1f; } z.HitTimer=0.3f; }
            }
            _floorHazards.RemoveAll(z => z.IsDone);


            foreach (var bp in _bossProjectiles) {
                bp.Position.X+=bp.Velocity.X*dt; bp.Position.Y+=bp.Velocity.Y*dt;
                bp.Timer+=dt; if (bp.Timer>=bp.Lifetime) { bp.IsActive=false; continue; }
                if (bp.HitTimer>0) { bp.HitTimer-=dt; continue; }
                if (Vector2.Distance(_player.Position,bp.Position)<bp.Radius+15f)
                { if (!_player.IsShielded) { _player.CurrentHP-=bp.Damage; _player.HitTimer=0.15f; } bp.HitTimer=0.8f; }
            }
            _bossProjectiles.RemoveAll(bp => !bp.IsActive);
            _weapon.Update(dt, _player, _enemies, _damageTexts);
            foreach (var t in _damageTexts) t.Update(dt);
            _damageTexts.RemoveAll(t => t.Timer >= t.Lifetime);

            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                if (_enemies[i].IsDead)
                {

                    if (_enemies[i].IsBoss)
                    { 
                        _mapChests.Add(new MapChest { Position = _enemies[i].Position }); 
                    }
                    else
                    {
                        int dropIndex = 3;
                        if (_rand.Next(0,100)<10) { int r=_rand.Next(0,100); dropIndex = r<70?0:r<95?1:2; }
                        else { int r=_rand.Next(0,100); dropIndex = r<60?3:r<85?4:r<97?5:6; }
                        _gems.Add(new ExpGem { Position = _enemies[i].Position, GemTypeIndex = dropIndex });

                        int itemRoll = _rand.Next(0, 1000); 
                        DropItemType? dropType = null;
                        if (itemRoll < 5)   dropType = DropItemType.Food;    
                        else if (itemRoll < 8)   dropType = DropItemType.Magnet;  
                        else if (itemRoll < 10)  dropType = DropItemType.Shield;  

                        if (dropType.HasValue)
                            _dropItems.Add(new DropItem { Position = _enemies[i].Position, Type = dropType.Value });
                    }
                    _enemies.RemoveAt(i);
                }
            }

            if (_player.MagnetActive)
            {
                foreach (var gem in _gems) gem.IsMagnetized = true;
                _player.MagnetActive = false; 
            }

            foreach (var gem in _gems)
            {
                gem.Update(dt, _player.Position); 
                float pickupRange = gem.IsCoin ? 30f : 50f;
                
                if (Vector2.Distance(_player.Position, gem.Position) < pickupRange)
                {
                    gem.IsCollected = true;
                    if (gem.IsCoin) _player.Gold += gem.GetValue();
                    else            _levelSystem.AddExp(gem.GetValue());
                }
            }
            _gems.RemoveAll(g => g.IsCollected);



            foreach (var chest in _mapChests)
            {
                chest.Update(dt);
                if (chest.State == ChestState.Closed &&
                    Vector2.Distance(_player.Position, chest.Position) < chest.TriggerRadius)
                {
                    chest.Open();
                    _chestRewards = _cardDeck.OpenChest(_weapon, _player);
                    _currentState = GameState.ChestReward; 
                    _chestAnimTimer = 0f;
                }
            }
            _mapChests.RemoveAll(c => c.IsDone);

            foreach (var item in _dropItems)
            {
                item.Update(dt);
                if (Vector2.Distance(_player.Position, item.Position) < item.PickupRadius)
                {
                    item.IsCollected = true;
                    switch (item.Type)
                    {
                        case DropItemType.Food:
                            _player.HealHP(30f);
                            _damageTexts.Add(new DamageText { Position = _player.Position, Damage = -30f });
                            break;
                        case DropItemType.Magnet:
                            _player.MagnetActive = true;
                            break;
                        case DropItemType.Shield:
                            _player.ShieldTimer = 3f;
                            break;
                    }
                }
            }
            _dropItems.RemoveAll(i => i.IsCollected);

            if (_levelSystem.IsLevelUpReady)
            {
                _cardDeck.DrawCards();
                _currentState = GameState.LevelUp;
            }
        }

        private void ExecuteTitleMenu(int idx)
        {
            if (idx == 0) StartGame();
            else if (idx == 1) { _shopCursor = 0; _currentState = GameState.Shop; }
            else if (idx == 2) { _recipePage = 0; _currentState = GameState.RecipeBook; }
        }

        private void ResumeGame() { _levelSystem.IsLevelUpReady = false; _currentState = GameState.Playing; }


        private void ApplyBonusCard(UpgradeCard card)
        {
            switch (card.BonusType)
            {
                case BonusCardType.HealSmall:
                    _player.HealHP(_player.MaxHP * 0.25f);
                    _damageTexts.Add(new DamageText { Position = _player.Position, Damage = -(_player.MaxHP * 0.25f) });
                    break;
                case BonusCardType.HealLarge:
                    _player.HealHP(_player.MaxHP * 0.60f);
                    _damageTexts.Add(new DamageText { Position = _player.Position, Damage = -(_player.MaxHP * 0.60f) });
                    break;
                case BonusCardType.GoldSmall:
                    _player.Gold += 80;
                    break;
                case BonusCardType.GoldLarge:
                    _player.Gold += 200;
                    break;
                case BonusCardType.Shield:
                    _player.ShieldTimer = 5f;
                    break;
            }
        }


        private void StartGame()
        {
            _player      = new Player { Position = new Vector2(400, 300) };
            _enemies     = new List<Enemy>();
            _weapon      = new Weapon();
            _gems        = new List<ExpGem>();
            _levelSystem = new LevelSystem();
            _damageTexts = new List<DamageText>();
            _dropItems   = new List<DropItem>();
            _mapChests   = new List<MapChest>();
            _chestRewards= new List<string>();

            _cardDeck = new CardDeck();
            _cardDeck.InitStartingWeapons(hasStaff: true, hasGarlic: false, hasOrbital: false);
            _weapon.ApplyLevel(WeaponType.Staff, 1);

            // 메타 업그레이드 초기값 적용
            var def_hp    = MetaTable.Get(MetaUpgradeType.MaxHP);
            var def_spd   = MetaTable.Get(MetaUpgradeType.MoveSpeed);
            var def_dmg   = MetaTable.Get(MetaUpgradeType.StartDamage);
            var def_gold  = MetaTable.Get(MetaUpgradeType.StartGold);
            var def_exp   = MetaTable.Get(MetaUpgradeType.ExpBonus);
            var def_rev   = MetaTable.Get(MetaUpgradeType.Revive);

            int lvHP   = _save.GetMetaLevel(MetaUpgradeType.MaxHP);
            int lvSpd  = _save.GetMetaLevel(MetaUpgradeType.MoveSpeed);
            int lvDmg  = _save.GetMetaLevel(MetaUpgradeType.StartDamage);
            int lvGold = _save.GetMetaLevel(MetaUpgradeType.StartGold);
            int lvExp  = _save.GetMetaLevel(MetaUpgradeType.ExpBonus);
            int lvRev  = _save.GetMetaLevel(MetaUpgradeType.Revive);

            float bonusHP   = def_hp.TotalValue(lvHP);
            float bonusSpd  = def_spd.TotalValue(lvSpd);
            float bonusDmg  = def_dmg.TotalValue(lvDmg);   
            float bonusGold = def_gold.TotalValue(lvGold);
            _levelSystem.ExpMult = 1f + def_exp.TotalValue(lvExp);
            _player.ReviveCount  = (int)def_rev.TotalValue(lvRev);

            _player.MaxHP    += bonusHP;
            _player.CurrentHP = _player.MaxHP;
            _player.Speed    += bonusSpd;
            _weapon.AccDamageMult += bonusDmg;
            _player.Gold      = (int)bonusGold;

            _spawnTimer       = 0f;
            _survivalTime     = 120f;
            _chestAnimTimer   = 0f;

            _midBoss1Spawned = true; _midBoss2Spawned = true; _midBoss3Spawned = false;
            _midBoss4Spawned = false; _midBoss5Spawned = false; _midBoss6Spawned = false;
            _midBoss7Spawned = false;
            _finalBossSpawned = false; _finalBoss = null;
            _bossZones?.Clear(); _bossProjectiles?.Clear(); _floorHazards?.Clear();
            Raylib.PlayMusicStream(_bgm);

            _levelSystem.AddExp(500);
            _currentState = GameState.Playing;
        }

        private void Render()
        {

            if (_currentState == GameState.Shop)       { RenderShop();       return; }
            if (_currentState == GameState.RecipeBook) { RenderRecipeBook(); return; }

            Raylib.BeginDrawing();

            if (_currentState == GameState.Title)
            {
                double gt = Raylib.GetTime();

                for (int row = 0; row < 600; row++)
                {
                    float rf = row / 600f;
                    byte r = (byte)(5  + (int)(10  * rf));
                    byte g = (byte)(5  + (int)(8   * rf));
                    byte b = (byte)(18 + (int)(15  * rf));
                    Raylib.DrawLine(0, row, 800, row, new Color(r, g, b, (byte)255));
                }

                for (int s = 0; s < 90; s++)
                {
                    int   sx = (s * 131 + 53) % 800;
                    int   sy = (s * 197 + 29) % 540;
                    float tw = (float)(0.3 + 0.7 * Math.Abs(Math.Sin(gt * (0.5 + s * 0.04) + s)));
                    byte  sc = (byte)(int)(220 * tw);
                    Raylib.DrawCircle(sx, sy, (s % 4 == 0) ? 2 : 1, new Color(sc, sc, sc, sc));
                }

                if (_texTitleIdle.Width > 0)
                {
                    int   fr  = (int)(gt * 10) % 10;
                    float fw  = _texTitleIdle.Width / 10f, fh = _texTitleIdle.Height;
                    var   src = new Rectangle(fr * fw, 0, fw, fh);
                    float sc  = 2.0f;
                    float fy  = (float)Math.Sin(gt * 1.8) * 7f;
                    var   org = new System.Numerics.Vector2(fw * sc / 2, fh * sc / 2);
                    Raylib.DrawTexturePro(_texTitleIdle, src,
                        new Rectangle(408, 290 + fy, fw*sc, fh*sc), org, 0f, new Color(0,0,0,80));
                    Raylib.DrawTexturePro(_texTitleIdle, src,
                        new Rectangle(400, 282 + fy, fw*sc, fh*sc), org, 0f, Color.White);
                }

                for (int g = 5; g >= 1; g--)
                    Raylib.DrawText("Vampire Comanndo", 130 - g, 45 - g, 64,
                        new Color((byte)255,(byte)180,(byte)0,(byte)(18 * g)));
                Raylib.DrawText("Vampire Comanndo", 130, 45, 64, Color.Gold);

                float sp = (float)(0.6 + 0.4 * Math.Sin(gt * 2.2));
                DrawTextKR("5분을 버텨라", 328, 118, 18,
                    new Color((byte)160,(byte)160,(byte)210,(byte)(int)(240*sp)));


                (string icon, string label, string hint)[] menus = {
                    ("▶", "게임 시작", "ENTER"),
                    ("★", "상점", $"영구 골드  {_save.PermanentGold} G"),
                    ("?", "조합표", "진화 예시"),
                };
                Color[] menuAccent = {
                    new Color(80,140,255,255),
                    new Color(255,200,50,255),
                    new Color(120,220,140,255),
                };
                for (int i = 0; i < menus.Length; i++)
                {
                    int   by2 = 395 + i * 60;
                    bool  sel = (i == _titleMenuIdx);
                    Color bg  = sel ? new Color(30,40,80,220) : new Color(12,12,28,180);
                    Color bd  = sel ? menuAccent[i] : new Color(40,40,65,200);
                    Raylib.DrawRectangle(200, by2, 400, 46, bg);
                    Raylib.DrawRectangleLines(200, by2, 400, 46, bd);
                    if (sel) Raylib.DrawRectangle(200, by2, 4, 46, menuAccent[i]);
                    Color tc = sel ? Color.White : new Color((byte)140,(byte)140,(byte)180,(byte)255);
                    DrawTextKR($"{menus[i].icon}  {menus[i].label}", 222, by2 + 13, 19, tc);
                    DrawTextKR(menus[i].hint, 525, by2 + 15, 13,
                        new Color((byte)100,(byte)110,(byte)150,(byte)255));
                }
                DrawTextKR("↑ ↓  이동    ENTER  선택", 290, 582, 15, new Color(60,60,90,255));
                Raylib.EndDrawing(); return;
            }

            Raylib.ClearBackground(Color.Black);
            Raylib.BeginMode2D(_camera);

            if (_texFloor.Width>0 && _texFloor.Height>0)
            {
                int tw=(int)_texFloor.Width, th=(int)_texFloor.Height;
                float sx=(float)Math.Floor((_camera.Target.X-400)/tw)*tw;
                float sy=(float)Math.Floor((_camera.Target.Y-300)/th)*th;
                for (float x=sx; x<_camera.Target.X+400+tw; x+=tw)
                for (float y=sy; y<_camera.Target.Y+300+th; y+=th)
                    Raylib.DrawTexture(_texFloor,(int)x,(int)y,Color.White);
            }

            foreach (var chest in _mapChests) chest.Draw();
            foreach (var item in _dropItems) item.Draw();

            foreach (var gem in _gems)
            {
                Texture2D gt = _gemTextures[gem.GemTypeIndex];
                if (gt.Width>0)
                {
                    float fw=(float)gt.Width/gem.MaxFrames;
                    var src = new Rectangle(gem.CurrentFrame*fw,0,fw,gt.Height);
                    var dst = new Rectangle(gem.Position.X,gem.Position.Y,fw,gt.Height);
                    var org = new System.Numerics.Vector2(fw/2,gt.Height/2);
                    Raylib.DrawTexturePro(gt,src,dst,org,0f,Color.White);
                }
                else Raylib.DrawCircle((int)gem.Position.X,(int)gem.Position.Y,5,Color.SkyBlue);
            }



            if (_weapon.HasGarlic)
            {
                int gr = (int)_weapon.GarlicRadius;
                Raylib.DrawCircle((int)_player.Position.X,(int)_player.Position.Y, gr, new Color(80,190,80,28));
                Raylib.DrawCircleLines((int)_player.Position.X,(int)_player.Position.Y, gr, new Color(100,230,100,90));
                Raylib.DrawCircle((int)_player.Position.X,(int)_player.Position.Y, gr/2, new Color(60,255,120,18));

                Raylib.DrawCircleLines((int)_player.Position.X,(int)_player.Position.Y, gr+2, new Color(80,200,80,50));
            }
            if (_weapon.HasMagicCircle)
            {

                int mr = (int)(120f * _weapon.AccAreaMult);
                double gt2 = Raylib.GetTime();
                if (_texMagicCircleIcon.Id != 0)
                {
                    float size = mr * 2f;
                    DrawCenteredItemIcon(_texMagicCircleIcon, _player.Position.X, _player.Position.Y, size, (float)(gt2 * 18.0), new Color(255,255,255,155));
                }
                else
                {
                    Raylib.DrawCircle((int)_player.Position.X,(int)_player.Position.Y, mr, new Color(120,40,220,35));
                    Raylib.DrawCircleLines((int)_player.Position.X,(int)_player.Position.Y, mr, new Color(180,100,255,180));
                }
            }
            if (_weapon.HasHellFire)
            {


                double gt3 = Raylib.GetTime();
                float hfPulse = (float)(0.7 + 0.3 * Math.Sin(gt3 * 6));
                Raylib.DrawCircle((int)_player.Position.X,(int)_player.Position.Y, 18, new Color((byte)255,(byte)80,(byte)0,(byte)(int)(30*hfPulse)));
                Raylib.DrawCircleLines((int)_player.Position.X,(int)_player.Position.Y, 20, new Color((byte)255,(byte)120,(byte)40,(byte)(int)(120*hfPulse)));
            }
            if (_weapon.HasOrbital)
            {
                int cnt = _weapon.OrbitalCount + _weapon.AccProjectileBonus;
                for (int i = 0; i < cnt; i++)
                {
                    float ang = _weapon.OrbitalAngle + i * ((float)Math.PI*2 / cnt);
                    float ox=(float)(_player.Position.X+Math.Cos(ang)*(_weapon.OrbitalRadius*_weapon.AccAreaMult));
                    float oy=(float)(_player.Position.Y+Math.Sin(ang)*(_weapon.OrbitalRadius*_weapon.AccAreaMult));
                    if (_texOrbitalIcon.Id != 0)
                        DrawCenteredItemIcon(_texOrbitalIcon, ox, oy, 24, (float)(Raylib.GetTime() * 120.0), Color.White);
                    else
                        Raylib.DrawCircle((int)ox,(int)oy,10,new Color(40,200,255,220));
                }
            }
            if (_weapon.HasBlackHole)
            {
                float rad=120f*_weapon.AccAreaMult; int obc=12+_weapon.AccProjectileBonus*2;
                Raylib.DrawCircle((int)_player.Position.X,(int)_player.Position.Y,(int)rad,new Color(20,0,60,35));
                for (int i=0;i<obc;i++) {
                    float ang=_weapon.BlackHoleAngle+i*((float)Math.PI*2/obc);
                    float bx=(float)(_player.Position.X+Math.Cos(ang)*rad);
                    float by=(float)(_player.Position.Y+Math.Sin(ang)*rad);
                    if (_texBlackHoleIcon.Id != 0)
                        DrawCenteredItemIcon(_texBlackHoleIcon, bx, by, 22, (float)(Raylib.GetTime() * 180.0), Color.White);
                    else
                        Raylib.DrawCircle((int)bx,(int)by,9,new Color(160,40,255,230));
                }
            }

            // 보스 장판 렌더링 (경고→활성, 활성시 fire.png 스프라이트 사용)
            foreach (var z in _bossZones)
            {
                int zx = (int)z.Position.X, zy = (int)z.Position.Y, zr = (int)z.Radius;
                if (z.IsWarning)
                {
                    float warnFade = z.Timer / z.WarnTime;
                    byte wa = (byte)(int)(140 + 115 * Math.Abs(Math.Sin(z.Timer * 8)));
                    Raylib.DrawCircle(zx, zy, zr, new Color((byte)220, (byte)20, (byte)20, (byte)50));
                    Raylib.DrawCircleLines(zx, zy, zr, new Color((byte)255, (byte)60, (byte)60, wa));
                    Raylib.DrawCircleLines(zx, zy, zr - 3, new Color((byte)255, (byte)180, (byte)50, (byte)(wa / 2)));
                }
                else if (z.IsActive)
                {
                    Raylib.DrawCircle(zx, zy, zr, new Color((byte)200, (byte)0, (byte)0, (byte)80));
                    Raylib.DrawCircleLines(zx, zy, zr, new Color((byte)255, (byte)30, (byte)30, (byte)220));
                    if (_texFireZone.Id != 0)
                    {
                        float fw = _texFireZone.Width  / 8f;
                        float fh = _texFireZone.Height / 8f;
                        int col = _fireFrame % 8, frow = _fireFrame / 8;
                        var fireSrc = new Rectangle(col * fw, frow * fh, fw, fh);
                        float fireSize = zr * 2.0f;
                        var fireDst = new Rectangle(zx, zy, fireSize, fireSize);
                        var fireOrg = new System.Numerics.Vector2(fireSize / 2f, fireSize / 2f);
                        Raylib.DrawTexturePro(_texFireZone, fireSrc, fireDst, fireOrg, 0f,
                            new Color((byte)255, (byte)255, (byte)255, (byte)210));
                    }
                    else
                    {
                        Raylib.DrawCircleLines(zx, zy, zr - 2, new Color((byte)255, (byte)120, (byte)50, (byte)160));
                    }
                }
            }


            // 중간보스/일반 바닥 장판 렌더링 (활성시 fire.png 스프라이트 사용)
            foreach (var z in _floorHazards)
            {
                int zx = (int)z.Position.X, zy = (int)z.Position.Y, zr = (int)z.Radius;
                if (z.IsWarning)
                {
                    byte wa2 = (byte)(int)(120 + 100 * Math.Abs(Math.Sin(z.Timer * 7)));
                    Raylib.DrawCircle(zx, zy, zr, new Color((byte)180, (byte)10, (byte)10, (byte)35));
                    Raylib.DrawCircleLines(zx, zy, zr, new Color((byte)255, (byte)50, (byte)50, wa2));
                }
                else if (z.IsActive)
                {
                    Raylib.DrawCircle(zx, zy, zr, new Color((byte)180, (byte)0, (byte)0, (byte)60));
                    Raylib.DrawCircleLines(zx, zy, zr, new Color((byte)255, (byte)20, (byte)20, (byte)200));
                    if (_texFireZone.Id != 0)
                    {
                        float fw = _texFireZone.Width  / 8f;
                        float fh = _texFireZone.Height / 8f;
                        int col = _fireFrame % 8, frow = _fireFrame / 8;
                        var fireSrc = new Rectangle(col * fw, frow * fh, fw, fh);
                        float fireSize = zr * 2.0f;
                        var fireDst = new Rectangle(zx, zy, fireSize, fireSize);
                        var fireOrg = new System.Numerics.Vector2(fireSize / 2f, fireSize / 2f);
                        Raylib.DrawTexturePro(_texFireZone, fireSrc, fireDst, fireOrg, 0f,
                            new Color((byte)255, (byte)255, (byte)255, (byte)180));
                    }
                }
            }


            foreach (var bp in _bossProjectiles)
            {
                int bpx = (int)bp.Position.X, bpy = (int)bp.Position.Y;

                Raylib.DrawCircle(bpx, bpy, (int)(bp.Radius + 6), new Color((byte)200, (byte)0, (byte)0, (byte)60));

                Raylib.DrawCircle(bpx, bpy, (int)bp.Radius, new Color((byte)255, (byte)30, (byte)30, (byte)230));
                Raylib.DrawCircleLines(bpx, bpy, (int)bp.Radius + 1, new Color((byte)255, (byte)150, (byte)80, (byte)200));

                Raylib.DrawCircle(bpx, bpy, 4, new Color((byte)255, (byte)200, (byte)180, (byte)255));
            }


            foreach (var e in _enemies)
            {
                if (e.IsShowingDashWarn)
                {
                    float dashT = 1f - (e.DashWarnRemain / 1.0f);
                    byte da = (byte)(int)(100 + 155 * Math.Abs(Math.Sin(Raylib.GetTime() * 12)));

                    Raylib.DrawLineEx(
                        new System.Numerics.Vector2(e.DashWarnStart.X, e.DashWarnStart.Y),
                        new System.Numerics.Vector2(e.DashWarnEnd.X, e.DashWarnEnd.Y),
                        16f, new Color((byte)255, (byte)0, (byte)0, (byte)80));
                    Raylib.DrawLineEx(
                        new System.Numerics.Vector2(e.DashWarnStart.X, e.DashWarnStart.Y),
                        new System.Numerics.Vector2(e.DashWarnEnd.X, e.DashWarnEnd.Y),
                        4f, new Color((byte)255, (byte)60, (byte)60, da));
                    // ?앹젏 ??
                    Raylib.DrawCircle((int)e.DashWarnEnd.X, (int)e.DashWarnEnd.Y, 14,
                        new Color((byte)255, (byte)0, (byte)0, (byte)(int)(60 + 60 * Math.Sin(Raylib.GetTime() * 10))));
                    Raylib.DrawCircleLines((int)e.DashWarnEnd.X, (int)e.DashWarnEnd.Y, 14,
                        new Color((byte)255, (byte)80, (byte)80, da));
                }
            }

            // 투사체 렌더링
            foreach (var p in _weapon.Projectiles)
            {
                Texture2D projectileTex = GetProjectileTexture(p);
                if (projectileTex.Id != 0)
                {
                    float size = GetProjectileSpriteSize(p);
                    float angle = (float)(Math.Atan2(p.Velocity.Y, p.Velocity.X) * 180.0 / Math.PI);
                    if (p.Sprite == ProjectileSprite.Axe || p.Sprite == ProjectileSprite.AxeStorm ||
                        p.Sprite == ProjectileSprite.Shuriken || p.Sprite == ProjectileSprite.InfiniteShuriken)
                        angle += (float)(Raylib.GetTime() * 720.0);
                    DrawCenteredItemIcon(projectileTex, p.Position.X, p.Position.Y, size, angle, Color.White);
                }
                else if (p.IsPiercing)
                {

                    Raylib.DrawCircle((int)p.Position.X,(int)p.Position.Y,8,new Color(255,140,30,230));
                    Raylib.DrawCircleLines((int)p.Position.X,(int)p.Position.Y,10,new Color(255,200,80,120));
                }
                else
                {
                    Raylib.DrawCircle((int)p.Position.X,(int)p.Position.Y,6,new Color(255,240,80,240));
                    Raylib.DrawCircleLines((int)p.Position.X,(int)p.Position.Y,8,new Color(255,255,200,100));
                }
            }


            foreach (var e in _enemies)
            {
                float spriteHalfH = 0f;

                bool twinkle = e.HitTimer > 0;
                bool showWhite = twinkle && ((int)(e.HitTimer * 30) % 2 == 0);
                if (_texEnemy.Width > 0)
                {
                    float fw = (float)_texEnemy.Width/5, fh = (float)_texEnemy.Height/3;
                    var src = new Rectangle(0,0,fw,fh);
                    var dst = new Rectangle(e.Position.X,e.Position.Y,fw*e.Scale,fh*e.Scale);
                    var org = new System.Numerics.Vector2(fw*e.Scale/2, fh*e.Scale/2);
                    Color col = showWhite ? Color.White : e.TintColor;
                    Raylib.DrawTexturePro(_texEnemy,src,dst,org,0f,col);

                    if (showWhite)
                        Raylib.DrawCircle((int)e.Position.X,(int)e.Position.Y,(int)(fw*e.Scale/2)*2/3,new Color(255,255,255,80));
                    spriteHalfH = fh * e.Scale / 2f;
                }
                else
                {
                    int er = e.IsBoss ? (int)(12*e.Scale/3f) : 10;
                    Color ecol = showWhite ? Color.White : e.TintColor;
                    Raylib.DrawCircle((int)e.Position.X,(int)e.Position.Y, er, ecol);
                    if (showWhite) Raylib.DrawCircle((int)e.Position.X,(int)e.Position.Y, er+3, new Color(255,255,255,80));
                    spriteHalfH = er;
                }


                if (e.IsBoss && e.MaxHP > 0)
                {
                    float hpRatio = Math.Max(0f, e.HP / e.MaxHP);
                    int   barW    = e.IsFinalBoss ? 120 : 70;
                    int   barH    = e.IsFinalBoss ? 10  : 7;
                    int   bx2     = (int)e.Position.X - barW/2;
                    int   barTop  = (int)(e.Position.Y - spriteHalfH - (e.IsFinalBoss ? 24 : 16));

                    Raylib.DrawRectangle(bx2-1, barTop-1, barW+2, barH+2, new Color(0,0,0,180));
                    Raylib.DrawRectangle(bx2, barTop, barW, barH, new Color(50,0,0,200));
                    Color hpCol2 = hpRatio > 0.5f ? new Color(220,40,40,255)
                                 : hpRatio > 0.25f ? new Color(255,130,0,255)
                                 : new Color(255,255,60,255);
                    Raylib.DrawRectangle(bx2, barTop, (int)(barW*hpRatio), barH, hpCol2);
                    Raylib.DrawRectangleLines(bx2-1, barTop-1, barW+2, barH+2, new Color(180,0,0,200));
                }
            }

            
            if (_texIdle.Width>0 && _texWalk.Width>0)
            {
                Texture2D ct  = _player.IsMoving ? _texWalk : _texIdle;
                int maxF = _player.IsMoving ? 24 : 10;
                int cols = _player.IsMoving ?  4 : 10;
                int rows = _player.IsMoving ?  6 :  1;
                int fn   = _player.CurrentFrame % maxF;
                float fw = (float)ct.Width/cols, fh=(float)ct.Height/rows;
                float sx = (fn%cols)*fw, sy2=(fn/cols)*fh;
                float rw = _player.IsFacingLeft ? fw : -fw;
                var src2 = new Rectangle(sx,sy2,rw,fh);
                var dst2 = new Rectangle(_player.Position.X,_player.Position.Y,fw*1.5f,fh*1.5f);
                var org2 = new System.Numerics.Vector2(fw*1.5f/2, fh*1.5f/2);
                Color pc = (_player.IsDead || _player.HitTimer>0) ? new Color(255,80,80,255) : Color.White;
                Raylib.DrawTexturePro(ct,src2,dst2,org2,0f,pc);
            }
            else
                Raylib.DrawCircle((int)_player.Position.X,(int)_player.Position.Y,15,new Color(80,140,255,255));


            if (_player.IsShielded)
            {
                float pulse = (float)(0.55 + 0.45*Math.Sin(Raylib.GetTime()*9));
                Raylib.DrawCircleLines((int)_player.Position.X,(int)_player.Position.Y,
                    30, new Color((byte)255,(byte)220,(byte)60,(byte)(int)(255*pulse)));
                Raylib.DrawCircle((int)_player.Position.X,(int)_player.Position.Y,
                    30, new Color((byte)255,(byte)220,(byte)60,(byte)(int)(40*pulse)));
            }


            {
                float hpR2 = _player.CurrentHP / _player.MaxHP;
                int   pbx  = (int)_player.Position.X - 22;
                int   pby  = (int)_player.Position.Y + 28;
                Raylib.DrawRectangle(pbx-1, pby-1, 46, 8, new Color(0,0,0,160));
                Raylib.DrawRectangle(pbx, pby, 44, 6, new Color(40,0,0,200));
                Color phc = hpR2 > 0.5f ? new Color(60,200,60,255) : hpR2 > 0.25f ? new Color(220,160,0,255) : new Color(220,40,40,255);
                Raylib.DrawRectangle(pbx, pby, (int)(44*hpR2), 6, phc);
            }

            // 데미지 텍스트 렌더링
            foreach (var t in _damageTexts)
            {
                bool isHeal = t.Damage < 0;
                string txt  = isHeal ? $"+{(-t.Damage):F0}" : t.Damage.ToString("F0");
                float  life = 1f - Math.Min(t.Timer / t.Lifetime, 1f);
                byte   alpha= (byte)(int)(255 * life);
                float  sz   = 1f + (1f - life) * 0.4f;
                int    fs   = (int)(isHeal ? 17*sz : 16*sz);
                Color  dc   = isHeal ? new Color((byte)80,(byte)255,(byte)120,alpha)
                                     : (t.Damage > 60 ? new Color((byte)255,(byte)80,(byte)40,alpha)
                                                       : new Color((byte)255,(byte)230,(byte)60,alpha));

                DrawTextKR(txt, (int)t.Position.X-9, (int)t.Position.Y-19, fs, new Color((byte)0,(byte)0,(byte)0,(byte)(alpha/2)));
                DrawTextKR(txt, (int)t.Position.X-10, (int)t.Position.Y-20, fs, dc);
            }

            Raylib.EndMode2D();



            float expR = (float)_levelSystem.CurrentExp / _levelSystem.MaxExp;
            Raylib.DrawRectangle(0, 0, 800, 6, new Color(10,10,30,220));
            Raylib.DrawRectangle(0, 0, (int)(800*expR), 6, new Color(60,120,255,255));
            Raylib.DrawRectangle(0, 5, (int)(800*expR), 2, new Color(160,200,255,140));


            Raylib.DrawRectangle(0, 6, 800, 36, new Color(8,8,20,210));
            Raylib.DrawLine(0, 42, 800, 42, new Color(30,30,60,200));

            
            Raylib.DrawRectangle(6, 10, 60, 24, new Color(40,80,160,200));
            DrawTextKR($"Lv.{_levelSystem.Level}", 10, 13, 18, Color.White);


            float hpRhud = Math.Max(0, _player.CurrentHP / _player.MaxHP);
            Raylib.DrawRectangle(72, 12, 130, 16, new Color(20,0,0,200));
            Color hphud = hpRhud > 0.5f ? new Color(60,200,60,255) : hpRhud > 0.25f ? new Color(220,150,0,255) : new Color(220,40,40,255);
            Raylib.DrawRectangle(72, 12, (int)(130*hpRhud), 16, hphud);
            Raylib.DrawRectangleLines(72, 12, 130, 16, new Color(60,60,80,200));
            DrawTextKR($"{(int)_player.CurrentHP}/{(int)_player.MaxHP}", 75, 13, 13, Color.White);


            Raylib.DrawRectangle(212, 10, 90, 24, new Color(50,40,0,180));
            DrawTextKR($"G {_player.Gold}", 218, 13, 16, Color.Gold);


            int min=(int)_survivalTime/60, sec=(int)_survivalTime%60;
            string timeStr = $"{min:D2}:{sec:D2}";
            Raylib.DrawRectangle(340, 8, 120, 28, new Color(15,15,40,220));
            Raylib.DrawRectangleLines(340, 8, 120, 28, new Color(50,50,90,200));
            DrawTextKR(timeStr, 358, 12, 22, new Color(200,210,255,255));


            DrawTextKR($"ATK {_weapon.StaffDamage * _weapon.AccDamageMult:F0}", 668, 13, 15, new Color(255,180,80,220));


            if (_finalBossSpawned && _finalBoss != null && !_finalBoss.IsDead)
            {
                float bp = (float)(0.5 + 0.5*Math.Sin(Raylib.GetTime()*4));
                Color wc = new Color((byte)255,(byte)(int)(40+40*bp),(byte)0,(byte)255);
                DrawTextKR("?? FINAL BOSS", 318, 549, 18, wc);
                float br = Math.Max(0, _finalBoss.HP / _finalBoss.MaxHP);
                Raylib.DrawRectangle(60, 570, 680, 18, new Color(20,0,0,220));
                Raylib.DrawRectangle(60, 570, (int)(680*br), 18, new Color(200,20,20,255));

                for (int seg = 1; seg < 4; seg++)
                    Raylib.DrawLine(60 + 680*seg/4, 570, 60 + 680*seg/4, 588, new Color(0,0,0,100));
                Raylib.DrawRectangle(60, 568, (int)(680*br), 2, new Color(255,120,120,180));
                Raylib.DrawRectangleLines(60, 570, 680, 18, new Color(150,0,0,255));
                DrawTextKR($"{(int)_finalBoss.HP:N0} / {(int)_finalBoss.MaxHP:N0}", 330, 571, 13, new Color(255,200,200,220));
            }

            if (_currentState == GameState.LevelUp) RenderLevelUpCards();
            if (_currentState == GameState.ChestReward) RenderChestReward();

            if (_currentState == GameState.GameOver)
            {

                for (int row2 = 0; row2 < 600; row2++)
                {
                    float rf = row2 / 600f;
                    byte ra = (byte)(int)(180 * (1 - rf * 0.3f));
                    Raylib.DrawLine(0, row2, 800, row2, new Color((byte)(int)(80*rf),(byte)0,(byte)0,ra));
                }

                Raylib.DrawRectangle(160, 130, 480, 320, new Color(10,0,0,230));
                Raylib.DrawRectangleLines(160, 130, 480, 320, new Color(180,0,0,255));
                Raylib.DrawRectangleLines(162, 132, 476, 316, new Color(80,0,0,200));

                for (int g = 4; g >= 1; g--)
                    Raylib.DrawText("YOU  DIED", 218-g, 158-g, 56, new Color((byte)180,(byte)0,(byte)0,(byte)(25*g)));
                Raylib.DrawText("YOU  DIED", 218, 158, 56, new Color(220,40,40,255));
                Raylib.DrawLine(180, 224, 620, 224, new Color(80,0,0,200));
                DrawTextKR($"생존 시간   {min:D2} : {sec:D2}", 280, 238, 18, new Color(180,120,120,255));
                DrawTextKR($"획득 골드   {_player.Gold} G", 295, 268, 18, Color.Gold);
                DrawTextKR("획득 골드는 영구 보관됩니다", 258, 296, 15, new Color(120,80,80,255));

                Raylib.DrawRectangle(270, 358, 260, 44, new Color(140,20,20,230));
                Raylib.DrawRectangleLines(270, 358, 260, 44, new Color(220,60,60,255));
                DrawTextKR("R  -  타이틀로 돌아가기", 285, 370, 18, Color.White);
            }
            if (_currentState == GameState.Victory)
            {

                for (int row2 = 0; row2 < 600; row2++)
                {
                    float rf = row2 / 600f;
                    byte ra = (byte)(int)(170 * (1 - rf * 0.2f));
                    Raylib.DrawLine(0, row2, 800, row2, new Color((byte)(int)(20+40*rf),(byte)(int)(30+60*rf),(byte)(int)(60+60*rf),ra));
                }
                Raylib.DrawRectangle(140, 110, 520, 360, new Color(5,10,30,235));
                Raylib.DrawRectangleLines(140, 110, 520, 360, Color.Gold);
                Raylib.DrawRectangleLines(142, 112, 516, 356, new Color(100,80,0,200));

                double gtt = Raylib.GetTime();
                for (int s = 0; s < 12; s++)
                {
                    float ang = (float)(gtt * 0.8 + s * Math.PI * 2 / 12);
                    int   vsx = 400 + (int)(Math.Cos(ang) * (180 + s*8));
                    int   vsy = 290 + (int)(Math.Sin(ang) * (80  + s*4));
                    byte  vsb = (byte)(int)(150 + 105 * Math.Abs(Math.Sin(gtt*2+s)));
                    Raylib.DrawCircle(vsx, vsy, 2, new Color(vsb, vsb, (byte)0, vsb));
                }
                for (int g = 4; g >= 1; g--)
                    Raylib.DrawText("VICTORY!", 228-g, 138-g, 60, new Color((byte)200,(byte)160,(byte)0,(byte)(20*g)));
                Raylib.DrawText("VICTORY!", 228, 138, 60, Color.Gold);
                DrawTextKR("최종 보스를 처치했습니다!", 254, 210, 18, new Color(220,220,180,255));
                Raylib.DrawLine(160, 240, 640, 240, new Color(80,70,0,180));
                DrawTextKR($"생존 시간   {min:D2} : {sec:D2}", 278, 256, 18, new Color(200,200,160,255));
                DrawTextKR($"획득 골드   {_player.Gold} G", 290, 286, 18, Color.Gold);
                DrawTextKR($"총합 골드 획득   {_save.PermanentGold + _player.Gold} G", 248, 316, 18, new Color(255,220,100,255));

                Raylib.DrawRectangle(260, 376, 280, 44, new Color(30,70,20,230));
                Raylib.DrawRectangleLines(260, 376, 280, 44, Color.Gold);
                DrawTextKR("R  -  타이틀로 돌아가기", 278, 388, 18, Color.White);
            }

            // 일시정지 오버레이 렌더링
            if (_currentState == GameState.Pause) RenderPauseMenu();

            Raylib.EndDrawing();
        }

        private void RenderLevelUpCards()
        {

            Raylib.DrawRectangle(0, 0, 800, 600, new Color(0,0,0,170));

            // ?쒕ぉ
            for (int g = 3; g >= 1; g--)
                Raylib.DrawText("LEVEL  UP", 272-g, 36-g, 46, new Color((byte)200,(byte)160,(byte)0,(byte)(30*g)));
            Raylib.DrawText("LEVEL  UP", 272, 36, 46, Color.Gold);
            DrawTextKR("업그레이드를 선택하세요", 288, 90, 18, new Color(160,160,200,255));

            var cards = _cardDeck.CurrentCards;
            int cardCount = cards.Count;
            if (cardCount == 0) return;

            int cardW = 188, cardH = 250, spacing = 16;
            int totalW = cardCount * cardW + (cardCount - 1) * spacing;
            int startX = (800 - totalW) / 2;
            int cardY  = 116;
            string[] keys = { "1", "2", "3" };

            for (int i = 0; i < cardCount; i++)
            {
                var  card = cards[i];
                int  cx   = startX + i * (cardW + spacing);
                bool bonus = card.IsBonus;


                Raylib.DrawRectangle(cx+6, cardY+6, cardW, cardH, new Color(0,0,0,100));


                Raylib.DrawRectangle(cx, cardY, cardW, cardH, card.CardColor);


                Raylib.DrawRectangle(cx, cardY, cardW, 6, card.BorderColor);


                Raylib.DrawRectangleLines(cx, cardY, cardW, cardH, card.BorderColor);
                Raylib.DrawRectangleLines(cx+2, cardY+2, cardW-4, cardH-4,
                    new Color(card.BorderColor.R, card.BorderColor.G, card.BorderColor.B, (byte)50));


                int iconH = 68;
                Raylib.DrawRectangle(cx, cardY+6, cardW, iconH, new Color(0,0,0,50));
                Texture2D cardIcon = GetCardIcon(card);
                if (cardIcon.Id != 0)
                    DrawItemIcon(cardIcon, cx + cardW/2 - 24, cardY + 16, 48);
                else if (card.IsBonus && (card.BonusType == BonusCardType.HealSmall || card.BonusType == BonusCardType.HealLarge) && _texItems32.Id != 0)
                {
                    // 32x32.png: 왼쪽 2번째(col=1), 위에서 33번째(row=32) → X=32, Y=1024
                    var breadSrc = new Rectangle(32, 1024, 32, 32);
                    var breadDst = new Rectangle(cx + cardW/2 - 24, cardY + 16, 48, 48);
                    Raylib.DrawTexturePro(_texItems32, breadSrc, breadDst, new System.Numerics.Vector2(0,0), 0f, Color.White);
                }
                else
                    DrawTextKR(card.Icon, cx + cardW/2 - 16, cardY + 18, 40, card.BorderColor);


                if (card.IsNewWeapon)
                {
                    Raylib.DrawRectangle(cx+8, cardY+iconH+10, cardW-16, 20, new Color(255,170,0,210));
                    DrawTextKR("NEW!", cx + cardW/2 - 18, cardY+iconH+12, 15, new Color(20,10,0,255));
                }

                int titleY = card.IsNewWeapon ? cardY+iconH+34 : cardY+iconH+12;
                DrawTextKR(card.Title, cx+10, titleY, 16, Color.White);


                int divY2 = titleY + 24;
                Raylib.DrawLine(cx+10, divY2, cx+cardW-10, divY2,
                    new Color(card.BorderColor.R, card.BorderColor.G, card.BorderColor.B, (byte)80));

                DrawWrappedTextKR(card.Description, cx+10, divY2+8, cardW-20, 14, new Color(195,195,205,255));


                string statLine = GetStatPreview(card);
                if (statLine != "")
                {
                    int statY = cardY + cardH - 40;
                    Raylib.DrawRectangle(cx+6, statY-2, cardW-12, 22, new Color(0,0,0,90));
                    DrawTextKR(statLine, cx+10, statY+1, 13, new Color(255,225,90,255));
                }


                int btnY = cardY + cardH + 10;
                Raylib.DrawRectangle(cx + cardW/2 - 20, btnY, 40, 30, card.BorderColor);
                Raylib.DrawRectangleLines(cx + cardW/2 - 20, btnY, 40, 30, Color.White);
                DrawTextKR(keys[i], cx + cardW/2 - 6, btnY + 7, 18, Color.Black);
            }

            DrawTextKR("키보드  1 / 2 / 3  으로 선택", 284, 474, 17, new Color(130,130,160,255));
        }




        private void RenderChestReward()
        {
            Raylib.DrawRectangle(0, 0, 800, 600, new Color((byte)0, (byte)0, (byte)0, (byte)230));

            int cx = 400; 
            int cy = 400; 
            int count = _chestRewards.Count;
            float t = Math.Min(_chestAnimTimer, 1.0f); 
            float easeOut = 1f - (1f - t) * (1f - t);


            if (_chestAnimTimer > 0.2f)
            {
                float beamLength = 600f * easeOut;

                Color[] beam1 = { new Color((byte)60, (byte)120, (byte)255, (byte)180) }; 
                Color[] beam3 = {
                    new Color((byte)255, (byte)50, (byte)50, (byte)180),
                    new Color((byte)60,  (byte)120,(byte)255,(byte)180),  
                    new Color((byte)50,  (byte)220,(byte)80, (byte)180),
                };
                Color[] beam5 = {
                    new Color((byte)180,(byte)50, (byte)255,(byte)180),
                    new Color((byte)255,(byte)50, (byte)50, (byte)180),
                    new Color((byte)60, (byte)120,(byte)255,(byte)180),  
                    new Color((byte)50, (byte)220,(byte)80, (byte)180),
                    new Color((byte)180,(byte)50, (byte)255,(byte)180),
                };
                Color[] beamColors = count == 1 ? beam1 : count == 3 ? beam3 : beam5;

                for (int i = 0; i < count; i++)
                {
                    float angle = -1.57f;
                    if (count > 1) angle += -0.5f + (1.0f / (count - 1)) * i;

                    Color beamColor = i < beamColors.Length ? beamColors[i] : new Color((byte)200, (byte)200, (byte)200, (byte)150);

                    Vector2 top = new Vector2(cx + (float)Math.Cos(angle) * beamLength, cy + (float)Math.Sin(angle) * beamLength);
                    Raylib.DrawLineEx(new System.Numerics.Vector2(cx, cy), new System.Numerics.Vector2(top.X, top.Y), 70f * easeOut, beamColor);
                }
            }

            // 상자 열기 파티클 이펙트
            if (_chestAnimTimer > 0.1f && _chestAnimTimer < 2.5f)
            {
                int particleCount = count * 15; 
                for (int i = 0; i < particleCount; i++)
                {
                    float pAngle = (i * 137.5f) * (float)Math.PI / 180f; 
                    float pSpeed = 200f + (i % 7) * 50f;
                    float pTime = _chestAnimTimer - 0.1f;
                    
                    float px = cx + (float)Math.Cos(pAngle) * pSpeed * pTime;
                    float py = cy + (float)Math.Sin(pAngle) * pSpeed * pTime + 600f * pTime * pTime; 

                    Raylib.DrawCircle((int)px, (int)py, 5, Color.Gold);
                    Raylib.DrawCircleLines((int)px, (int)py, 5, new Color((byte)200, (byte)150, (byte)0, (byte)255));
                }
            }


            float bounce = _chestAnimTimer < 0.5f ? (float)Math.Sin(_chestAnimTimer * 10f) * 10f : 0f;

            for (int i = 0; i < count; i++) {
                float lockTime = 1.5f + (i * 0.8f);
                if (_chestAnimTimer > lockTime && _chestAnimTimer < lockTime + 0.15f) bounce = -15f; 
            }

            int drawY = cy + (int)bounce;
            Raylib.DrawRectangle(cx - 50, drawY - 30, 100, 60, new Color((byte)139, (byte)90, (byte)43, (byte)255));
            Raylib.DrawRectangleLines(cx - 50, drawY - 30, 100, 60, new Color((byte)80, (byte)50, (byte)20, (byte)255));
            if (_chestAnimTimer > 0.3f) Raylib.DrawRectangle(cx - 50, drawY - 45, 100, 15, new Color((byte)160, (byte)110, (byte)55, (byte)255));
            Raylib.DrawRectangle(cx - 10, drawY - 10, 20, 20, new Color((byte)220, (byte)180, (byte)50, (byte)255));


            string[] dummyNames = { "지팡이", "영창", "궤도구체", "도끼", "신발", "갑옷", "반지", "장갑", "마법진", "금화 주머니", "???" };

            if (_chestAnimTimer > 1.0f)
            {
                int spacing = 60; 
                int startY = 250 - (count * spacing / 2);

                for (int i = 0; i < count; i++)
                {

                    float lockTime = 1.5f + (i * 0.8f); 

                    if (_chestAnimTimer < lockTime)
                    {

                        int randIdx = (int)(_chestAnimTimer * 30 + i * 7) % dummyNames.Length;
                        string spinText = dummyNames[randIdx];


                        int shakeY = (int)(Math.Sin(_chestAnimTimer * 50 + i) * 3);

                        Raylib.DrawRectangle(150, startY + (i * spacing) - 15, 500, 40, new Color((byte)0, (byte)0, (byte)0, (byte)150));
                        DrawTextKR(spinText, 260, startY + (i * spacing) + shakeY, 28, Color.Gray);
                    }
                    else
                    {

                        float timeSinceLock = _chestAnimTimer - lockTime;
                        Color textColor = _chestRewards[i].Contains("★") ? Color.Gold : Color.White;
                        

                        if (timeSinceLock < 0.1f) 
                        {
                            Raylib.DrawRectangle(150, startY + (i * spacing) - 15, 500, 40, new Color((byte)255, (byte)255, (byte)255, (byte)200));
                        } 
                        else 
                        {
                            Raylib.DrawRectangle(150, startY + (i * spacing) - 15, 500, 40, new Color((byte)0, (byte)0, (byte)0, (byte)150));
                            DrawTextKR(_chestRewards[i], 260, startY + (i * spacing), 28, textColor);
                        }
                    }
                }
            }


            float exitTime = 1.5f + (count * 0.8f) + 0.5f;
            if (_chestAnimTimer > exitTime)
            {
                DrawTextKR("ENTER 게임으로 이동", 320, 520, 20, Color.LightGray);
                if ((int)(Raylib.GetTime() * 4) % 2 == 0) 
                    Raylib.DrawRectangleLines(300, 505, 200, 40, Color.Gold);
            }
        }

       
        private void RenderShop()
        {
            Raylib.BeginDrawing();

            for (int row2 = 0; row2 < 600; row2++)
            {
                float rf = row2/600f;
                Raylib.DrawLine(0, row2, 800, row2,
                    new Color((byte)(8+4*(int)rf),(byte)(8+4*(int)rf),(byte)(18+10*(int)rf),(byte)255));
            }

  
            Raylib.DrawRectangle(0, 0, 800, 58, new Color(10,10,25,230));
            Raylib.DrawLine(0, 58, 800, 58, new Color(60,50,20,255));
            for (int g = 3; g >= 1; g--)
                Raylib.DrawText("SHOP", 330-g, 10-g, 40, new Color((byte)180,(byte)130,(byte)0,(byte)(25*g)));
            Raylib.DrawText("SHOP", 330, 10, 40, Color.Gold);
            Raylib.DrawRectangle(560, 14, 220, 30, new Color(40,34,0,200));
            Raylib.DrawRectangleLines(560, 14, 220, 30, new Color(100,80,0,200));
            DrawTextKR($"보유골드  {_save.PermanentGold} G", 572, 18, 20, Color.Gold);

            var upgrades = MetaTable.All;
            int rowH = 74, startY = 72;
            for (int i = 0; i < upgrades.Count; i++)
            {
                var  def  = upgrades[i];
                int  lv   = _save.GetMetaLevel(def.Type);
                bool max  = lv >= def.MaxLevel;
                bool sel  = (i == _shopCursor);
                int  ry   = startY + i * rowH;


                Color bg = sel ? new Color(30,30,55,240) : new Color(14,14,28,210);
                Raylib.DrawRectangle(30, ry, 740, rowH-5, bg);
                Color bd = sel ? Color.Gold : new Color(40,40,65,200);
                Raylib.DrawRectangleLines(30, ry, 740, rowH-5, bd);
                if (sel) Raylib.DrawRectangle(30, ry, 4, rowH-5, Color.Gold);

                if (i < _shopIcons.Length && _shopIcons[i].Id != 0)
                {
                    Rectangle iconSrc = new Rectangle(0, 0, _shopIcons[i].Width, _shopIcons[i].Height);
                    Rectangle iconDst = new Rectangle(48, ry + 14, 42, 42);
                    Raylib.DrawRectangle(44, ry + 10, 50, 50, new Color(0, 0, 0, 90));
                    Raylib.DrawRectangleLines(44, ry + 10, 50, 50, sel ? Color.Gold : new Color(70, 70, 95, 180));
                    Raylib.DrawTexturePro(_shopIcons[i], iconSrc, iconDst, new System.Numerics.Vector2(0, 0), 0f, Color.White);
                }

                // ?대쫫
                Color nc = max ? new Color(80,80,80,255) : (sel ? Color.White : new Color(200,200,210,255));
                DrawTextKR((max?"[MAX] ":"")+def.Name, 106, ry+8, 20, nc);
                DrawTextKR(def.Description, 106, ry+34, 14, new Color(130,130,150,255));


                DrawLevelSquares(440, ry+20, lv, def.MaxLevel);


                if (max)
                    DrawTextKR("MAX", 660, ry+22, 20, new Color(80,80,80,255));
                else
                {
                    int  cost   = def.Cost(lv);
                    bool canBuy = _save.PermanentGold >= cost;
                    Raylib.DrawRectangle(620, ry+10, 130, 30, canBuy?new Color(30,50,0,200):new Color(50,0,0,180));
                    Raylib.DrawRectangleLines(620, ry+10, 130, 30, canBuy?new Color(120,200,60,200):new Color(150,40,40,200));
                    DrawTextKR($"{cost} G", 632, ry+15, 18, canBuy?Color.Gold:new Color(180,60,60,255));
                }


                string prev = GetMetaEffectPreview(def, lv);
                if (prev != "") DrawTextKR(prev, 440, ry+44, 13, new Color(100,200,100,255));
            }

            Raylib.DrawLine(30, startY + upgrades.Count*rowH - 2, 770, startY + upgrades.Count*rowH - 2, new Color(40,40,60,200));
            DrawTextKR("↑ ↓  선택    ENTER  구매    ESC  타이틀", 228, 566, 18, new Color(80,80,110,255));
            Raylib.EndDrawing();
        }

        private string GetMetaEffectPreview(MetaUpgradeDef def, int currentLevel)
        {
            if (currentLevel >= def.MaxLevel) return $"최대 효과: +{def.TotalValue(def.MaxLevel)}";
            float next = def.Values[currentLevel];
            return def.Type switch {
                MetaUpgradeType.MaxHP       => $"다음: 최대HP +{next:F0}",
                MetaUpgradeType.MoveSpeed   => $"다음: 이동속도 +{next:F0}",
                MetaUpgradeType.StartDamage => $"다음: 데미지 +{next*100:F0}%",
                MetaUpgradeType.StartGold   => $"다음: 시작골드 +{next:F0}G",
                MetaUpgradeType.ExpBonus    => $"다음: 경험치 +{next*100:F0}%",
                MetaUpgradeType.Revive      => $"다음: 부활 +{next:F0}회",
                _ => ""
            };
        }

        
        private void RenderRecipeBook()
        {
            Raylib.BeginDrawing();
            for (int row2 = 0; row2 < 600; row2++)
            {
                float rf = row2/600f;
                Raylib.DrawLine(0,row2,800,row2,
                    new Color((byte)(8+4*(int)rf),(byte)(8+4*(int)rf),(byte)(20+12*(int)rf),(byte)255));
            }

            // ???ㅻ뜑
            Raylib.DrawRectangle(0,0,800,56, new Color(10,10,25,235));
            Raylib.DrawLine(0,56,800,56,new Color(40,40,70,255));

            bool t0 = _recipePage==0, t1 = _recipePage==1;
            Raylib.DrawRectangle(30,  8, 340, 42, t0?new Color(35,35,70,255):new Color(15,15,30,200));
            Raylib.DrawRectangle(430, 8, 340, 42, t1?new Color(35,35,70,255):new Color(15,15,30,200));
            if (t0) Raylib.DrawRectangle(30, 8,  4, 42, new Color(100,160,255,255));
            if (t1) Raylib.DrawRectangle(430,8,  4, 42, Color.Gold);
            Raylib.DrawRectangleLines(30, 8,340,42,t0?new Color(80,120,220,200):new Color(30,30,55,200));
            Raylib.DrawRectangleLines(430,8,340,42,t1?Color.Gold:new Color(30,30,55,200));
            DrawTextKR("무기 & 장신구", 100, 18, 22, t0?Color.White:new Color(100,100,130,255));
            DrawTextKR("조합표",     505, 18, 22, t1?Color.Gold:new Color(100,100,130,255));

            if (_recipePage==0) RenderRecipeWeapons();
            else                RenderRecipeEvolution();

            DrawTextKR("← →  탭 전환      ESC  타이틀로", 258, 570, 16, new Color(60,60,90,255));
            Raylib.EndDrawing();
        }

        private void RenderRecipeWeapons()
        {
            (string name, string desc, string stats, Color ac)[] weapons = {
                ("지팡이",   "가장 가까운 적에게 마법 투사체 발사", "Lv1: DMG15  |  Lv3: x2발  |  Lv5: DMG55 x3발", new Color(80,140,255,255)),
                ("영창",     "주변 적에게 지속 범위 피해", "Lv1: R70  |  Lv3: R95  |  Lv5: DMG28 R130", new Color(100,220,100,255)),
                ("궤도구체", "플레이어 주위를 선회하는 구체", "Lv1: x2  |  Lv3: x3  |  Lv5: DMG55 x4", new Color(40,200,255,255)),
                ("도끼",     "위로 투척, 무한 관통으로 여러 적 타격", "Lv1: DMG25 x1  |  Lv3: x2  |  Lv5: DMG80 x3", new Color(255,140,40,255)),
                ("표창",     "부메랑 투척, 돌아온 후 다음 발사", "Lv1: DMG18 x1  |  Lv3: x2  |  Lv5: DMG60 x3", new Color(80,220,220,255)),
            };
            (string name, string desc, string eff, Color ac)[] accs = {
                ("신발",   "투사체 속도, 이동속도, 투사체 개수 강화", "Lv3/5: 투사체 +1/+2", new Color(160,160,255,255)),
                ("갑옷",   "레벨업마다 최대 체력 증가와 즉시 회복", "Lv5 합계: 최대HP +130", new Color(220,80,80,255)),
                ("반지",   "모든 무기 공격 범위 확대", "Lv5: 범위 +60%", new Color(160,220,120,255)),
                ("장갑",   "모든 무기 데미지 배율 증가", "Lv5: 데미지 +60%", new Color(255,200,60,255)),
                ("목걸이", "경험치 획득량 증가", "Lv5: 경험치 획득 x2", new Color(180,120,255,255)),
            };

            int wy = 66, rwh = 50;
            Texture2D[] weaponIcons = { _texStaffIcon, _texGarlicIcon, _texOrbitalIcon, _texAxeIcon, _texShurikenIcon };
            Texture2D[] accIcons = { _texShoesIcon, _texArmorIcon, _texRingIcon, _texGloveIcon, _texNecklaceIcon };
            DrawTextKR("▶  무  기", 40, wy, 18, new Color(80,140,255,255));
            wy += 24;
            int weaponIndex = 0;
            foreach (var w in weapons)
            {
                Raylib.DrawRectangle(28, wy, 744, rwh-3, new Color(14,20,40,220));
                Raylib.DrawRectangleLines(28, wy, 744, rwh-3, new Color(40,60,110,200));
                Raylib.DrawRectangle(28, wy, 4, rwh-3, w.ac);
                Raylib.DrawRectangle(36, wy+5, 32, 32, new Color(w.ac.R,w.ac.G,w.ac.B,(byte)50));
                if (weaponIndex < weaponIcons.Length) DrawItemIcon(weaponIcons[weaponIndex], 36, wy+5, 32);
                DrawTextKR(w.name, 76, wy+4,  17, Color.White);
                DrawTextKR(w.desc, 76, wy+24, 12, new Color(140,140,160,255));
                DrawTextKR(w.stats, 360, wy+16, 12, new Color(255,210,80,255));
                weaponIndex++;
                wy += rwh;
            }

            wy += 6;
            DrawTextKR("▶  장  신  구", 40, wy, 18, new Color(255,160,80,255));
            wy += 24;
            int accIndex = 0;
            foreach (var a in accs)
            {
                Raylib.DrawRectangle(28, wy, 744, rwh-3, new Color(30,18,14,220));
                Raylib.DrawRectangleLines(28, wy, 744, rwh-3, new Color(90,50,30,200));
                Raylib.DrawRectangle(28, wy, 4, rwh-3, a.ac);
                Raylib.DrawRectangle(36, wy+5, 32, 32, new Color(a.ac.R,a.ac.G,a.ac.B,(byte)40));
                if (accIndex < accIcons.Length) DrawItemIcon(accIcons[accIndex], 36, wy+5, 32);
                DrawTextKR(a.name, 76, wy+4,  17, Color.White);
                DrawTextKR(a.desc, 76, wy+24, 12, new Color(140,140,160,255));
                DrawTextKR(a.eff,  470,wy+16, 12, new Color(255,210,80,255));
                accIndex++;
                wy += rwh;
            }
        }

        private void RenderRecipeEvolution()
        {
            DrawTextKR("무기 Lv.5  +  장신구 Lv.5  =  진화 무기", 168, 68, 18, new Color(220,200,80,255));
            DrawTextKR("보스 처치 보물상자에서 자동으로 진화됩니다", 155, 94, 15, new Color(110,110,140,255));

            (string w, string a, string r, string d, Color c)[] evos = {
                ("지팡이 Lv.5", "신발 Lv.5",   "헬파이어", "3발 버스트 관통탄을 연속 발사", new Color(255,80,40,255)),
                ("영창 Lv.5",   "갑옷 Lv.5",   "마법진",   "광역 흡혈과 체력 회복", new Color(160,80,255,255)),
                ("궤도구체 Lv.5", "반지 Lv.5", "블랙홀",   "고속 회전 구체와 흡입", new Color(180,80,255,255)),
                ("도끼 Lv.5",   "장갑 Lv.5",   "도끼폭풍", "8방향 도끼 투척", new Color(255,140,40,255)),
                ("표창 Lv.5",   "목걸이 Lv.5", "무한표창", "돌아오는 표창을 연속 발사", new Color(80,200,255,255)),
            };

            Texture2D[] evoWeaponIcons = { _texStaffIcon, _texGarlicIcon, _texOrbitalIcon, _texAxeIcon, _texShurikenIcon };
            Texture2D[] evoAccIcons = { _texShoesIcon, _texArmorIcon, _texRingIcon, _texGloveIcon, _texNecklaceIcon };
            Texture2D[] evoResultIcons = { _texHellFireIcon, _texMagicCircleIcon, _texBlackHoleIcon, _texAxeStormIcon, _texInfiniteShurikenIcon };

            int ey = 122, rh = 96;
            int evoIndex = 0;
            foreach (var evo in evos)
            {
                Raylib.DrawRectangle(28, ey, 744, rh-6, new Color(12,12,28,230));
                Raylib.DrawRectangleLines(28, ey, 744, rh-6, new Color(evo.c.R/2,evo.c.G/2,evo.c.B/2,(byte)200));
                Raylib.DrawRectangle(28, ey, 4, rh-6, evo.c);


                Raylib.DrawRectangle(44, ey+12, 180, 44, new Color(evo.c.R,evo.c.G,evo.c.B,(byte)20));
                Raylib.DrawRectangleLines(44, ey+12, 180, 44, new Color(evo.c.R,evo.c.G,evo.c.B,(byte)80));
                if (evoIndex < evoWeaponIcons.Length) DrawItemIcon(evoWeaponIcons[evoIndex], 52, ey+18, 26);
                DrawTextKR(evo.w, 84, ey+14, 15, Color.LightGray);

                DrawTextKR("+", 234, ey+22, 24, new Color(150,150,150,255));

                Raylib.DrawRectangle(258, ey+12, 165, 44, new Color(evo.c.R,evo.c.G,evo.c.B,(byte)20));
                Raylib.DrawRectangleLines(258, ey+12, 165, 44, new Color(evo.c.R,evo.c.G,evo.c.B,(byte)80));
                if (evoIndex < evoAccIcons.Length) DrawItemIcon(evoAccIcons[evoIndex], 266, ey+18, 26);
                DrawTextKR(evo.a, 298, ey+14, 15, Color.LightGray);

                DrawTextKR("=", 432, ey+22, 24, new Color(150,150,150,255));


                Raylib.DrawRectangle(456, ey+10, 300, 50, new Color(evo.c.R,evo.c.G,evo.c.B,(byte)25));
                int resultTextX = 466;
                if (evoIndex < evoResultIcons.Length)
                {
                    DrawItemIcon(evoResultIcons[evoIndex], 466, ey+17, 34);
                    resultTextX = 508;
                }
                DrawTextKR(evo.r, resultTextX, ey+14, 22, evo.c);
                DrawTextKR(evo.d,        resultTextX, ey+44, 14, new Color(160,160,170,255));

                evoIndex++;
                ey += rh;
            }
            DrawTextKR("진화 후 원본 무기는 슬롯에서 제거됩니다", 50, ey+6, 14, new Color(80,80,100,255));
        }

        private Texture2D GetProjectileTexture(Projectile p)
        {
            return p.Sprite switch
            {
                ProjectileSprite.StaffBullet => _texStaffBullet,
                ProjectileSprite.Fireball => _texFireball,
                ProjectileSprite.Axe => _texAxeIcon,
                ProjectileSprite.AxeStorm => _texAxeStormIcon,
                ProjectileSprite.Shuriken => _texShurikenIcon,
                ProjectileSprite.InfiniteShuriken => _texInfiniteShurikenIcon,
                _ => default
            };
        }

        private float GetProjectileSpriteSize(Projectile p)
        {
            return p.Sprite switch
            {
                ProjectileSprite.StaffBullet => 22f,
                ProjectileSprite.Fireball => p.IsPiercing ? 30f : 22f,
                ProjectileSprite.Axe => 28f,
                ProjectileSprite.AxeStorm => 30f,
                ProjectileSprite.Shuriken => 26f,
                ProjectileSprite.InfiniteShuriken => 28f,
                _ => 20f
            };
        }

        private Texture2D GetCardIcon(UpgradeCard card)
        {
            if (card.IsBonus) return default;
            if (card.CardType == CardType.Weapon)
            {
                return card.WeaponType switch
                {
                    WeaponType.Staff => _texStaffIcon,
                    WeaponType.Garlic => _texGarlicIcon,
                    WeaponType.Orbital => _texOrbitalIcon,
                    WeaponType.Axe => _texAxeIcon,
                    WeaponType.Shuriken => _texShurikenIcon,
                    WeaponType.MagicCircle => _texMagicCircleIcon,
                    WeaponType.HellFire => _texHellFireIcon,
                    WeaponType.BlackHole => _texBlackHoleIcon,
                    WeaponType.AxeStorm => _texAxeStormIcon,
                    WeaponType.InfiniteShuriken => _texInfiniteShurikenIcon,
                    _ => default
                };
            }

            return card.AccessoryType switch
            {
                AccessoryType.Shoes => _texShoesIcon,
                AccessoryType.Armor => _texArmorIcon,
                AccessoryType.Ring => _texRingIcon,
                AccessoryType.Glove => _texGloveIcon,
                AccessoryType.Necklace => _texNecklaceIcon,
                _ => default
            };
        }

        private void DrawCenteredItemIcon(Texture2D texture, float x, float y, float size, float rotation, Color tint)
        {
            if (texture.Id == 0) return;
            Raylib.DrawTexturePro(
                texture,
                new Rectangle(0, 0, texture.Width, texture.Height),
                new Rectangle(x, y, size, size),
                new System.Numerics.Vector2(size / 2f, size / 2f),
                rotation,
                tint);
        }

        private void DrawItemIcon(Texture2D texture, int x, int y, int size)
        {
            if (texture.Id == 0) return;
            Raylib.DrawTexturePro(
                texture,
                new Rectangle(0, 0, texture.Width, texture.Height),
                new Rectangle(x, y, size, size),
                new System.Numerics.Vector2(0, 0),
                0f,
                Color.White);
        }

        private void DrawTextKR(string text, int x, int y, int fontSize, Color color)
        {
            float scale = (float)fontSize / 32f;
            Raylib.DrawTextEx(_fontKR, text, new System.Numerics.Vector2(x, y), fontSize, scale, color);
        }

        private void DrawWrappedTextKR(string text, int x, int y, int maxWidth, int fontSize, Color color)
        {
            string[] words = text.Split(' ');
            string   line  = "";
            int      lineY = y;
            int      lineH = fontSize + 4;
            float    scale = (float)fontSize / 32f;

            foreach (var word in words)
            {
                string test = line.Length == 0 ? word : line + " " + word;
                var    size = Raylib.MeasureTextEx(_fontKR, test, fontSize, scale);
                if (size.X > maxWidth && line.Length > 0)
                {
                    Raylib.DrawTextEx(_fontKR, line, new System.Numerics.Vector2(x, lineY), fontSize, scale, color);
                    line  = word;
                    lineY += lineH;
                }
                else line = test;
            }
            if (line.Length > 0)
                Raylib.DrawTextEx(_fontKR, line, new System.Numerics.Vector2(x, lineY), fontSize, scale, color);
        }

        private string GetStatPreview(UpgradeCard card)
        {
            if (card.CardType == CardType.Weapon)
            {
                var data = WeaponTable.GetWeapon(card.WeaponType, card.NextLevel);
                switch (card.WeaponType)
                {
                    case WeaponType.Staff: return $"DMG {data.StaffDamage:F0}  CD {data.StaffCooldown:F2}s  x{data.StaffProjectileCount}";
                    case WeaponType.Garlic: return $"DMG {data.GarlicDamage:F0}  R {data.GarlicRadius:F0}";
                    case WeaponType.Orbital: return $"DMG {data.OrbitalDamage:F0}  x{data.OrbitalCount}개";
                    case WeaponType.Axe: return $"DMG {data.AxeDamage:F0}  x{data.AxeCount}개";
                    default: return "";
                }
            }
            else
            {
                var data = WeaponTable.GetAcc(card.AccessoryType, card.NextLevel);
                switch (card.AccessoryType)
                {
                    case AccessoryType.Shoes: 
                        if (card.NextLevel == 1) return "투사체 날아가는 속도 UP";
                        if (card.NextLevel == 2) return "플레이어 이동 속도 UP";
                        if (card.NextLevel == 4) return "투사체 크기 UP";
                        return $"투사체 개수 +{data.ValueInt}";
                    case AccessoryType.Armor: return $"최대 체력 +{data.ValueFloat:F0}";
                    case AccessoryType.Ring: return $"공격 범위 +{(data.ValueFloat - 1f) * 100:F0}%";
                    case AccessoryType.Glove: return $"모든 데미지 +{(data.ValueFloat - 1f) * 100:F0}%";
                    default: return "";
                }
            }
        }

        
        private void RenderPauseMenu()
        {
            Raylib.DrawRectangle(0, 0, 800, 600, new Color(0,0,0,200));

            Raylib.DrawRectangle(30, 20, 740, 556, new Color(10,10,24,240));
            Raylib.DrawRectangleLines(30, 20, 740, 556, new Color(50,50,90,255));
            Raylib.DrawRectangle(30, 20, 740, 4, new Color(80,120,255,200));

            for (int g=3;g>=1;g--)
                Raylib.DrawText("PAUSE", 328-g, 34-g, 38, new Color((byte)60,(byte)80,(byte)160,(byte)(30*g)));
            Raylib.DrawText("PAUSE", 328, 34, 38, new Color(140,160,255,255));
            Raylib.DrawLine(50, 82, 750, 82, new Color(40,40,70,200));


            Raylib.DrawRectangle(40, 92, 340, 400, new Color(14,14,30,220));
            Raylib.DrawRectangleLines(40, 92, 340, 400, new Color(40,40,70,200));
            Raylib.DrawRectangle(40, 92, 340, 4, new Color(80,120,255,180));
            DrawTextKR("능력치", 158, 100, 20, new Color(160,180,255,255));
            Raylib.DrawLine(56, 128, 368, 128, new Color(40,40,70,200));

            int sy2 = 138;
            (string label, string val)[] stats2 = {
                ("최대 체력", $"{_player.MaxHP:F0}"),
                ("이동 속도", $"{_player.Speed:F0}"),
                ("데미지 보너스", $"+{(_weapon.AccDamageMult-1f)*100:F0}%"),
                ("공격 범위", $"+{(_weapon.AccAreaMult-1f)*100:F0}%"),
                ("투사체 추가", $"+{_weapon.AccProjectileBonus}개"),
            };
            foreach (var st in stats2)
            {
                DrawTextKR(st.label, 58, sy2, 17, new Color(130,130,160,255));
                DrawTextKR(st.val,   290, sy2, 17, new Color(200,210,255,255));
                Raylib.DrawLine(56, sy2+22, 368, sy2+22, new Color(25,25,45,200));
                sy2 += 30;
            }

            Raylib.DrawRectangle(420, 92, 340, 400, new Color(14,14,30,220));
            Raylib.DrawRectangleLines(420, 92, 340, 400, new Color(40,40,70,200));
            Raylib.DrawRectangle(420, 92, 340, 4, Color.Gold);
            DrawTextKR("보유 장비", 534, 100, 20, Color.Gold);
            Raylib.DrawLine(436, 128, 748, 128, new Color(40,40,70,200));

            int ey2 = 138;
            DrawTextKR("무기", 436, ey2, 15, new Color(80,140,255,255));
            ey2 += 22;
            foreach (var w in _cardDeck.WeaponLevels)
            {
                if (w.Value > 0)
                {
                    DrawTextKR(GetWeaponNameUI(w.Key), 436, ey2, 16, new Color(160,190,255,255));
                    bool isEvo = w.Key == WeaponType.MagicCircle || w.Key == WeaponType.HellFire ||
                                 w.Key == WeaponType.BlackHole   || w.Key == WeaponType.AxeStorm ||
                                 w.Key == WeaponType.InfiniteShuriken;
                    DrawLevelSquares(620, ey2+3, isEvo ? 1 : w.Value, isEvo ? 1 : 5);
                    ey2 += 28;
                }
            }
            ey2 += 6;
            DrawTextKR("장신구", 436, ey2, 15, new Color(255,160,80,255));
            ey2 += 22;
            foreach (var a in _cardDeck.AccessoryLevels)
            {
                if (a.Value > 0)
                {
                    DrawTextKR(GetAccNameUI(a.Key), 436, ey2, 16, new Color(255,190,130,255));
                    DrawLevelSquares(620, ey2+3, a.Value, 5);
                    ey2 += 28;
                }
            }

            Raylib.DrawLine(50, 500, 750, 500, new Color(40,40,70,200));
            DrawTextKR("BGM 볼륨", 400, 532, 18, Color.White);
            Raylib.DrawRectangle(500, 540, 200, 4, Color.Gray); // 슬라이더 배경
            Raylib.DrawRectangle(500 + (int)(_bgmVolume * 200) - 5, 535, 10, 14, Color.Gold); // 조절 노브
            DrawTextKR("ESC  게임으로 돌아가기       Q  게임 종료", 200, 510, 18, new Color(80,80,110,255));
        }

        private void DrawLevelSquares(int x, int y, int level, int maxLevel)
        {
            for (int i = 0; i < maxLevel; i++)
            {
                int sx = x + i * 18;
                if (i < level)
                {
                    Raylib.DrawRectangle(sx, y, 14, 14, Color.Gold);
                    Raylib.DrawRectangle(sx, y, 14, 3, new Color(255,240,160,180)); 
                }
                else
                {
                    Raylib.DrawRectangle(sx, y, 14, 14, new Color(20,20,35,200));
                    Raylib.DrawRectangleLines(sx, y, 14, 14, new Color(50,50,70,200));
                }
            }
        }

        private string GetWeaponNameUI(WeaponType t) => t switch { WeaponType.Staff => "지팡이", WeaponType.Garlic => "영창", WeaponType.Orbital => "궤도구체", WeaponType.Axe => "도끼", WeaponType.Shuriken => "표창", WeaponType.HellFire => "헬파이어★", WeaponType.MagicCircle => "마법진★", WeaponType.BlackHole => "블랙홀★", WeaponType.AxeStorm => "도끼폭풍★", WeaponType.InfiniteShuriken => "무한표창★", _ => "???" };
        private string GetAccNameUI(AccessoryType t) => t switch { AccessoryType.Shoes => "신발", AccessoryType.Armor => "갑옷", AccessoryType.Ring => "반지", AccessoryType.Glove => "장갑", AccessoryType.Necklace => "목걸이", _ => "???" };
    }
}