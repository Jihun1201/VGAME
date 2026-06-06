// 파일명: GameCore.cs
using System;
using System.Collections.Generic;
using Raylib_cs;
using EntityGroup;
using CombatSystem;
using UpgradeLogic;
using WeaponData;
using FieldItem;

namespace GameCore
{
    public struct Vector2 { public float X; public float Y; public Vector2(float x, float y) { X = x; Y = y; } public static float Distance(Vector2 a, Vector2 b) { float dx = a.X - b.X; float dy = a.Y - b.Y; return (float)Math.Sqrt(dx * dx + dy * dy); } }

    // ★ ChestReward (상자 보상 연출 화면) 추가
    public enum GameState { Title, Playing, LevelUp, ChestReward, Pause, GameOver, Victory }

    public class Engine
    {
        private Player        _player;
        private List<Enemy>   _enemies;
        private Weapon        _weapon;
        private List<ExpGem>  _gems;
        private LevelSystem   _levelSystem;
        private CardDeck      _cardDeck;        

        private List<DropItem> _dropItems  = new List<DropItem>();
        private List<MapChest> _mapChests  = new List<MapChest>();
        
        // ★ 상자 보상 메시지를 담아둘 리스트
        private List<string> _chestRewards = new List<string>();

        private float _chestAnimTimer = 0f;

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
                if (Raylib.IsKeyPressed(KeyboardKey.Enter)) _currentState = GameState.Playing;
                return;
            }
            if (_currentState == GameState.GameOver || _currentState == GameState.Victory)
            {
                if (Raylib.IsKeyPressed(KeyboardKey.R)) RestartGame();
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
                        if (card.CardType == CardType.Weapon) _weapon.ApplyLevel(card.WeaponType, card.NextLevel);
                        else _weapon.ApplyAccessory(card.AccessoryType, card.NextLevel, _player);
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
            if (_player.IsDead) { _player.CurrentHP = 0; _currentState = GameState.GameOver; return; }

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

        private void RestartGame()
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
            Raylib.BeginDrawing();

            if (_currentState == GameState.Title)
            {
                Raylib.ClearBackground(new Color(20, 20, 35, 255));
                if (_texTitleIdle.Width > 0)
                {
                    int   frame     = (int)(Raylib.GetTime() * 10) % 10;
                    float frameW    = _texTitleIdle.Width / 10f;
                    float frameH    = _texTitleIdle.Height;
                    var   src       = new Rectangle(frame * frameW, 0, frameW, frameH);
                    float scale     = 1.5f;
                    var   origin    = new System.Numerics.Vector2((frameW*scale)/2, (frameH*scale)/2);
                    Raylib.DrawTexturePro(_texTitleIdle, src, new Rectangle(400+15,300+15,frameW*scale,frameH*scale), origin, 0f, new Color(0,0,0,150));
                    Raylib.DrawTexturePro(_texTitleIdle, src, new Rectangle(400,300,frameW*scale,frameH*scale),       origin, 0f, Color.White);
                }
                Raylib.DrawText("ASDF SURVIVOR", 140, 50, 60, Color.Gold);
                if ((int)(Raylib.GetTime()*2)%2==0) Raylib.DrawText("- Press ENTER to Start -", 220, 500, 28, Color.White);
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

            // 진화 무기 및 기본 무기 이펙트 렌더링
            if (_weapon.HasGarlic)
                Raylib.DrawCircle((int)_player.Position.X,(int)_player.Position.Y,(int)_weapon.GarlicRadius,new Color(150,255,150,80));
            if (_weapon.HasHolyWater)
                Raylib.DrawCircle((int)_player.Position.X,(int)_player.Position.Y,(int)(100f * _weapon.AccAreaMult),new Color(255,255,200,60));
            if (_weapon.HasOrbital)
                for (int i=0;i<_weapon.OrbitalCount + _weapon.AccProjectileBonus;i++)
                {
                    float ang = _weapon.OrbitalAngle + (i*((float)Math.PI*2/(_weapon.OrbitalCount + _weapon.AccProjectileBonus)));
                    Raylib.DrawCircle(
                        (int)(_player.Position.X+Math.Cos(ang)*(_weapon.OrbitalRadius*_weapon.AccAreaMult)),
                        (int)(_player.Position.Y+Math.Sin(ang)*(_weapon.OrbitalRadius*_weapon.AccAreaMult)),
                        8, new Color(0,255,255,255));
                }
            if (_weapon.HasBlackHole)
            {
                float rad = 120f * _weapon.AccAreaMult;
                int orbCount = 12 + _weapon.AccProjectileBonus * 2;
                for (int i = 0; i < orbCount; i++)
                {
                    float ang = _weapon.BlackHoleAngle + (i * ((float)Math.PI * 2 / orbCount));
                    Raylib.DrawCircle(
                        (int)(_player.Position.X + Math.Cos(ang) * rad),
                        (int)(_player.Position.Y + Math.Sin(ang) * rad),
                        10, new Color(100, 0, 200, 230));
                }
                // 중심부 흡입 이펙트 (반투명 원)
                Raylib.DrawCircle((int)_player.Position.X, (int)_player.Position.Y,
                    (int)rad, new Color(30, 0, 80, 40));
            }

            foreach (var p in _weapon.Projectiles)
                Raylib.DrawCircle((int)p.Position.X,(int)p.Position.Y, p.IsPiercing ? 8 : 5, p.IsPiercing ? Color.Orange : Color.Yellow);

            foreach (var e in _enemies)
            {
                if (_texEnemy.Width>0)
                {
                    float fw=(float)_texEnemy.Width/5, fh=(float)_texEnemy.Height/3;
                    var src = new Rectangle(0,0,fw,fh);
                    var dst = new Rectangle(e.Position.X,e.Position.Y,fw*e.Scale,fh*e.Scale);
                    var org = new System.Numerics.Vector2((fw*e.Scale)/2,(fh*e.Scale)/2);
                    Color col = (e.HitTimer>0) ? Color.Red : e.TintColor;
                    Raylib.DrawTexturePro(_texEnemy,src,dst,org,0f,col);
                }
                else Raylib.DrawRectangle((int)e.Position.X-10,(int)e.Position.Y-10,20,20,Color.Red);
            }

            if (_texIdle.Width>0 && _texWalk.Width>0)
            {
                Texture2D ct  = _player.IsMoving ? _texWalk : _texIdle;
                int maxF = _player.IsMoving ? 24 : 10;
                int cols = _player.IsMoving ?  4 : 10;
                int rows = _player.IsMoving ?  6 :  1;
                int fn   = _player.CurrentFrame % maxF;
                float fw = (float)ct.Width/cols, fh=(float)ct.Height/rows;
                float sx = (fn%cols)*fw, sy=(fn/cols)*fh;
                float rw = _player.IsFacingLeft ? fw : -fw;
                var src = new Rectangle(sx,sy,rw,fh);
                var dst = new Rectangle(_player.Position.X,_player.Position.Y,fw*1.5f,fh*1.5f);
                var org = new System.Numerics.Vector2((fw*1.5f)/2,(fh*1.5f)/2);
                Color pc = (_player.IsDead||_player.HitTimer>0) ? Color.Red : Color.White;
                Raylib.DrawTexturePro(ct,src,dst,org,0f,pc);
            }
            else Raylib.DrawCircle((int)_player.Position.X,(int)_player.Position.Y,15,Color.Blue);

            if (_player.IsShielded)
            {
                float pulse = (float)(0.6 + 0.4 * Math.Sin(Raylib.GetTime() * 8));
                Raylib.DrawCircle((int)_player.Position.X, (int)_player.Position.Y,
                    28, new Color((byte)255, (byte)220, (byte)50, (byte)(int)(80 * pulse)));
                Raylib.DrawCircleLines((int)_player.Position.X, (int)_player.Position.Y,
                    28, new Color((byte)255, (byte)220, (byte)50, (byte)(int)(200 * pulse)));
            }

            float hpR = _player.CurrentHP/_player.MaxHP;
            int bx=(int)_player.Position.X-20, by=(int)_player.Position.Y+50;
            Raylib.DrawRectangle(bx,by,40,6,Color.DarkGray);
            Raylib.DrawRectangle(bx,by,(int)(40*hpR),6,Color.Red);

            foreach (var t in _damageTexts)
            {
                bool isHeal = t.Damage < 0;
                string txt  = isHeal ? $"+{(-t.Damage):F0}" : t.Damage.ToString("F0");
                Color  col  = isHeal ? Color.Green : Color.Yellow;
                Raylib.DrawText(txt, (int)t.Position.X-10, (int)t.Position.Y-20, 18, col);
            }

            Raylib.EndMode2D();

            float expR = (float)_levelSystem.CurrentExp/_levelSystem.MaxExp;
            Raylib.DrawRectangle(0,0,800,20,Color.Black);
            Raylib.DrawRectangle(0,0,(int)(800*expR),20,Color.Blue);
            Raylib.DrawText($"LV: {_levelSystem.Level}", 10,25,20,Color.White);
            Raylib.DrawText($"ATK: {_weapon.StaffDamage * _weapon.AccDamageMult:F0}", 10, 50, 15, Color.LightGray);
            Raylib.DrawText($"GOLD: {_player.Gold}", 650,25,20,Color.Gold);
            int min=(int)_survivalTime/60, sec=(int)_survivalTime%60;
            Raylib.DrawText($"{min:D2}:{sec:D2}", 360,25,28,Color.White);

            // 최종 보스 전투 중 경고 표시
            if (_finalBossSpawned && _finalBoss != null && !_finalBoss.IsDead)
            {
                float pulse = (float)(0.5 + 0.5 * Math.Sin(Raylib.GetTime() * 4));
                Color warnColor = new Color((byte)255, (byte)(int)(50*pulse), (byte)0, (byte)255);
                DrawTextKR("⚠ 최종 보스 출현!", 280, 50, 26, warnColor);
                // 최종 보스 HP바
                float bossHpR = Math.Max(0, _finalBoss.HP / 8000f);
                Raylib.DrawRectangle(100, 575, 600, 16, new Color(60, 0, 0, 200));
                Raylib.DrawRectangle(100, 575, (int)(600*bossHpR), 16, new Color(220, 30, 30, 255));
                Raylib.DrawRectangleLines(100, 575, 600, 16, Color.Red);
                DrawTextKR("FINAL BOSS", 360, 555, 16, Color.Red);
            }

            if (_currentState == GameState.LevelUp) RenderLevelUpCards();
            if (_currentState == GameState.ChestReward) RenderChestReward();

            if (_currentState == GameState.GameOver)
            {
                Raylib.DrawRectangle(0,0,800,600,new Color(150,0,0,200));
                Raylib.DrawText("YOU DIED",  260,200,60,Color.Red);
                Raylib.DrawText("Game Over", 340,280,24,Color.LightGray);
                // 재시작 버튼
                Raylib.DrawRectangle(280, 360, 240, 50, new Color(200, 50, 50, 220));
                Raylib.DrawRectangleLines(280, 360, 240, 50, Color.White);
                DrawTextKR("[ R ] 다시 시작", 308, 374, 22, Color.White);
            }
            if (_currentState == GameState.Victory)
            {
                Raylib.DrawRectangle(0,0,800,600,new Color(0,100,255,200));
                Raylib.DrawText("VICTORY!", 260,180,60,Color.Gold);
                DrawTextKR($"5분 생존 성공!  골드: {_player.Gold}", 200,270,26,Color.White);
                DrawTextKR("최종 보스를 처치했습니다!", 230,310,22,new Color(255,215,0,255));
                // 재시작 버튼
                Raylib.DrawRectangle(280, 380, 240, 50, new Color(50, 150, 50, 220));
                Raylib.DrawRectangleLines(280, 380, 240, 50, Color.Gold);
                DrawTextKR("[ R ] 다시 시작", 308, 394, 22, Color.White);
            }

            // ★ [신규 추가] 일시 정지 메뉴 렌더링
            if (_currentState == GameState.Pause) RenderPauseMenu();

            Raylib.EndDrawing();
        }

        private void RenderLevelUpCards()
        {
            Raylib.DrawRectangle(0, 0, 800, 600, new Color(0, 0, 0, 160));
            Raylib.DrawText("LEVEL  UP", 290, 60, 42, Color.Gold);
            DrawTextKR("업그레이드를 선택하세요", 270, 112, 20, new Color(200, 200, 200, 255));

            var cards = _cardDeck.CurrentCards;
            int cardCount = cards.Count;
            if (cardCount == 0) return;

            int cardW   = 180, cardH   = 240, spacing = 20;
            int totalW  = cardCount * cardW + (cardCount - 1) * spacing;
            int startX  = (800 - totalW) / 2;
            int cardY   = 160;
            string[] keys = { "1", "2", "3" };

            for (int i = 0; i < cardCount; i++)
            {
                var card = cards[i];
                int cx   = startX + i * (cardW + spacing);

                Raylib.DrawRectangle(cx+5, cardY+5, cardW, cardH, new Color(0,0,0,120));
                Raylib.DrawRectangle(cx, cardY, cardW, cardH, card.CardColor);
                Raylib.DrawRectangleLines(cx, cardY, cardW, cardH, card.BorderColor);
                Raylib.DrawRectangleLines(cx+2, cardY+2, cardW-4, cardH-4, new Color(card.BorderColor.R, card.BorderColor.G, card.BorderColor.B, (byte)80));

                int iconAreaH = 70;
                Raylib.DrawRectangle(cx, cardY, cardW, iconAreaH, new Color(0,0,0,60));
                Raylib.DrawText(card.Icon, cx + cardW/2 - 14, cardY + 14, 42, card.BorderColor);

                if (card.IsNewWeapon)
                {
                    Raylib.DrawRectangle(cx+8, cardY+iconAreaH+8, cardW-16, 18, new Color(255,180,0,200));
                    Raylib.DrawText("NEW!", cx+cardW/2-16, cardY+iconAreaH+10, 14, Color.Black);
                }

                int titleY = card.IsNewWeapon ? cardY+iconAreaH+30 : cardY+iconAreaH+10;
                DrawTextKR(card.Title, cx+10, titleY, 17, Color.White);

                int divY = titleY + 26;
                Raylib.DrawLine(cx+10, divY, cx+cardW-10, divY, new Color(card.BorderColor.R, card.BorderColor.G, card.BorderColor.B, (byte)120));
                DrawWrappedTextKR(card.Description, cx+10, divY+8, cardW-20, 15, new Color(210,210,210,255));

                string statLine = GetStatPreview(card);
                if (statLine != "")
                {
                    int statY = cardY + cardH - 38;
                    Raylib.DrawRectangle(cx+8, statY-4, cardW-16, 20, new Color(0,0,0,80));
                    
                    // ★ Raylib.DrawText를 DrawTextKR로 변경합니다.
                    DrawTextKR(statLine, cx+12, statY, 13, new Color(255,230,100,255)); 
                }

                int btnY = cardY + cardH + 8;
                Raylib.DrawRectangle(cx + cardW/2 - 18, btnY, 36, 28, card.BorderColor);
                Raylib.DrawText($"[{keys[i]}]", cx + cardW/2 - 10, btnY + 6, 18, Color.Black);
            }

            DrawTextKR("1 / 2 / 3  키로 선택", 290, 460, 18, new Color(160,160,160,255));
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
            // 화면 전체 반투명 처리
            Raylib.DrawRectangle(0, 0, 800, 600, new Color(0, 0, 0, 220));
            DrawTextKR("일시 정지", 330, 40, 40, Color.Gold);

            // ── [1] 플레이어 현재 스펙 (왼쪽 패널) ──
            int statX = 50, statY = 120;
            Raylib.DrawRectangle(statX, statY, 300, 400, new Color(30, 30, 40, 240));
            Raylib.DrawRectangleLines(statX, statY, 300, 400, Color.LightGray);
            DrawTextKR("현재 스펙", statX + 100, statY + 20, 24, Color.White);
            Raylib.DrawLine(statX + 20, statY + 60, statX + 280, statY + 60, Color.Gray);

            int sy = statY + 80;
            DrawTextKR($"최대 체력: {_player.MaxHP:F0}", statX + 30, sy, 20, Color.LightGray); sy += 40;
            DrawTextKR($"이동 속도: {_player.Speed:F0}", statX + 30, sy, 20, Color.LightGray); sy += 40;
            DrawTextKR($"피해량: +{(_weapon.AccDamageMult - 1f) * 100:F0}%", statX + 30, sy, 20, Color.LightGray); sy += 40;
            DrawTextKR($"공격 범위: +{(_weapon.AccAreaMult - 1f) * 100:F0}%", statX + 30, sy, 20, Color.LightGray); sy += 40;
            DrawTextKR($"투사체 추가: +{_weapon.AccProjectileBonus}개", statX + 30, sy, 20, Color.LightGray); sy += 40;
            DrawTextKR($"부활: 0회 (미구현)", statX + 30, sy, 20, Color.DarkGray);

            // ── [2] 획득한 장비 현황 (오른쪽 패널) ──
            int eqX = 400, eqY = 120;
            Raylib.DrawRectangle(eqX, eqY, 350, 400, new Color(30, 30, 40, 240));
            Raylib.DrawRectangleLines(eqX, eqY, 350, 400, Color.LightGray);
            DrawTextKR("보유 장비", eqX + 130, eqY + 20, 24, Color.White);
            Raylib.DrawLine(eqX + 20, eqY + 60, eqX + 330, eqY + 60, Color.Gray);

            int ey = eqY + 80;
            
            // 무기 리스트 출력 (파란색)
            foreach (var w in _cardDeck.WeaponLevels)
            {
                if (w.Value > 0)
                {
                    DrawTextKR(GetWeaponNameUI(w.Key), eqX + 30, ey, 20, new Color(80, 140, 255, 255));
                    DrawLevelSquares(eqX + 180, ey + 4, w.Value, 5); // 5칸 네모 그리기
                    ey += 35;
                }
            }
            
            // 장신구 리스트 출력 (주황색)
            foreach (var a in _cardDeck.AccessoryLevels)
            {
                if (a.Value > 0)
                {
                    DrawTextKR(GetAccNameUI(a.Key), eqX + 30, ey, 20, new Color(255, 180, 80, 255));
                    DrawLevelSquares(eqX + 180, ey + 4, a.Value, 5); // 5칸 네모 그리기
                    ey += 35;
                }
            }

            // ── [3] 하단 안내 문구 ──
            DrawTextKR("ESC: 게임으로 돌아가기   /   Q: 게임 종료", 220, 540, 20, Color.Gray);
        }

        // ★ 아이템 강화 수치를 뱀서식 네모 칸으로 렌더링
        private void DrawLevelSquares(int x, int y, int level, int maxLevel)
        {
            for (int i = 0; i < maxLevel; i++)
            {
                if (i < level) 
                    Raylib.DrawRectangle(x + (i * 20), y, 14, 14, Color.Gold); // 꽉 찬 금색 칸
                else 
                    Raylib.DrawRectangleLines(x + (i * 20), y, 14, 14, Color.DarkGray); // 빈 회색 테두리 칸
            }
        }

        // UI용 이름 변환기
        private string GetWeaponNameUI(WeaponType t) => t switch { WeaponType.Staff => "지팡이", WeaponType.Garlic => "마늘", WeaponType.Orbital => "궤도구체", WeaponType.Axe => "도끼", WeaponType.MagicCircle => "마법진 (진화)", WeaponType.HolyWater => "성수 (진화)", WeaponType.BlackHole => "블랙홀 (진화)", WeaponType.AxeStorm => "도끼폭풍 (진화)", _ => "???" };
private string GetAccNameUI(AccessoryType t) => t switch { AccessoryType.Wings => "날개", AccessoryType.Armor => "갑옷", AccessoryType.Ring => "반지", AccessoryType.Glove => "장갑", _ => "???" };
    }
}