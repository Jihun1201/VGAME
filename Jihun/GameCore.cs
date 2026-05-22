// 파일명: GameCore.cs
using System;
using System.Collections.Generic;
using Raylib_cs;
using EntityGroup;
using CombatSystem;
using UpgradeLogic;

namespace GameCore
{
    public struct Vector2 { public float X; public float Y; public Vector2(float x, float y) { X = x; Y = y; } public static float Distance(Vector2 a, Vector2 b) { float dx = a.X - b.X; float dy = a.Y - b.Y; return (float)Math.Sqrt(dx * dx + dy * dy); } }
    public enum GameState { Playing, LevelUp, GameOver }

    public class Engine
    {
        private Player _player;
        private List<Enemy> _enemies;
        private Weapon _weapon;
        private List<ExpGem> _gems;
        private LevelSystem _levelSystem;
        private GameState _currentState = GameState.Playing;
        private float _spawnTimer = 0f;

        private List<DamageText> _damageTexts;
        private Camera2D _camera;
        private Texture2D _texIdle, _texWalk, _texEnemy, _texFloor;
        private List<Texture2D> _gemTextures = new List<Texture2D>();
        
        private string[] _gemFileNames = { 
            "image/MonedaP.png", "image/MonedaD.png", "image/MonedaR.png", 
            "image/spr_coin_gri.png", "image/spr_coin_strip4.png", "image/spr_coin_azu.png", 
            "image/spr_coin_ama.png", "image/spr_coin_roj.png" 
        };

        public Engine()
        {
            _player = new Player { Position = new Vector2(400, 300) };
            _enemies = new List<Enemy>();
            _weapon = new Weapon();
            _gems = new List<ExpGem>();
            _levelSystem = new LevelSystem();
            _damageTexts = new List<DamageText>();
            _camera = new Camera2D(); _camera.Offset = new System.Numerics.Vector2(800f / 2f, 600f / 2f); _camera.Zoom = 1.0f;
        }

        public void Run()
        {
            Raylib.InitWindow(800, 600, "Vampire Survivor - Floating HP Bar");
            Raylib.SetTargetFPS(60);
            _texIdle = Raylib.LoadTexture("image/idle.png"); _texWalk = Raylib.LoadTexture("image/walk.png");
            _texEnemy = Raylib.LoadTexture("image/Basic 1x.png"); _texFloor = Raylib.LoadTexture("image/floor.png"); 
            foreach (var fileName in _gemFileNames) _gemTextures.Add(Raylib.LoadTexture(fileName));

            while (!Raylib.WindowShouldClose())
            {
                Update(Raylib.GetFrameTime()); Render(); 
            }

            Raylib.UnloadTexture(_texIdle); Raylib.UnloadTexture(_texWalk); Raylib.UnloadTexture(_texEnemy); Raylib.UnloadTexture(_texFloor);
            foreach (var tex in _gemTextures) Raylib.UnloadTexture(tex);
            Raylib.CloseWindow();
        }

        private void Update(float dt)
        {
            if (_currentState == GameState.GameOver) return;
            if (_currentState == GameState.LevelUp)
            {
                if (Raylib.IsKeyPressed(KeyboardKey.One)) { _player.Speed += 30f; ResumeGame(); }
                else if (Raylib.IsKeyPressed(KeyboardKey.Two)) { _weapon.Damage += 5; ResumeGame(); }
                else if (Raylib.IsKeyPressed(KeyboardKey.Three)) { _weapon.FireCooldown *= 0.8f; ResumeGame(); }
                return; 
            }

            _player.Update(dt);
            _camera.Target = new System.Numerics.Vector2(_player.Position.X, _player.Position.Y);

            foreach (var enemy in _enemies)
            {
                if (enemy.IsDead) continue;
                if (Vector2.Distance(_player.Position, enemy.Position) < 25.0f) _player.CurrentHP -= enemy.Damage * dt;
            }

            if (_player.IsDead) { _player.CurrentHP = 0; _currentState = GameState.GameOver; return; }

            _spawnTimer += dt;
            if (_spawnTimer >= 0.8f) 
            {
                _spawnTimer = 0f; Random rand = new Random();
                float spawnX = _player.Position.X + (rand.Next(0, 2) == 0 ? rand.Next(-450, -400) : rand.Next(400, 450));
                float spawnY = _player.Position.Y + (rand.Next(0, 2) == 0 ? rand.Next(-350, -300) : rand.Next(300, 450));
                _enemies.Add(new Enemy { Position = new Vector2(spawnX, spawnY) });
            }

            foreach (var enemy in _enemies) enemy.Update(dt, _player.Position);
            _weapon.Update(dt, _player, _enemies, _damageTexts);

            foreach (var text in _damageTexts) text.Update(dt);
            _damageTexts.RemoveAll(t => t.Timer >= t.Lifetime);

            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                if (_enemies[i].IsDead)
                {
                    Random rand = new Random();
                    int dropIndex = 3; 

                    if (rand.Next(0, 100) < 10) 
                    {
                        int coinRoll = rand.Next(0, 100);
                        if (coinRoll < 70) dropIndex = 0;      
                        else if (coinRoll < 95) dropIndex = 1; 
                        else dropIndex = 2;                    
                    }
                    else 
                    {
                        int expRoll = rand.Next(0, 100);
                        if (expRoll < 60) dropIndex = 3;       
                        else if (expRoll < 85) dropIndex = 4;  
                        else if (expRoll < 97) dropIndex = 5;  
                        else dropIndex = 6;                    
                    }

                    _gems.Add(new ExpGem { Position = _enemies[i].Position, GemTypeIndex = dropIndex });
                    _enemies.RemoveAt(i);
                }
            }

            foreach (var gem in _gems)
            {
                gem.Update(dt); 
                if (Vector2.Distance(_player.Position, gem.Position) < 30.0f)
                {
                    gem.IsCollected = true;
                    if (gem.IsCoin) _player.Gold += gem.GetValue(); 
                    else _levelSystem.AddExp(gem.GetValue()); 
                }
            }
            _gems.RemoveAll(g => g.IsCollected);
            
            if (_levelSystem.IsLevelUpReady) _currentState = GameState.LevelUp;
        }

        private void ResumeGame() { _levelSystem.IsLevelUpReady = false; _currentState = GameState.Playing; }

        private void Render()
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
            Raylib.BeginMode2D(_camera);

            if (_texFloor.Width > 0 && _texFloor.Height > 0)
            {
                int tileW = _texFloor.Width; int tileH = _texFloor.Height;
                float startX = (float)Math.Floor((_camera.Target.X - 400) / tileW) * tileW;
                float startY = (float)Math.Floor((_camera.Target.Y - 300) / tileH) * tileH;
                for (float x = startX; x < _camera.Target.X + 400 + tileW; x += tileW)
                    for (float y = startY; y < _camera.Target.Y + 300 + tileH; y += tileH)
                        Raylib.DrawTexture(_texFloor, (int)x, (int)y, Color.White);
            }
            foreach (var gem in _gems) { Texture2D gemTex = _gemTextures[gem.GemTypeIndex]; if (gemTex.Width > 0) { float frameWidth = (float)gemTex.Width / gem.MaxFrames; Rectangle sourceRec = new Rectangle(gem.CurrentFrame * frameWidth, 0, frameWidth, gemTex.Height); Rectangle destRec = new Rectangle(gem.Position.X, gem.Position.Y, frameWidth, gemTex.Height); System.Numerics.Vector2 origin = new System.Numerics.Vector2(frameWidth / 2, gemTex.Height / 2); Raylib.DrawTexturePro(gemTex, sourceRec, destRec, origin, 0f, Color.White); } else Raylib.DrawCircle((int)gem.Position.X, (int)gem.Position.Y, 5, Color.SkyBlue); }
            foreach (var p in _weapon.Projectiles) Raylib.DrawCircle((int)p.Position.X, (int)p.Position.Y, 5, Color.Yellow);
            foreach (var e in _enemies) { if (_texEnemy.Width > 0) { float frameWidth = (float)_texEnemy.Width / 5; float frameHeight = (float)_texEnemy.Height / 3; Rectangle sourceRec = new Rectangle(0, 0, frameWidth, frameHeight); Rectangle destRec = new Rectangle(e.Position.X, e.Position.Y, frameWidth * 3f, frameHeight * 3f); System.Numerics.Vector2 origin = new System.Numerics.Vector2((frameWidth * 3f) / 2, (frameHeight * 3f) / 2); Color tintColor = (e.HitTimer > 0) ? Color.Red : Color.White; Raylib.DrawTexturePro(_texEnemy, sourceRec, destRec, origin, 0f, tintColor); } else Raylib.DrawRectangle((int)e.Position.X - 10, (int)e.Position.Y - 10, 20, 20, Color.Red); }
            
            if (_texIdle.Width > 0 && _texWalk.Width > 0) { Texture2D currentTex = _player.IsMoving ? _texWalk : _texIdle; int maxFrames = _player.IsMoving ? 24 : 10; int cols = _player.IsMoving ? 4 : 10; int rows = _player.IsMoving ? 6 : 1; int currentFrameNum = _player.CurrentFrame % maxFrames; float frameWidth = (float)currentTex.Width / cols; float frameHeight = (float)currentTex.Height / rows; float sourceX = (currentFrameNum % cols) * frameWidth; float sourceY = (currentFrameNum / cols) * frameHeight; float renderWidth = _player.IsFacingLeft ? frameWidth : -frameWidth; Rectangle sourceRec = new Rectangle(sourceX, sourceY, renderWidth, frameHeight); Rectangle destRec = new Rectangle(_player.Position.X, _player.Position.Y, frameWidth * 1.5f, frameHeight * 1.5f); System.Numerics.Vector2 origin = new System.Numerics.Vector2((frameWidth * 1.5f) / 2, (frameHeight * 1.5f) / 2); Color playerColor = _player.IsDead ? Color.Red : Color.White; Raylib.DrawTexturePro(currentTex, sourceRec, destRec, origin, 0f, playerColor); } else Raylib.DrawCircle((int)_player.Position.X, (int)_player.Position.Y, 15, Color.Blue);

            // ★ 캐릭터 머리 위에 작은 체력바 표시 (BeginMode2D 안에 위치함)
            // ★ 캐릭터 발밑에 작은 체력바 표시
            float hpRatio = _player.CurrentHP / _player.MaxHP;
            int barWidth = 40;
            int barHeight = 6;
            int barX = (int)_player.Position.X - (barWidth / 2);
            
            // ★ 위치를 캐릭터 아래로 옮김 (+ 25)
            int barY = (int)_player.Position.Y + 45; 

            Raylib.DrawRectangle(barX, barY, barWidth, barHeight, Color.DarkGray); // 빈 체력 (배경)
            Raylib.DrawRectangle(barX, barY, (int)(barWidth * hpRatio), barHeight, Color.Red); // 남은 체력 (빨간색)

            foreach (var text in _damageTexts) Raylib.DrawText(text.Damage.ToString(), (int)text.Position.X - 10, (int)text.Position.Y - 20, 20, Color.Yellow);
            
            Raylib.EndMode2D();

            // ---------------------------------------------------
            // HUD 영역 (화면 절대 고정)
            // ---------------------------------------------------
            float expRatio = (float)_levelSystem.CurrentExp / _levelSystem.MaxExp;
            Raylib.DrawRectangle(0, 0, 800, 20, Color.Black);
            Raylib.DrawRectangle(0, 0, (int)(800 * expRatio), 20, Color.Blue);
            
            Raylib.DrawText($"LV: {_levelSystem.Level}", 10, 25, 20, Color.White);
            Raylib.DrawText($"ATK: {_weapon.Damage}", 10, 50, 15, Color.LightGray);
            
            Raylib.DrawText($"GOLD: {_player.Gold}", 650, 25, 20, Color.Gold);

            if (_currentState == GameState.LevelUp) { Raylib.DrawRectangle(0, 0, 800, 600, new Color(0, 0, 0, 150)); Raylib.DrawText("LEVEL UP! (1, 2, 3)", 300, 200, 30, Color.Gold); Raylib.DrawRectangle(100, 280, 180, 100, Color.DarkBlue); Raylib.DrawText("[1] Speed +", 135, 320, 20, Color.White); Raylib.DrawRectangle(310, 280, 180, 100, Color.DarkPurple); Raylib.DrawText("[2] Damage +", 345, 320, 20, Color.White); Raylib.DrawRectangle(520, 280, 180, 100, Color.DarkGreen); Raylib.DrawText("[3] Atk Speed +", 540, 320, 20, Color.White); }
            if (_currentState == GameState.GameOver) { Raylib.DrawRectangle(0, 0, 800, 600, new Color(150, 0, 0, 200)); Raylib.DrawText("YOU DIED", 260, 240, 60, Color.Red); Raylib.DrawText("Game Over", 340, 320, 24, Color.LightGray); }
            
            Raylib.EndDrawing();
        }
    }
}