// 파일명: GameCore.cs
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

    // Shop = 타이틀 상점, RecipeBook = 조합표
    public enum GameState { Title, Shop, RecipeBook, Playing, LevelUp, ChestReward, Pause, GameOver, Victory }

    public class Engine
    {
        private int _titleMenuIdx = 0;
        private Player        _player;
        private List<Enemy>   _enemies;
        private Weapon        _weapon;
        private List<ExpGem>  _gems;
        private LevelSystem   _levelSystem;
        private CardDeck      _cardDeck;        

        private List<DropItem> _dropItems  = new List<DropItem>();
        private List<MapChest> _mapChests  = new List<MapChest>();
        
        // 상자 보상 메시지를 담아둘 리스트
        private List<string> _chestRewards = new List<string>();
        private float _chestAnimTimer = 0f;

        // ★ 세이브 데이터 (타이틀 상점 / 영구 골드)
        private SaveFile _save;

        // ★ 타이틀 상점 커서
        private int _shopCursor = 0;

        // ★ 조합표 탭 (0=무기조합, 1=진화조합)
        private int _recipePage = 0;

        private Random _rand = new Random();

        private GameState _currentState = GameState.Title;
        private float _spawnTimer  = 0f;
        private float _survivalTime= 0f;

        // 중간보스 스폰 플래그 (1:00 / 2:00 / 2:30 / 3:00 / 3:30 / 4:00 / 4:30)
        private bool _midBoss1Spawned = false; // 1:00
        private bool _midBoss2Spawned = false; // 2:00
        private bool _midBoss3Spawned = false; // 2:30
        private bool _midBoss4Spawned = false; // 3:00
        private bool _midBoss5Spawned = false; // 3:30
        private bool _midBoss6Spawned = false; // 4:00
        private bool _midBoss7Spawned = false; // 4:30

        // 최종 보스
        private bool  _finalBossSpawned = false;
        private Enemy _finalBoss        = null;

        private List<DamageText> _damageTexts;
        private Camera2D _camera;
        private Texture2D _texIdle, _texTitleIdle, _texWalk, _texEnemy, _texFloor;
        private Font _fontKR;  
        private List<Texture2D> _gemTextures = new List<Texture2D>();
        private string[] _gemFileNames =
        {
            "image/MonedaP.png","image/MonedaD.png","image/MonedaR.png",
            "image/spr_coin_gri.png","image/spr_coin_strip4.png",
            "image/spr_coin_azu.png","image/spr_coin_ama.png","image/spr_coin_roj.png"
        };

        public Engine()
        {
            // ★ 세이브 파일 로드 (없으면 새로 생성)
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
            Raylib.InitWindow(800, 600, "ASDF SURVIVOR");
            Raylib.SetTargetFPS(60);

            Raylib.SetExitKey(KeyboardKey.Null);

            _texIdle      = Raylib.LoadTexture("image/idle.png");
            _texTitleIdle = Raylib.LoadTexture("image/ups_idle.png");
            _texWalk      = Raylib.LoadTexture("image/walk.png");
            _texEnemy     = Raylib.LoadTexture("image/Basic 1x.png");
            _texFloor     = Raylib.LoadTexture("image/floor.png");

            Raylib.SetTextureFilter(_texIdle,      TextureFilter.Point);
            Raylib.SetTextureFilter(_texWalk,      TextureFilter.Point);
            Raylib.SetTextureFilter(_texEnemy,     TextureFilter.Point);
            Raylib.SetTextureFilter(_texTitleIdle, TextureFilter.Bilinear);

            unsafe { _fontKR = Raylib.LoadFontEx("fonts/NanumGothic.ttf", 32, null, 65535); }
            Raylib.SetTextureFilter(_fontKR.Texture, TextureFilter.Bilinear);

            foreach (var f in _gemFileNames) _gemTextures.Add(Raylib.LoadTexture(f));

            while (!Raylib.WindowShouldClose()) { Update(Raylib.GetFrameTime()); Render(); }

            Raylib.UnloadTexture(_texIdle); Raylib.UnloadTexture(_texTitleIdle);
            Raylib.UnloadTexture(_texWalk); Raylib.UnloadTexture(_texEnemy);
            Raylib.UnloadTexture(_texFloor);
            Raylib.UnloadFont(_fontKR);
            foreach (var t in _gemTextures) Raylib.UnloadTexture(t);
            Raylib.CloseWindow();
        }

        private void Update(float dt)
        {
            if (_currentState == GameState.Title)
            {
                if (Raylib.IsKeyPressed(KeyboardKey.Enter)) StartGame();
                if (Raylib.IsKeyPressed(KeyboardKey.S)) { _shopCursor = 0; _currentState = GameState.Shop; }
                if (Raylib.IsKeyPressed(KeyboardKey.R)) { _recipePage = 0; _currentState = GameState.RecipeBook; }
                return;
            }

            // ── 타이틀 상점 ──
            if (_currentState == GameState.Shop)
            {
                var upgrades = MetaTable.All;
                if (Raylib.IsKeyPressed(KeyboardKey.Escape)) _currentState = GameState.Title;
                if (Raylib.IsKeyPressed(KeyboardKey.Up))   _shopCursor = (_shopCursor - 1 + upgrades.Count) % upgrades.Count;
                if (Raylib.IsKeyPressed(KeyboardKey.Down)) _shopCursor = (_shopCursor + 1) % upgrades.Count;
                if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Z))
                {
                    var def = upgrades[_shopCursor];
                    _save.BuyUpgrade(def.Type);
                    _save.Save();
                }
                return;
            }

            // ── 조합표 ──
            if (_currentState == GameState.RecipeBook)
            {
                if (Raylib.IsKeyPressed(KeyboardKey.Escape)) _currentState = GameState.Title;
                if (Raylib.IsKeyPressed(KeyboardKey.Left) || Raylib.IsKeyPressed(KeyboardKey.Right))
                    _recipePage = 1 - _recipePage;
                return;
            }
            if (_currentState == GameState.GameOver || _currentState == GameState.Victory)
            {
                if (Raylib.IsKeyPressed(KeyboardKey.R))
                {
                    // ★ 게임 종료 시 획득 골드를 영구 골드로 정산
                    _save.EarnGold(_player.Gold);
                    _save.Save();
                    _currentState = GameState.Title;
                }
                return;
            }
            if (_currentState == GameState.Pause)
            {
                // ESC를 다시 누르면 게임으로 복귀
                if (Raylib.IsKeyPressed(KeyboardKey.Escape)) _currentState = GameState.Playing;
                // Q를 누르면 게임 완전 종료
                if (Raylib.IsKeyPressed(KeyboardKey.Q)) Raylib.CloseWindow(); 
                return;
            }

            // ★ [신규 추가] 게임 중 ESC를 누르면 일시 정지
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
                    var card = _cardDeck.SelectCard(chosen);
                    if (card != null)
                    {
                        if (card.IsBonus)
                        {
                            // ★ 풀강 보너스 카드 즉시 적용
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

            // ★ 상자 보상 UI 확인 후 닫기
            if (_currentState == GameState.ChestReward)
            {
                _chestAnimTimer += dt; // ★ 타이머 증가
                // 연출이 2초 이상 진행된 후에만 엔터/스페이스로 닫을 수 있음 (뱀서식 딜레이)
                if (_chestAnimTimer > 2.0f && (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Space))) {
                    _currentState = GameState.Playing;
                }
                return;
            }

            _survivalTime += dt;

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
                // ★ 메타 부활 처리
                if (_player.ReviveCount > 0)
                {
                    _player.ReviveCount--;
                    _player.CurrentHP = _player.MaxHP * 0.30f;
                    _player.ShieldTimer = 3f; // 부활 후 3초 무적
                    _damageTexts.Add(new DamageText { Position = _player.Position, Damage = -(_player.MaxHP * 0.30f) });
                }
                else
                {
                    _player.CurrentHP = 0;
                    _currentState = GameState.GameOver;
                    return;
                }
            }

            // ── 5분: 잡몹 스폰 중지 + 최종 보스 소환 ──
            if (_survivalTime >= 300f && !_finalBossSpawned)
            {
                _finalBossSpawned = true;
                _enemies.RemoveAll(e => !e.IsBoss); // 잡몹만 제거
                _finalBoss = new Enemy
                {
                    Position  = new Vector2(_player.Position.X + 500, _player.Position.Y),
                    HP        = 8000f, Damage = 40f, Speed = 80f,
                    Scale     = 12f,
                    TintColor = new Color(255, 50, 50, 255),
                    IsBoss    = true
                };
                _enemies.Add(_finalBoss);
            }

            // 최종 보스 처치 → 승리
            if (_finalBossSpawned && (_finalBoss == null || _finalBoss.IsDead))
            { _currentState = GameState.Victory; return; }

            // 잡몹 스폰 (5분 이후엔 스폰 안 함)
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

            // ── 중간보스 스폰 (1:00 ~ 4:30) ──
            int bossSign = (_rand.Next(0,2)==0) ? 1 : -1;
            Vector2 BossPos() => new Vector2(
                _player.Position.X + bossSign * _rand.Next(420, 500),
                _player.Position.Y + _rand.Next(-80, 80));

            if (_survivalTime >= 60f  && !_midBoss1Spawned) { _midBoss1Spawned=true; _enemies.Add(new Enemy { Position=BossPos(), HP=400,  Damage=18, Speed=105, Scale=5f, TintColor=Color.Purple,                   IsBoss=true }); }
            if (_survivalTime >= 120f && !_midBoss2Spawned) { _midBoss2Spawned=true; _enemies.Add(new Enemy { Position=BossPos(), HP=700,  Damage=22, Speed=115, Scale=6f, TintColor=Color.DarkPurple,                IsBoss=true }); }
            if (_survivalTime >= 150f && !_midBoss3Spawned) { _midBoss3Spawned=true; _enemies.Add(new Enemy { Position=BossPos(), HP=1000, Damage=25, Speed=120, Scale=6f, TintColor=new Color(255,100,  0,255),      IsBoss=true }); }
            if (_survivalTime >= 180f && !_midBoss4Spawned) { _midBoss4Spawned=true; _enemies.Add(new Enemy { Position=BossPos(), HP=1400, Damage=28, Speed=125, Scale=7f, TintColor=new Color(200,  0,200,255),      IsBoss=true }); }
            if (_survivalTime >= 210f && !_midBoss5Spawned) { _midBoss5Spawned=true; _enemies.Add(new Enemy { Position=BossPos(), HP=1800, Damage=30, Speed=130, Scale=7f, TintColor=new Color(255, 50, 50,255),      IsBoss=true }); }
            if (_survivalTime >= 240f && !_midBoss6Spawned) { _midBoss6Spawned=true; _enemies.Add(new Enemy { Position=BossPos(), HP=2500, Damage=33, Speed=135, Scale=8f, TintColor=new Color( 50, 50,255,255),      IsBoss=true }); }
            if (_survivalTime >= 270f && !_midBoss7Spawned) { _midBoss7Spawned=true; _enemies.Add(new Enemy { Position=BossPos(), HP=3500, Damage=36, Speed=140, Scale=9f, TintColor=new Color(255,215,  0,255),      IsBoss=true }); }

            foreach (var e in _enemies) e.Update(dt, _player.Position);
            _weapon.Update(dt, _player, _enemies, _damageTexts);
            foreach (var t in _damageTexts) t.Update(dt);
            _damageTexts.RemoveAll(t => t.Timer >= t.Lifetime);

            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                if (_enemies[i].IsDead)
                {
                    // ★ 보스가 죽으면 무조건 상자 드랍
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

            // ★ 맵 곳곳에 상자가 랜덤 스폰되던 로직 완전 삭제 (보스만 상자 드랍)

            foreach (var chest in _mapChests)
            {
                chest.Update(dt);
                if (chest.State == ChestState.Closed &&
                    Vector2.Distance(_player.Position, chest.Position) < chest.TriggerRadius)
                {
                    chest.Open();
                    _chestRewards = _cardDeck.OpenChest(_weapon, _player);
                    _currentState = GameState.ChestReward; 
                    _chestAnimTimer = 0f; // ★ [신규 추가] 애니메이션 타이머 초기화
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

        private void ResumeGame() { _levelSystem.IsLevelUpReady = false; _currentState = GameState.Playing; }

        // ★ 보너스 카드 즉시 효과 적용 (풀강 레벨업 대체 카드)
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

        // ★ 게임 시작 (메타 업그레이드 스탯 적용)
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

            // ★ 메타 업그레이드 스탯 적용
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
            float bonusDmg  = def_dmg.TotalValue(lvDmg);   // AccDamageMult에 더함
            float bonusGold = def_gold.TotalValue(lvGold);
            _levelSystem.ExpMult = 1f + def_exp.TotalValue(lvExp);
            _player.ReviveCount  = (int)def_rev.TotalValue(lvRev);

            _player.MaxHP    += bonusHP;
            _player.CurrentHP = _player.MaxHP;
            _player.Speed    += bonusSpd;
            _weapon.AccDamageMult += bonusDmg;
            _player.Gold      = (int)bonusGold;

            _spawnTimer       = 0f;
            _survivalTime     = 0f;
            _chestAnimTimer   = 0f;

            _midBoss1Spawned = false; _midBoss2Spawned = false; _midBoss3Spawned = false;
            _midBoss4Spawned = false; _midBoss5Spawned = false; _midBoss6Spawned = false;
            _midBoss7Spawned = false;
            _finalBossSpawned = false; _finalBoss = null;

            _currentState = GameState.Playing;
        }

        private void Render()
        {
            // ★ 상점/조합표는 별도 렌더 함수에서 완전 독립 처리
            if (_currentState == GameState.Shop)       { RenderShop();       return; }
            if (_currentState == GameState.RecipeBook) { RenderRecipeBook(); return; }

            Raylib.BeginDrawing();

            if (_currentState == GameState.Title)
            {
                double gt = Raylib.GetTime();
                // ── 배경: 세로 그라디언트 ──
                for (int row = 0; row < 600; row++)
                {
                    float rf = row / 600f;
                    byte r = (byte)(5  + (int)(10  * rf));
                    byte g = (byte)(5  + (int)(8   * rf));
                    byte b = (byte)(18 + (int)(15  * rf));
                    Raylib.DrawLine(0, row, 800, row, new Color(r, g, b, (byte)255));
                }
                // ── 별 파티클 ──
                for (int s = 0; s < 90; s++)
                {
                    int   sx = (s * 131 + 53) % 800;
                    int   sy = (s * 197 + 29) % 540;
                    float tw = (float)(0.3 + 0.7 * Math.Abs(Math.Sin(gt * (0.5 + s * 0.04) + s)));
                    byte  sc = (byte)(int)(220 * tw);
                    Raylib.DrawCircle(sx, sy, (s % 4 == 0) ? 2 : 1, new Color(sc, sc, sc, sc));
                }
                // ── 캐릭터 (위아래 부유) ──
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
                // ── 제목 글로우 ──
                for (int g = 5; g >= 1; g--)
                    Raylib.DrawText("ASDF SURVIVOR", 130 - g, 45 - g, 64,
                        new Color((byte)255,(byte)180,(byte)0,(byte)(18 * g)));
                Raylib.DrawText("ASDF SURVIVOR", 130, 45, 64, Color.Gold);
                // 부제
                float sp = (float)(0.6 + 0.4 * Math.Sin(gt * 2.2));
                DrawTextKR("5분을 버텨라", 328, 118, 18,
                    new Color((byte)160,(byte)160,(byte)210,(byte)(int)(240*sp)));

                // ── 메뉴 버튼 ──
                (string icon, string label, string hint)[] menus = {
                    ("▶", "게  임  시  작", "ENTER"),
                    ("★", "상      점",     $"영구 골드  {_save.PermanentGold} G"),
                    ("◈", "조  합  표",     "진화 레시피"),
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
                    if (sel) Raylib.DrawRectangle(200, by2, 4, 46, menuAccent[i]); // 왼쪽 강조 바
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

            // ── 무기 범위 이펙트 (월드 공간) ──
            if (_weapon.HasGarlic)
            {
                int gr = (int)_weapon.GarlicRadius;
                Raylib.DrawCircle((int)_player.Position.X,(int)_player.Position.Y, gr, new Color(100,220,100,30));
                Raylib.DrawCircleLines((int)_player.Position.X,(int)_player.Position.Y, gr, new Color(120,255,120,80));
            }
            if (_weapon.HasHolyWater)
            {
                int hr = (int)(100f * _weapon.AccAreaMult);
                float hwp = (float)(0.5 + 0.5 * Math.Sin(Raylib.GetTime() * 3));
                Raylib.DrawCircle((int)_player.Position.X,(int)_player.Position.Y, hr, new Color((byte)220,(byte)220,(byte)80,(byte)(int)(25*hwp)));
                Raylib.DrawCircleLines((int)_player.Position.X,(int)_player.Position.Y, hr, new Color((byte)255,(byte)255,(byte)160,(byte)(int)(120*hwp)));
            }
            if (_weapon.HasOrbital)
            {
                int cnt = _weapon.OrbitalCount + _weapon.AccProjectileBonus;
                for (int i = 0; i < cnt; i++)
                {
                    float ang = _weapon.OrbitalAngle + i * ((float)Math.PI*2 / cnt);
                    int   ox  = (int)(_player.Position.X + Math.Cos(ang) * (_weapon.OrbitalRadius * _weapon.AccAreaMult));
                    int   oy  = (int)(_player.Position.Y + Math.Sin(ang) * (_weapon.OrbitalRadius * _weapon.AccAreaMult));
                    Raylib.DrawCircle(ox, oy, 10, new Color(40,200,255,220));
                    Raylib.DrawCircleLines(ox, oy, 12, new Color(100,230,255,120));
                }
            }
            if (_weapon.HasBlackHole)
            {
                float rad = 120f * _weapon.AccAreaMult;
                int   obc = 12 + _weapon.AccProjectileBonus * 2;
                Raylib.DrawCircle((int)_player.Position.X,(int)_player.Position.Y,(int)rad, new Color(20,0,60,35));
                for (int i = 0; i < obc; i++)
                {
                    float ang = _weapon.BlackHoleAngle + i * ((float)Math.PI*2 / obc);
                    Raylib.DrawCircle(
                        (int)(_player.Position.X + Math.Cos(ang)*rad),
                        (int)(_player.Position.Y + Math.Sin(ang)*rad),
                        9, new Color(160,40,255,230));
                    Raylib.DrawCircleLines(
                        (int)(_player.Position.X + Math.Cos(ang)*rad),
                        (int)(_player.Position.Y + Math.Sin(ang)*rad),
                        11, new Color(200,120,255,80));
                }
            }

            // ── 투사체 ──
            foreach (var p in _weapon.Projectiles)
            {
                if (p.IsPiercing)
                {
                    Raylib.DrawCircle((int)p.Position.X,(int)p.Position.Y, 8, new Color(255,140,30,230));
                    Raylib.DrawCircleLines((int)p.Position.X,(int)p.Position.Y, 10, new Color(255,200,80,120));
                }
                else
                {
                    Raylib.DrawCircle((int)p.Position.X,(int)p.Position.Y, 6, new Color(255,240,80,240));
                    Raylib.DrawCircleLines((int)p.Position.X,(int)p.Position.Y, 8, new Color(255,255,200,100));
                }
            }

            // ── 적 렌더링 + 보스 머리 위 HP바 ──
            foreach (var e in _enemies)
            {
                float spriteHalfH = 0f;
                if (_texEnemy.Width > 0)
                {
                    float fw = (float)_texEnemy.Width/5, fh = (float)_texEnemy.Height/3;
                    var src = new Rectangle(0,0,fw,fh);
                    var dst = new Rectangle(e.Position.X,e.Position.Y,fw*e.Scale,fh*e.Scale);
                    var org = new System.Numerics.Vector2(fw*e.Scale/2, fh*e.Scale/2);
                    Color col = (e.HitTimer>0) ? Color.White : e.TintColor;
                    // 피격 시 빨간 플래시
                    if (e.HitTimer > 0)
                        Raylib.DrawCircle((int)e.Position.X,(int)e.Position.Y,(int)(fw*e.Scale*0.4f), new Color(255,60,60,60));
                    Raylib.DrawTexturePro(_texEnemy,src,dst,org,0f,col);
                    spriteHalfH = fh * e.Scale / 2f;
                }
                else
                {
                    int er = e.IsBoss ? (int)(12*e.Scale/3f) : 10;
                    Raylib.DrawCircle((int)e.Position.X,(int)e.Position.Y, er, e.TintColor);
                    spriteHalfH = er;
                }

                // 보스 HP바 (머리 위)
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

            // ── 플레이어 ──
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

            // 방패
            if (_player.IsShielded)
            {
                float pulse = (float)(0.55 + 0.45*Math.Sin(Raylib.GetTime()*9));
                Raylib.DrawCircleLines((int)_player.Position.X,(int)_player.Position.Y,
                    30, new Color((byte)255,(byte)220,(byte)60,(byte)(int)(255*pulse)));
                Raylib.DrawCircle((int)_player.Position.X,(int)_player.Position.Y,
                    30, new Color((byte)255,(byte)220,(byte)60,(byte)(int)(40*pulse)));
            }

            // 플레이어 발 밑 HP바
            {
                float hpR2 = _player.CurrentHP / _player.MaxHP;
                int   pbx  = (int)_player.Position.X - 22;
                int   pby  = (int)_player.Position.Y + 28;
                Raylib.DrawRectangle(pbx-1, pby-1, 46, 8, new Color(0,0,0,160));
                Raylib.DrawRectangle(pbx, pby, 44, 6, new Color(40,0,0,200));
                Color phc = hpR2 > 0.5f ? new Color(60,200,60,255) : hpR2 > 0.25f ? new Color(220,160,0,255) : new Color(220,40,40,255);
                Raylib.DrawRectangle(pbx, pby, (int)(44*hpR2), 6, phc);
            }

            // ── 데미지 텍스트 ──
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
                // 그림자
                DrawTextKR(txt, (int)t.Position.X-9, (int)t.Position.Y-19, fs, new Color((byte)0,(byte)0,(byte)0,(byte)(alpha/2)));
                DrawTextKR(txt, (int)t.Position.X-10, (int)t.Position.Y-20, fs, dc);
            }

            Raylib.EndMode2D();

            // ══════════════ HUD ══════════════

            // ── EXP바 (최상단, 얇고 파란 글로우) ──
            float expR = (float)_levelSystem.CurrentExp / _levelSystem.MaxExp;
            Raylib.DrawRectangle(0, 0, 800, 6, new Color(10,10,30,220));
            Raylib.DrawRectangle(0, 0, (int)(800*expR), 6, new Color(60,120,255,255));
            Raylib.DrawRectangle(0, 5, (int)(800*expR), 2, new Color(160,200,255,140));

            // ── 상단 패널 배경 ──
            Raylib.DrawRectangle(0, 6, 800, 36, new Color(8,8,20,210));
            Raylib.DrawLine(0, 42, 800, 42, new Color(30,30,60,200));

            // ── 레벨 ──
            Raylib.DrawRectangle(6, 10, 60, 24, new Color(40,80,160,200));
            DrawTextKR($"Lv.{_levelSystem.Level}", 10, 13, 18, Color.White);

            // ── HP바 (좌측) ──
            float hpRhud = Math.Max(0, _player.CurrentHP / _player.MaxHP);
            Raylib.DrawRectangle(72, 12, 130, 16, new Color(20,0,0,200));
            Color hphud = hpRhud > 0.5f ? new Color(60,200,60,255) : hpRhud > 0.25f ? new Color(220,150,0,255) : new Color(220,40,40,255);
            Raylib.DrawRectangle(72, 12, (int)(130*hpRhud), 16, hphud);
            Raylib.DrawRectangleLines(72, 12, 130, 16, new Color(60,60,80,200));
            DrawTextKR($"{(int)_player.CurrentHP}/{(int)_player.MaxHP}", 75, 13, 13, Color.White);

            // ── 골드 ──
            Raylib.DrawRectangle(212, 10, 90, 24, new Color(50,40,0,180));
            DrawTextKR($"G {_player.Gold}", 218, 13, 16, Color.Gold);

            // ── 타이머 (중앙) ──
            int min=(int)_survivalTime/60, sec=(int)_survivalTime%60;
            string timeStr = $"{min:D2}:{sec:D2}";
            Raylib.DrawRectangle(340, 8, 120, 28, new Color(15,15,40,220));
            Raylib.DrawRectangleLines(340, 8, 120, 28, new Color(50,50,90,200));
            DrawTextKR(timeStr, 358, 12, 22, new Color(200,210,255,255));

            // ── 공격력 (우측) ──
            DrawTextKR($"ATK {_weapon.StaffDamage * _weapon.AccDamageMult:F0}", 668, 13, 15, new Color(255,180,80,220));

            // ── 최종보스 HP바 (하단 전용) ──
            if (_finalBossSpawned && _finalBoss != null && !_finalBoss.IsDead)
            {
                float bp = (float)(0.5 + 0.5*Math.Sin(Raylib.GetTime()*4));
                Color wc = new Color((byte)255,(byte)(int)(40+40*bp),(byte)0,(byte)255);
                DrawTextKR("⚠  FINAL BOSS", 318, 549, 18, wc);
                float br = Math.Max(0, _finalBoss.HP / _finalBoss.MaxHP);
                Raylib.DrawRectangle(60, 570, 680, 18, new Color(20,0,0,220));
                Raylib.DrawRectangle(60, 570, (int)(680*br), 18, new Color(200,20,20,255));
                // HP바 내부 구분선 (25% 단위)
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
                // 배경 그라디언트 오버레이
                for (int row2 = 0; row2 < 600; row2++)
                {
                    float rf = row2 / 600f;
                    byte ra = (byte)(int)(180 * (1 - rf * 0.3f));
                    Raylib.DrawLine(0, row2, 800, row2, new Color((byte)(int)(80*rf),(byte)0,(byte)0,ra));
                }
                // 중앙 패널
                Raylib.DrawRectangle(160, 130, 480, 320, new Color(10,0,0,230));
                Raylib.DrawRectangleLines(160, 130, 480, 320, new Color(180,0,0,255));
                Raylib.DrawRectangleLines(162, 132, 476, 316, new Color(80,0,0,200));
                // 제목 글로우
                for (int g = 4; g >= 1; g--)
                    Raylib.DrawText("YOU  DIED", 218-g, 158-g, 56, new Color((byte)180,(byte)0,(byte)0,(byte)(25*g)));
                Raylib.DrawText("YOU  DIED", 218, 158, 56, new Color(220,40,40,255));
                Raylib.DrawLine(180, 224, 620, 224, new Color(80,0,0,200));
                DrawTextKR($"생존 시간   {min:D2} : {sec:D2}", 280, 238, 18, new Color(180,120,120,255));
                DrawTextKR($"획득 골드   {_player.Gold} G", 295, 268, 18, Color.Gold);
                DrawTextKR("획득 골드는 영구 보관됩니다", 258, 296, 15, new Color(120,80,80,255));
                // 버튼
                Raylib.DrawRectangle(270, 358, 260, 44, new Color(140,20,20,230));
                Raylib.DrawRectangleLines(270, 358, 260, 44, new Color(220,60,60,255));
                DrawTextKR("R  —  타이틀로 돌아가기", 285, 370, 18, Color.White);
            }
            if (_currentState == GameState.Victory)
            {
                // 배경 그라디언트 오버레이 (금빛)
                for (int row2 = 0; row2 < 600; row2++)
                {
                    float rf = row2 / 600f;
                    byte ra = (byte)(int)(170 * (1 - rf * 0.2f));
                    Raylib.DrawLine(0, row2, 800, row2, new Color((byte)(int)(20+40*rf),(byte)(int)(30+60*rf),(byte)(int)(60+60*rf),ra));
                }
                Raylib.DrawRectangle(140, 110, 520, 360, new Color(5,10,30,235));
                Raylib.DrawRectangleLines(140, 110, 520, 360, Color.Gold);
                Raylib.DrawRectangleLines(142, 112, 516, 356, new Color(100,80,0,200));
                // 파티클 별
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
                DrawTextKR($"영구 골드 합계   {_save.PermanentGold + _player.Gold} G", 248, 316, 18, new Color(255,220,100,255));
                // 버튼
                Raylib.DrawRectangle(260, 376, 280, 44, new Color(30,70,20,230));
                Raylib.DrawRectangleLines(260, 376, 280, 44, Color.Gold);
                DrawTextKR("R  —  타이틀로 돌아가기", 278, 388, 18, Color.White);
            }

            // ★ 일시 정지 메뉴 렌더링
            if (_currentState == GameState.Pause) RenderPauseMenu();

            Raylib.EndDrawing();
        }

        private void RenderLevelUpCards()
        {
            // 배경 어둡게
            Raylib.DrawRectangle(0, 0, 800, 600, new Color(0,0,0,170));

            // 제목
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

                // ── 카드 그림자 ──
                Raylib.DrawRectangle(cx+6, cardY+6, cardW, cardH, new Color(0,0,0,100));

                // ── 카드 본체 ──
                Raylib.DrawRectangle(cx, cardY, cardW, cardH, card.CardColor);

                // ── 상단 색 띠 (악센트) ──
                Raylib.DrawRectangle(cx, cardY, cardW, 6, card.BorderColor);

                // ── 테두리 ──
                Raylib.DrawRectangleLines(cx, cardY, cardW, cardH, card.BorderColor);
                Raylib.DrawRectangleLines(cx+2, cardY+2, cardW-4, cardH-4,
                    new Color(card.BorderColor.R, card.BorderColor.G, card.BorderColor.B, (byte)50));

                // ── 아이콘 영역 ──
                int iconH = 68;
                Raylib.DrawRectangle(cx, cardY+6, cardW, iconH, new Color(0,0,0,50));
                DrawTextKR(card.Icon, cx + cardW/2 - 16, cardY + 18, 40, card.BorderColor);

                // ── NEW 배지 ──
                if (card.IsNewWeapon)
                {
                    Raylib.DrawRectangle(cx+8, cardY+iconH+10, cardW-16, 20, new Color(255,170,0,210));
                    DrawTextKR("NEW!", cx + cardW/2 - 18, cardY+iconH+12, 15, new Color(20,10,0,255));
                }

                // ── 타이틀 ──
                int titleY = card.IsNewWeapon ? cardY+iconH+34 : cardY+iconH+12;
                DrawTextKR(card.Title, cx+10, titleY, 16, Color.White);

                // ── 구분선 ──
                int divY2 = titleY + 24;
                Raylib.DrawLine(cx+10, divY2, cx+cardW-10, divY2,
                    new Color(card.BorderColor.R, card.BorderColor.G, card.BorderColor.B, (byte)80));

                // ── 설명 ──
                DrawWrappedTextKR(card.Description, cx+10, divY2+8, cardW-20, 14, new Color(195,195,205,255));

                // ── 스탯 미리보기 ──
                string statLine = GetStatPreview(card);
                if (statLine != "")
                {
                    int statY = cardY + cardH - 40;
                    Raylib.DrawRectangle(cx+6, statY-2, cardW-12, 22, new Color(0,0,0,90));
                    DrawTextKR(statLine, cx+10, statY+1, 13, new Color(255,225,90,255));
                }

                // ── 키 버튼 ──
                int btnY = cardY + cardH + 10;
                Raylib.DrawRectangle(cx + cardW/2 - 20, btnY, 40, 30, card.BorderColor);
                Raylib.DrawRectangleLines(cx + cardW/2 - 20, btnY, 40, 30, Color.White);
                DrawTextKR(keys[i], cx + cardW/2 - 6, btnY + 7, 18, Color.Black);
            }

            DrawTextKR("키보드  1 / 2 / 3  으로 선택", 284, 474, 17, new Color(130,130,160,255));
        }

        // ★ [신규] 상자 보상 UI 렌더링
        // ★ [신규] 상자 보상 화려한 뱀서식 UI 연출 렌더링
        // ★ [신규] 긴장감 넘치는 뱀서식 룰렛 상자 연출
        private void RenderChestReward()
        {
            Raylib.DrawRectangle(0, 0, 800, 600, new Color((byte)0, (byte)0, (byte)0, (byte)230));

            int cx = 400; 
            int cy = 400; 
            int count = _chestRewards.Count;
            float t = Math.Min(_chestAnimTimer, 1.0f); 
            float easeOut = 1f - (1f - t) * (1f - t);

            // ── [1] 스포트라이트 빛기둥 ──
            if (_chestAnimTimer > 0.2f)
            {
                float beamLength = 600f * easeOut;
                for (int i = 0; i < count; i++)
                {
                    float angle = -1.57f; 
                    if (count > 1) angle += -0.5f + (1.0f / (count - 1)) * i; 

                    Color beamColor = Color.SkyBlue; 
                    if (count == 3) 
                    {
                        if (i == 0) beamColor = new Color((byte)255, (byte)100, (byte)100, (byte)150); 
                        if (i == 1) beamColor = new Color((byte)255, (byte)200, (byte)50, (byte)150);  
                        if (i == 2) beamColor = new Color((byte)100, (byte)255, (byte)100, (byte)150); 
                    }
                    else if (count == 5) beamColor = new Color((byte)200, (byte)50, (byte)255, (byte)150); 

                    Vector2 top = new Vector2(cx + (float)Math.Cos(angle) * beamLength, cy + (float)Math.Sin(angle) * beamLength);
                    Raylib.DrawLineEx(new System.Numerics.Vector2(cx, cy), new System.Numerics.Vector2(top.X, top.Y), 70f * easeOut, beamColor);
                }
            }

            // ── [2] 쏟아지는 동전 파티클 ──
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

            // ── [3] 보물 상자 본체 ──
            float bounce = _chestAnimTimer < 0.5f ? (float)Math.Sin(_chestAnimTimer * 10f) * 10f : 0f;
            // 결과가 확정될 때마다(1.5, 2.3, 3.1...) 상자가 들썩이는 효과
            for (int i = 0; i < count; i++) {
                float lockTime = 1.5f + (i * 0.8f);
                if (_chestAnimTimer > lockTime && _chestAnimTimer < lockTime + 0.15f) bounce = -15f; 
            }

            int drawY = cy + (int)bounce;
            Raylib.DrawRectangle(cx - 50, drawY - 30, 100, 60, new Color((byte)139, (byte)90, (byte)43, (byte)255));
            Raylib.DrawRectangleLines(cx - 50, drawY - 30, 100, 60, new Color((byte)80, (byte)50, (byte)20, (byte)255));
            if (_chestAnimTimer > 0.3f) Raylib.DrawRectangle(cx - 50, drawY - 45, 100, 15, new Color((byte)160, (byte)110, (byte)55, (byte)255));
            Raylib.DrawRectangle(cx - 10, drawY - 10, 20, 20, new Color((byte)220, (byte)180, (byte)50, (byte)255));

            // ── [4] ★ 대망의 룰렛 애니메이션 ★ ──
            string[] dummyNames = { "지팡이", "마늘", "궤도구체", "도끼", "날개", "갑옷", "반지", "장갑", "마법진", "금화 주머니", "???" };

            if (_chestAnimTimer > 1.0f) // 상자가 완전히 열리고 1초 뒤부터 룰렛 시작
            {
                int spacing = 60; 
                int startY = 250 - (count * spacing / 2);

                for (int i = 0; i < count; i++)
                {
                    // 각 슬롯이 확정되는 시간 (1번: 1.5초, 2번: 2.3초, 3번: 3.1초 ...)
                    float lockTime = 1.5f + (i * 0.8f); 

                    if (_chestAnimTimer < lockTime)
                    {
                        // [룰렛이 돌아가는 중] - 텍스트가 미친듯이 바뀜
                        int randIdx = (int)(_chestAnimTimer * 30 + i * 7) % dummyNames.Length; // 프레임 단위로 인덱스 변경
                        string spinText = dummyNames[randIdx];

                        // 텍스트가 위아래로 미세하게 흔들리게 처리 (긴장감 부여)
                        int shakeY = (int)(Math.Sin(_chestAnimTimer * 50 + i) * 3);

                        Raylib.DrawRectangle(150, startY + (i * spacing) - 15, 500, 40, new Color((byte)0, (byte)0, (byte)0, (byte)150));
                        DrawTextKR(spinText, 260, startY + (i * spacing) + shakeY, 28, Color.Gray); // 회색으로 빠르게 돌아감
                    }
                    else
                    {
                        // [룰렛 확정 빡!]
                        float timeSinceLock = _chestAnimTimer - lockTime;
                        Color textColor = _chestRewards[i].Contains("진화") ? Color.Gold : Color.White;
                        
                        // 확정된 순간 0.1초 동안 하얗게 번쩍이는(Flash) 효과
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

            // ── [5] 닫기 버튼 (모든 연출이 끝난 후 등장) ──
            float exitTime = 1.5f + (count * 0.8f) + 0.5f;
            if (_chestAnimTimer > exitTime)
            {
                DrawTextKR("ENTER 키로 닫기", 320, 520, 20, Color.LightGray);
                if ((int)(Raylib.GetTime() * 4) % 2 == 0) 
                    Raylib.DrawRectangleLines(300, 505, 200, 40, Color.Gold);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // ★ 타이틀 상점 렌더
        // ─────────────────────────────────────────────────────────────
        private void RenderShop()
        {
            Raylib.BeginDrawing();
            // 배경
            for (int row2 = 0; row2 < 600; row2++)
            {
                float rf = row2/600f;
                Raylib.DrawLine(0, row2, 800, row2,
                    new Color((byte)(8+4*(int)rf),(byte)(8+4*(int)rf),(byte)(18+10*(int)rf),(byte)255));
            }

            // 제목 패널
            Raylib.DrawRectangle(0, 0, 800, 58, new Color(10,10,25,230));
            Raylib.DrawLine(0, 58, 800, 58, new Color(60,50,20,255));
            for (int g = 3; g >= 1; g--)
                Raylib.DrawText("SHOP", 330-g, 10-g, 40, new Color((byte)180,(byte)130,(byte)0,(byte)(25*g)));
            Raylib.DrawText("SHOP", 330, 10, 40, Color.Gold);
            Raylib.DrawRectangle(560, 14, 220, 30, new Color(40,34,0,200));
            Raylib.DrawRectangleLines(560, 14, 220, 30, new Color(100,80,0,200));
            DrawTextKR($"★  {_save.PermanentGold} G", 572, 18, 20, Color.Gold);

            var upgrades = MetaTable.All;
            int rowH = 74, startY = 72;
            for (int i = 0; i < upgrades.Count; i++)
            {
                var  def  = upgrades[i];
                int  lv   = _save.GetMetaLevel(def.Type);
                bool max  = lv >= def.MaxLevel;
                bool sel  = (i == _shopCursor);
                int  ry   = startY + i * rowH;

                // 행 배경
                Color bg = sel ? new Color(30,30,55,240) : new Color(14,14,28,210);
                Raylib.DrawRectangle(30, ry, 740, rowH-5, bg);
                Color bd = sel ? Color.Gold : new Color(40,40,65,200);
                Raylib.DrawRectangleLines(30, ry, 740, rowH-5, bd);
                if (sel) Raylib.DrawRectangle(30, ry, 4, rowH-5, Color.Gold);

                // 이름
                Color nc = max ? new Color(80,80,80,255) : (sel ? Color.White : new Color(200,200,210,255));
                DrawTextKR((max?"[MAX] ":"")+def.Name, 48, ry+8, 20, nc);
                DrawTextKR(def.Description, 48, ry+34, 14, new Color(130,130,150,255));

                // 레벨 칸
                DrawLevelSquares(440, ry+20, lv, def.MaxLevel);

                // 비용
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

                // 효과 미리보기
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

        // ─────────────────────────────────────────────────────────────
        // ★ 조합표 렌더
        // ─────────────────────────────────────────────────────────────
        private void RenderRecipeBook()
        {
            Raylib.BeginDrawing();
            for (int row2 = 0; row2 < 600; row2++)
            {
                float rf = row2/600f;
                Raylib.DrawLine(0,row2,800,row2,
                    new Color((byte)(8+4*(int)rf),(byte)(8+4*(int)rf),(byte)(20+12*(int)rf),(byte)255));
            }

            // 탭 헤더
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
            DrawTextKR("진화 조합",     505, 18, 22, t1?Color.Gold:new Color(100,100,130,255));

            if (_recipePage==0) RenderRecipeWeapons();
            else                RenderRecipeEvolution();

            DrawTextKR("← →  탭 전환      ESC  타이틀로", 258, 570, 16, new Color(60,60,90,255));
            Raylib.EndDrawing();
        }

        private void RenderRecipeWeapons()
        {
            (string name, string icon, string desc, string stats, Color ac)[] weapons = {
                ("지팡이",   "W", "가장 가까운 적에게 마법 투사체 발사", "Lv1: DMG15  |  Lv3: x2발  |  Lv5: DMG55 x3발", new Color(80,140,255,255)),
                ("마늘",     "G", "주변 적에게 지속 범위 피해 (범위 장신구 비적용)", "Lv1: R70  |  Lv3: R95  |  Lv5: DMG28 R130", new Color(100,220,100,255)),
                ("궤도구체", "O", "플레이어 주위를 선회하는 구체 (반지로 궤도 확장)", "Lv1: x2  |  Lv3: x3  |  Lv5: DMG55 x4", new Color(40,200,255,255)),
                ("도끼",     "A", "포물선 투척, 무한 관통으로 여러 적 타격", "Lv1: DMG25 x1  |  Lv3: x2  |  Lv5: DMG80 x3", new Color(255,140,40,255)),
            };
            (string name, string icon, string desc, string eff, Color ac)[] accs = {
                ("날개",  "↑", "투사체 속도→이동속도→투사체 개수 순 강화",   "Lv3/5: 투사체 +1/+2",      new Color(160,160,255,255)),
                ("갑옷",  "♥", "레벨업마다 최대 체력 증가+즉시 회복",         "Lv5 합계: 최대HP +130",    new Color(220,80,80,255)),
                ("반지",  "◎", "모든 무기 공격 범위 확대 (마늘 제외)",         "Lv5: 범위 +60%",           new Color(160,220,120,255)),
                ("장갑",  "✦", "모든 무기 데미지 영구 배율 증가",              "Lv5: 데미지 +60%",         new Color(255,200,60,255)),
            };

            int wy = 66, rh = 58;
            DrawTextKR("▶  무  기", 40, wy, 18, new Color(80,140,255,255));
            wy += 24;
            foreach (var w in weapons)
            {
                Raylib.DrawRectangle(28, wy, 744, rh-3, new Color(14,20,40,220));
                Raylib.DrawRectangleLines(28, wy, 744, rh-3, new Color(40,60,110,200));
                Raylib.DrawRectangle(28, wy, 4, rh-3, w.ac);
                Raylib.DrawRectangle(36, wy+8, 30, 30, new Color(w.ac.R,w.ac.G,w.ac.B,(byte)60));
                DrawTextKR(w.icon, 44, wy+10, 20, w.ac);
                DrawTextKR(w.name, 74, wy+6,  18, Color.White);
                DrawTextKR(w.desc, 74, wy+28, 13, new Color(140,140,160,255));
                DrawTextKR(w.stats, 370, wy+18, 13, new Color(255,210,80,255));
                wy += rh;
            }

            wy += 8;
            DrawTextKR("▶  장  신  구", 40, wy, 18, new Color(255,160,80,255));
            wy += 24;
            foreach (var a in accs)
            {
                Raylib.DrawRectangle(28, wy, 744, rh-3, new Color(30,18,14,220));
                Raylib.DrawRectangleLines(28, wy, 744, rh-3, new Color(90,50,30,200));
                Raylib.DrawRectangle(28, wy, 4, rh-3, a.ac);
                Raylib.DrawRectangle(36, wy+8, 30, 30, new Color(a.ac.R,a.ac.G,a.ac.B,(byte)50));
                DrawTextKR(a.icon, 44, wy+10, 20, a.ac);
                DrawTextKR(a.name, 74, wy+6,  18, Color.White);
                DrawTextKR(a.desc, 74, wy+28, 13, new Color(140,140,160,255));
                DrawTextKR(a.eff,  470,wy+18, 13, new Color(255,210,80,255));
                wy += rh;
            }
        }

        private void RenderRecipeEvolution()
        {
            DrawTextKR("무기 Lv.5  +  장신구 Lv.5  =  진화 무기", 168, 68, 18, new Color(220,200,80,255));
            DrawTextKR("보스 처치 보물상자에서 자동으로 진화됩니다", 155, 94, 15, new Color(110,110,140,255));

            (string w, string a, string r, string d, Color c)[] evos = {
                ("지팡이 Lv.5","날개 Lv.5",  "마법진",   "전방 3방향 무한 관통빔. 냉혹한 DPS형.",         new Color(80,140,255,255)),
                ("마늘 Lv.5",  "갑옷 Lv.5",  "성수",     "광역 폭발 + 피흡. 싸울수록 체력 회복.",         new Color(80,220,120,255)),
                ("궤도구체Lv.5","반지 Lv.5", "블랙홀",   "12개 구체 고속 회전 + 적 흡입. 범위 최강.",     new Color(180,80,255,255)),
                ("도끼 Lv.5",  "장갑 Lv.5",  "도끼폭풍", "8방향 전방위 도끼 투척. 무한 관통 광역 딜.",    new Color(255,140,40,255)),
            };

            int ey = 122, rh = 96;
            foreach (var evo in evos)
            {
                Raylib.DrawRectangle(28, ey, 744, rh-6, new Color(12,12,28,230));
                Raylib.DrawRectangleLines(28, ey, 744, rh-6, new Color(evo.c.R/2,evo.c.G/2,evo.c.B/2,(byte)200));
                Raylib.DrawRectangle(28, ey, 4, rh-6, evo.c);

                // 재료 박스
                Raylib.DrawRectangle(44, ey+12, 180, 44, new Color(evo.c.R,evo.c.G,evo.c.B,(byte)20));
                Raylib.DrawRectangleLines(44, ey+12, 180, 44, new Color(evo.c.R,evo.c.G,evo.c.B,(byte)80));
                DrawTextKR(evo.w, 52, ey+14, 15, Color.LightGray);

                DrawTextKR("+", 234, ey+22, 24, new Color(150,150,150,255));

                Raylib.DrawRectangle(258, ey+12, 165, 44, new Color(evo.c.R,evo.c.G,evo.c.B,(byte)20));
                Raylib.DrawRectangleLines(258, ey+12, 165, 44, new Color(evo.c.R,evo.c.G,evo.c.B,(byte)80));
                DrawTextKR(evo.a, 266, ey+14, 15, Color.LightGray);

                DrawTextKR("=", 432, ey+22, 24, new Color(150,150,150,255));

                // 결과
                Raylib.DrawRectangle(456, ey+10, 300, 50, new Color(evo.c.R,evo.c.G,evo.c.B,(byte)25));
                DrawTextKR("★  "+evo.r, 466, ey+14, 22, evo.c);
                DrawTextKR(evo.d,        466, ey+44, 14, new Color(160,160,170,255));

                ey += rh;
            }
            DrawTextKR("※ 진화 후 원본 무기는 슬롯에서 제거됩니다", 50, ey+6, 14, new Color(80,80,100,255));
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
                    case AccessoryType.Wings: 
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

        // ─────────────────────────────────────────────────────────────
        // ★ [신규 추가] ESC 일시 정지 메뉴 화면
        // ─────────────────────────────────────────────────────────────
        private void RenderPauseMenu()
        {
            Raylib.DrawRectangle(0, 0, 800, 600, new Color(0,0,0,200));
            // 중앙 패널
            Raylib.DrawRectangle(30, 20, 740, 556, new Color(10,10,24,240));
            Raylib.DrawRectangleLines(30, 20, 740, 556, new Color(50,50,90,255));
            Raylib.DrawRectangle(30, 20, 740, 4, new Color(80,120,255,200));

            for (int g=3;g>=1;g--)
                Raylib.DrawText("PAUSE", 328-g, 34-g, 38, new Color((byte)60,(byte)80,(byte)160,(byte)(30*g)));
            Raylib.DrawText("PAUSE", 328, 34, 38, new Color(140,160,255,255));
            Raylib.DrawLine(50, 82, 750, 82, new Color(40,40,70,200));

            // ── 왼쪽: 현재 스펙 ──
            Raylib.DrawRectangle(40, 92, 340, 400, new Color(14,14,30,220));
            Raylib.DrawRectangleLines(40, 92, 340, 400, new Color(40,40,70,200));
            Raylib.DrawRectangle(40, 92, 340, 4, new Color(80,120,255,180));
            DrawTextKR("현재 스펙", 158, 100, 20, new Color(160,180,255,255));
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

            // ── 오른쪽: 보유 장비 ──
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
                    DrawLevelSquares(620, ey2+3, w.Value, 5);
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
            DrawTextKR("ESC  게임으로 돌아가기       Q  게임 종료", 200, 510, 18, new Color(80,80,110,255));
        }

        // ★ 아이템 강화 수치를 뱀서식 네모 칸으로 렌더링
        private void DrawLevelSquares(int x, int y, int level, int maxLevel)
        {
            for (int i = 0; i < maxLevel; i++)
            {
                int sx = x + i * 18;
                if (i < level)
                {
                    Raylib.DrawRectangle(sx, y, 14, 14, Color.Gold);
                    Raylib.DrawRectangle(sx, y, 14, 3, new Color(255,240,160,180)); // 하이라이트
                }
                else
                {
                    Raylib.DrawRectangle(sx, y, 14, 14, new Color(20,20,35,200));
                    Raylib.DrawRectangleLines(sx, y, 14, 14, new Color(50,50,70,200));
                }
            }
        }

        // UI용 이름 변환기
        private string GetWeaponNameUI(WeaponType t) => t switch { WeaponType.Staff => "지팡이", WeaponType.Garlic => "마늘", WeaponType.Orbital => "궤도구체", WeaponType.Axe => "도끼", WeaponType.MagicCircle => "마법진 (진화)", WeaponType.HolyWater => "성수 (진화)", WeaponType.BlackHole => "블랙홀 (진화)", WeaponType.AxeStorm => "도끼폭풍 (진화)", _ => "???" };
private string GetAccNameUI(AccessoryType t) => t switch { AccessoryType.Wings => "날개", AccessoryType.Armor => "갑옷", AccessoryType.Ring => "반지", AccessoryType.Glove => "장갑", _ => "???" };
    }
}