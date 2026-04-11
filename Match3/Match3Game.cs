using System.Text.RegularExpressions;
using Match3Easter.Models;
using Match3Easter.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Match = Match3Easter.Models.Match;

namespace Match3Easter;

public class Match3Game : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private Texture2D _pixel;
    private Board _board;

    private GameState _state = GameState.Idle;

    private Dictionary<GemType, Texture2D> _gemTextures;
    private Dictionary<BonusType, Texture2D> _bonusTextures;
    private Dictionary<DestroyerType, Texture2D> _destroyerTextures;

    private readonly List<GemAnimation> _gemAnimations = [];
    private readonly List<DestroyerAnimation> _destroyerAnimations = [];

    private float _animTimer;
    private const float AnimDuration = 0.25f;
    private const float RemovalAnimDuration = 0.1f;

    private const float ActivationDuration = 0.25f;

    private readonly Dictionary<(int x, int y), float> _removalStartTimes = new();
    private float _lastRemovalTime;
    private readonly HashSet<(int x, int y)> _processedActivations = new();


    private MouseState _previousMouseState;
    private (int x, int y)? _selected = null;

    private int _swapX1, _swapY1, _swapX2, _swapY2;

    private const int CellSize = 64;

    public Match3Game()
    {
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = 8 * CellSize;
        _graphics.PreferredBackBufferHeight = 8 * CellSize;
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        // TODO: Add your initialization logic here (e.g., non-content variables)
        _board = new Board();
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _pixel = new Texture2D(_graphics.GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);

        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // TODO: use this.Content to load your game content here

        _gemTextures = new Dictionary<GemType, Texture2D>
        {
            [GemType.Circle] = Texture2D.FromFile(GraphicsDevice, "Assets/circle.png"),
            [GemType.Diamond] = Texture2D.FromFile(GraphicsDevice, "Assets/diamond.png"),
            [GemType.Square] = Texture2D.FromFile(GraphicsDevice, "Assets/rectangle.png"),
            [GemType.Pentagon] =
                Texture2D.FromFile(GraphicsDevice, "Assets/pentagon.png"), // Pentagon -> hexagon.png
            [GemType.Triangle] =
                Texture2D.FromFile(GraphicsDevice, "Assets/triangle.png"), // Triangle -> bipyramid.png
        };
        _bonusTextures = new Dictionary<BonusType, Texture2D>
        {
            [BonusType.LineHorizontal] = Texture2D.FromFile(GraphicsDevice, "Assets/lineHorizontal.png"),
            [BonusType.LineVertical] = Texture2D.FromFile(GraphicsDevice, "Assets/lineVertical1.png"),
            [BonusType.Bomb] = Texture2D.FromFile(GraphicsDevice, "Assets/bomb.png")
        };
        _destroyerTextures = new Dictionary<DestroyerType, Texture2D>
        {
            [DestroyerType.West] = Texture2D.FromFile(GraphicsDevice, "Assets/destroyerWest.png"),
            [DestroyerType.East] = Texture2D.FromFile(GraphicsDevice, "Assets/destroyerEast.png"),
            [DestroyerType.South] = Texture2D.FromFile(GraphicsDevice, "Assets/destroyerSouth.png"),
            [DestroyerType.North] = Texture2D.FromFile(GraphicsDevice, "Assets/destroyerNorth.png"),
        };
    }

    private static Vector2 GridToScreen(int x, int y)
        => new(x * CellSize, y * CellSize);

    // TODO: REWRITE WITH STATES
    private void OnCellClicked(int x, int y)
    {
        if (_selected is null)
        {
            _selected = (x, y);
            return;
        }

        var (sx, sy) = _selected.Value;
        _selected = null;

        if (!Board.IsSwappable(sx, sy, x, y))
        {
            return;
        }

        _swapX1 = sx;
        _swapY1 = sy;
        _swapX2 = x;
        _swapY2 = y;


        EnterState(GameState.Swapping);
    }

    // TODO: REWRITE WITH STATES
    private void HandleInput()
    {
        var mouse = Mouse.GetState();
        if (_previousMouseState.LeftButton == ButtonState.Pressed && mouse.LeftButton == ButtonState.Released)
        {
            var x = mouse.X / CellSize;
            var y = mouse.Y / CellSize;

            if (x is >= 0 and < 9 && y is >= 0 and < 9)
                OnCellClicked(x, y);
        }

        _previousMouseState = mouse;
    }

    private static float ComputeRadialDelay(int x, int y, int originX, int originY)
    {
        var dist = MathF.Sqrt(MathF.Pow(x - originX, 2) + MathF.Pow(y - originY, 2));
        return dist * RemovalAnimDuration;
    }

    private static float ComputeLineDelay(int x, int y, int originX, int originY)
    {
        var dist = Math.Max(Math.Abs(x - originX), Math.Abs(y - originY));
        return dist * RemovalAnimDuration;
    }

    private float CalculateLocalDelay(DestructionCause cause, int x, int y, int originX, int originY)
    {
        return (cause) switch
        {
            DestructionCause.Bomb or DestructionCause.DoubleBomb => ComputeRadialDelay(x, y, originX, originY),
            DestructionCause.LineHorizontal
                or DestructionCause.LineCross
                or DestructionCause.LineVertical
                => ComputeLineDelay(x, y, originX, originY),
            _ => 0f
        };
    }

    private void EnterState(GameState newState)
    {
        _state = newState;
        _animTimer = 0f;

        switch (newState)
        {
            case GameState.Swapping or GameState.SwappingBack:
                _gemAnimations.Clear();

                _gemAnimations.Add(new GemAnimation // CALL BEFORE SWAP -> tile will be on FROM_POSITION
                {
                    From = GridToScreen(_swapX1, _swapY1),
                    To = GridToScreen(_swapX2, _swapY2),
                    GridX = _swapX1, GridY = _swapY1
                });

                _gemAnimations.Add(new GemAnimation // CALL BEFORE SWAP -> tile will be on FROM_POSITION
                {
                    From = GridToScreen(_swapX2, _swapY2),
                    To = GridToScreen(_swapX1, _swapY1),
                    GridX = _swapX2, GridY = _swapY2
                });
                break;

            case GameState.CreateBonuses:
                _gemAnimations.Clear();

                foreach (var (toX, toY, fromX, fromY) in
                         _board.CalcBonusAnimPositions(_swapX1, _swapY1, _swapX2, _swapY2))
                    _gemAnimations.Add(new GemAnimation // CALL BEFORE SWAP -> tile will be on FROM_POSITION
                    {
                        From = GridToScreen(fromX, fromY),
                        To = GridToScreen(toX, toY),
                        GridX = fromX, GridY = fromY
                    });

                break;

            case GameState.Removing:
                _removalStartTimes.Clear();

                _lastRemovalTime = 0f;
                _processedActivations.Clear();

                // while (_board._destructionEventQueue.TryDequeue(out var ev))
                while (_board.TryDequeueDestructionEvent(out var ev))
                {
                    var triggerTime = ev.BaseTime + CalculateLocalDelay(ev.Cause, ev.X, ev.Y, ev.XOrigin, ev.YOrigin);

                    if (triggerTime > _lastRemovalTime) _lastRemovalTime = triggerTime;

                    if (_removalStartTimes.TryGetValue((ev.X, ev.Y), out var existing))
                        triggerTime = Math.Min(existing, triggerTime);

                    _removalStartTimes[(ev.X, ev.Y)] = triggerTime;

                    var gem = _board.GetGem(ev.X, ev.Y);
                    if (gem is null) continue;

                    if (gem._getBonusType() == BonusType.None || _processedActivations.Contains((ev.X, ev.Y)) ||
                        _board.IsProtectedForRemoval(ev.X, ev.Y)) continue;

                    _processedActivations.Add((ev.X, ev.Y));

                    var cascadeBaseTime = triggerTime + ActivationDuration;

                    var bonusType = _board.ResolveBonusType(gem._getBonusType(), BonusType.None);
                    var bonusActivator = _board.ResolveBonusActivator(bonusType);

                    switch (gem._getBonusType())
                    {
                        case BonusType.LineHorizontal:
                            //West destroyer
                            _destroyerAnimations.Add(new DestroyerAnimation
                            {
                                LaunchTime = cascadeBaseTime,
                                Type = DestroyerType.West,
                                From = GridToScreen(ev.X, ev.Y),
                                To = GridToScreen(-1, ev.Y),
                                TravelDuration = (ev.X + 1) * RemovalAnimDuration
                            });
                            // East destroyer
                            _destroyerAnimations.Add(new DestroyerAnimation
                            {
                                LaunchTime = cascadeBaseTime,
                                Type = DestroyerType.East,
                                From = GridToScreen(ev.X, ev.Y),
                                To = GridToScreen(_board.GetXSize(), ev.Y),
                                TravelDuration = (_board.GetXSize() - ev.X) * RemovalAnimDuration
                            });
                            break;
                        case BonusType.LineVertical:
                            //North destroyer
                            _destroyerAnimations.Add(new DestroyerAnimation
                            {
                                LaunchTime = cascadeBaseTime,
                                Type = DestroyerType.North,
                                From = GridToScreen(ev.X, ev.Y),
                                To = GridToScreen(ev.X, -1),
                                TravelDuration = (ev.Y + 1) * RemovalAnimDuration
                            });

                            // South destroyer
                            _destroyerAnimations.Add(new DestroyerAnimation
                            {
                                LaunchTime = cascadeBaseTime,
                                Type = DestroyerType.South,
                                From = GridToScreen(ev.X, ev.Y),
                                To = GridToScreen(ev.X, _board.GetYSize()),
                                TravelDuration = (_board.GetYSize() - ev.Y) * RemovalAnimDuration
                            });
                            break;

                        case BonusType.Cross:
                            // To North
                            _destroyerAnimations.Add(new DestroyerAnimation
                            {
                                LaunchTime = cascadeBaseTime,
                                Type = DestroyerType.North,
                                From = GridToScreen(ev.X, ev.Y),
                                To = GridToScreen(ev.X, -1),
                                TravelDuration = (ev.Y + 1) * RemovalAnimDuration
                            });


                            // To West
                            _destroyerAnimations.Add(new DestroyerAnimation
                            {
                                LaunchTime = cascadeBaseTime,
                                Type = DestroyerType.West,
                                From = GridToScreen(ev.X, ev.Y),
                                To = GridToScreen(-1, ev.Y),
                                TravelDuration = (ev.X + 1) * RemovalAnimDuration
                            });

                            // To South
                            _destroyerAnimations.Add(new DestroyerAnimation
                            {
                                LaunchTime = cascadeBaseTime,
                                Type = DestroyerType.South,
                                From = GridToScreen(ev.X, ev.Y),
                                To = GridToScreen(ev.X, _board.GetYSize()),
                                TravelDuration = (_board.GetYSize() - ev.Y) * RemovalAnimDuration
                            });

                            // To East
                            _destroyerAnimations.Add(new DestroyerAnimation
                            {
                                LaunchTime = cascadeBaseTime,
                                Type = DestroyerType.East,
                                From = GridToScreen(ev.X, ev.Y),
                                To = GridToScreen(_board.GetXSize(), ev.Y),
                                TravelDuration = (_board.GetXSize() - ev.X) * RemovalAnimDuration
                            });
                            break;
                        case BonusType.TripleLineVertical:
                            for (var i = ev.X - 1; i <= ev.X + 1; i++)
                            {
                                if (!_board.InBounds(i, ev.Y)) continue;
                                _destroyerAnimations.Add(new DestroyerAnimation
                                {
                                    LaunchTime = cascadeBaseTime,
                                    Type = DestroyerType.North,
                                    From = GridToScreen(i, ev.Y),
                                    To = GridToScreen(i, -1),
                                    TravelDuration = (ev.Y + 1) * RemovalAnimDuration
                                });

                                // South destroyer
                                _destroyerAnimations.Add(new DestroyerAnimation
                                {
                                    LaunchTime = cascadeBaseTime,
                                    Type = DestroyerType.South,
                                    From = GridToScreen(i, ev.Y),
                                    To = GridToScreen(i, _board.GetYSize()),
                                    TravelDuration = (_board.GetYSize() - ev.Y) * RemovalAnimDuration
                                });
                            }

                            break;
                        case BonusType.TripleLineHorizontal:
                            for (var j = ev.Y - 1; j <= ev.Y + 1; j++)
                            {
                                if (!_board.InBounds(ev.X, j)) continue;
                                //West destroyer
                                _destroyerAnimations.Add(new DestroyerAnimation
                                {
                                    LaunchTime = cascadeBaseTime,
                                    Type = DestroyerType.West,
                                    From = GridToScreen(ev.X, j),
                                    To = GridToScreen(-1, j),
                                    TravelDuration = (ev.X + 1) * RemovalAnimDuration
                                });
                                // East destroyer
                                _destroyerAnimations.Add(new DestroyerAnimation
                                {
                                    LaunchTime = cascadeBaseTime,
                                    Type = DestroyerType.East,
                                    From = GridToScreen(ev.X, j),
                                    To = GridToScreen(_board.GetXSize(), j),
                                    TravelDuration = (_board.GetXSize() - ev.X) * RemovalAnimDuration
                                });
                            }

                            break;
                    }

                    bonusActivator!(ev.X, ev.Y, cascadeBaseTime);
                }

                break;

            case GameState.Falling:

                _gemAnimations.Clear();
                // собрать анимации ДО перемещения данных
                var fallData = _board.CalcFallPositions();
                foreach (var (fx, fromY, toY) in fallData)
                {
                    _gemAnimations.Add(new GemAnimation
                    {
                        From = GridToScreen(fx, fromY),
                        To = GridToScreen(fx, toY),
                        GridX = fx, GridY = fromY // рисуем до падения -> фишка будет на fromY
                    });
                }

                break;

            case GameState.Filling:

                _board.FillCols(); // CALLING BEFORE ANIMATION

                _gemAnimations.Clear();

                var fillData = _board.CalcFillPositions();
                foreach (var (fx, fromY, toY) in fillData)
                {
                    _gemAnimations.Add(new GemAnimation
                    {
                        From = GridToScreen(fx, fromY),
                        To = GridToScreen(fx, toY),
                        GridX = fx, GridY = toY // рисуем после падения -> фишка будет на toY
                    });
                }

                break;
        }
    }


    protected override void Update(GameTime gameTime)
    {
        // Exit game if Back button (Controller) or Escape (Keyboard) is pressed
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
            Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        // TODO: Add your update logic here (input handling, physics, etc.)

        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        switch (_state)
        {
            //Wait for input
            case GameState.Idle:
                _animTimer += dt;
                if (_animTimer >= AnimDuration)
                {
                    HandleInput();
                }

                break;

            // Wait till Swap ANIM will end then go to matching or SwapBack
            case GameState.Swapping:
                _animTimer += dt;
                if (_animTimer >= AnimDuration)
                {
                    var gem1 = _board.GetGem(_swapX1, _swapY1)!;
                    var gem2 = _board.GetGem(_swapX2, _swapY2)!;

                    var bonusActivator = _board.ResolveBonusActivator(
                        _board.ResolveBonusType(gem1._getBonusType(), gem2._getBonusType())
                    );

                    switch (
                        gem1.isBonus(),
                        gem2.isBonus())
                    {
                        case (false, false):
                            _board.Swap(_swapX1, _swapY1, _swapX2, _swapY2);
                            _board.MarkDirty(_swapX1, _swapY1);
                            _board.MarkDirty(_swapX2, _swapY2);

                            EnterState(_board.FindMatches() ? GameState.Matching : GameState.SwappingBack);

                            break;
                        case (true, false):
                            _board.Swap(_swapX1, _swapY1, _swapX2, _swapY2);
                            _board.MarkDirty(_swapX1, _swapY1);
                            _board.MarkDirty(_swapX2, _swapY2);

                            bonusActivator!(_swapX2, _swapY2, ActivationDuration);

                            EnterState(_board.FindMatches() ? GameState.Matching : GameState.Removing);

                            break;

                        case (false, true):
                            _board.Swap(_swapX1, _swapY1, _swapX2, _swapY2);
                            _board.MarkDirty(_swapX1, _swapY1);
                            _board.MarkDirty(_swapX2, _swapY2);

                            bonusActivator!(_swapX1, _swapY1, ActivationDuration);

                            EnterState(_board.FindMatches() ? GameState.Matching : GameState.Removing);

                            break;

                        case (true, true):

                            _board.MergeBonus(_swapX1, _swapY1, _swapX2, _swapY2);

                            bonusActivator!(_swapX2, _swapY2, ActivationDuration);

                            EnterState(_board.FindMatches() ? GameState.Matching : GameState.Removing);
                            break;
                    }
                }

                break;

            // Wait till SwapBack ANIM will end then go Idle
            case GameState.SwappingBack:
                _animTimer += dt;
                if (_animTimer >= AnimDuration)
                {
                    _board.Swap(_swapX1, _swapY1, _swapX2, _swapY2);
                    EnterState(GameState.Idle);
                }

                break;

            case GameState.CreateBonuses:
                _animTimer += dt;
                if (_animTimer >= AnimDuration)
                {
                    _board.CreateBonuses(_swapX1, _swapY1, _swapX2, _swapY2);
                    _board.MarkMatches();
                    EnterState(GameState.Removing);
                }

                break;

            // Wait until swap ANIM ends then mark matches and go to Removing 
            case GameState.Matching:
                _animTimer += dt;
                if (_animTimer >= AnimDuration)
                {
                    if (_board.HasBonusMatches())
                    {
                        EnterState(GameState.CreateBonuses);
                    }
                    else
                    {
                        _board.MarkMatches();
                        EnterState(GameState.Removing);
                    }
                }

                break;

            // Wait till all gems disappear ANIM will end: it happens after _lastRemovalTime + AnimDuration
            // then update logic
            case GameState.Removing:
                _animTimer += dt;

                if (_animTimer >= _lastRemovalTime + AnimDuration)
                {
                    _destroyerAnimations.Clear();
                    _board.CallDestruction();
                    _board.ReshuffleFreeGems();
                    EnterState(GameState.Falling);
                }

                break;

            // Wait till Fall ANIM will end then 
            case GameState.Falling:
                _animTimer += dt;
                if (_animTimer >= AnimDuration)
                {
                    _board.FallCols(); // CALLING AFTER ANIMATION
                    EnterState(GameState.Filling);
                }

                break;
            case GameState.Filling:
                _animTimer += dt;
                if (_animTimer >= AnimDuration)
                {
                    EnterState(_board.FindMatches() ? GameState.Matching : GameState.Idle);
                }

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        base.Update(gameTime);
    }

    private Color GetGemColor(Gem gem)
    {
        var color = gem._getGemType() switch
        {
            GemType.Pentagon => Color.Red,
            GemType.Triangle => Color.Blue,
            GemType.Square => Color.Green,
            GemType.Diamond => Color.Yellow,
            GemType.Circle => Color.Purple,
            _ => Color.White
        };
        return color;
    }

    protected override void Draw(GameTime gameTime)
    {
        // Clear the screen with a specific color (standard is CornflowerBlue)
        GraphicsDevice.Clear(Color.LightGoldenrodYellow);

        // TODO: Add your drawing code here

        _spriteBatch.Begin(samplerState: SamplerState.LinearClamp);

        var t = Math.Clamp(_animTimer / AnimDuration, 0f, 1f);

        // Background
        for (var x = 0; x < 8; x++)
        for (var y = 0; y < 8; y++)
        {
            var cellColor = (x + y) % 2 == 0
                ? new Color(50, 40, 70)
                : new Color(35, 25, 55);

            _spriteBatch.Draw(_pixel,
                new Rectangle(x * CellSize, y * CellSize, CellSize, CellSize),
                cellColor);
        }

        // Gems
        for (var x = 0; x < 8; x++)
        for (var y = 0; y < 8; y++)
        {
            var gem = _board.GetGem(x, y);
            if (gem is null) continue;

            // var color = GetGemColor(gem);
            var pos = GridToScreen(x, y);

            var scale = 1f;
            var alpha = 1f;

            Texture2D texture;

            if (gem._getGemType() != GemType.None)
            {
                texture = _gemTextures[gem._getGemType()];
            }
            else if (gem._getBonusType() != BonusType.None && _bonusTextures.ContainsKey(gem._getBonusType()))
            {
                texture = _bonusTextures[gem._getBonusType()];
            }
            else
            {
                continue;
            }

            // ???Creating interpolating Vector to Animate Tile Path???
            var animIndex = _gemAnimations.FindIndex(a => a.GridX == x && a.GridY == y);


            switch (_state)
            {
                case GameState.Swapping or GameState.SwappingBack:
                    if (animIndex >= 0)
                        pos = Vector2.Lerp(_gemAnimations[animIndex].From,
                            _gemAnimations[animIndex].To, t);
                    break;
                case GameState.CreateBonuses:
                    if (animIndex >= 0)
                    {
                        pos = Vector2.Lerp(_gemAnimations[animIndex].From,
                            _gemAnimations[animIndex].To, t);

                        if (!(_gemAnimations[animIndex].From == _gemAnimations[animIndex].To))
                        {
                            scale = 1f - t;
                            alpha = 1f - t;
                        }
                    }

                    break;

                case GameState.Removing:
                    if (_removalStartTimes.TryGetValue((x, y), out var startTime))
                    {
                        var elapsed = _animTimer - startTime;
                        if (elapsed < 0) scale = 1f;
                        else
                        {
                            var localT = Math.Clamp(elapsed / RemovalAnimDuration, 0f, 1f);
                            scale = 1f - localT;
                            alpha = 1f - localT;
                        }
                    }

                    break;
                case GameState.Falling:
                    var fallIdx = _gemAnimations.FindIndex(a => a.GridX == x && a.GridY == y);
                    if (fallIdx >= 0)
                        pos = Vector2.Lerp(_gemAnimations[fallIdx].From,
                            _gemAnimations[fallIdx].To, t);
                    break;
                case GameState.Filling:
                    var fillIdx = _gemAnimations.FindIndex(a => a.GridX == x && a.GridY == y);
                    if (fillIdx >= 0)
                        pos = Vector2.Lerp(_gemAnimations[fillIdx].From,
                            _gemAnimations[fillIdx].To, t);
                    break;
            }


            //Disappearing -> decrease size

            var size = (int)(60 * scale);
            var offset = (60 - size) / 2;

            _spriteBatch.Draw(
                texture,
                position: pos + new Vector2(2 + offset, 2 + offset),
                sourceRectangle: null,
                color: Color.White * alpha,
                rotation: 0f,
                origin: Vector2.Zero,
                scale: new Vector2(size / (float)texture.Width),
                effects: SpriteEffects.None,
                layerDepth: 0f
            );
        }

        // Destroyers


        if (_destroyerAnimations.Count > 0)
        {
            foreach (var animation in _destroyerAnimations)
            {
                var elapsed = _animTimer - animation.LaunchTime;
                // if (elapsed < 0) continue;

                // if (animation.Type != BonusType.LineHorizontal || animation.Type != BonusType.LineVertical)
                //     continue;

                var t1 = Math.Clamp(elapsed / animation.TravelDuration, 0f, 1f);
                var pos = Vector2.Lerp(animation.From, animation.To, t1);
                var texture = _destroyerTextures[animation.Type];

                _spriteBatch.Draw(
                    texture,
                    position: pos + new Vector2(2, 2),
                    sourceRectangle: null,
                    color: Color.White,
                    rotation: 0f,
                    origin: Vector2.Zero,
                    scale: 60f / texture.Width,
                    effects: SpriteEffects.None,
                    layerDepth: 0f
                );
            }
        }

        // Selection
        if (_selected is not null)
        {
            var (sx, sy) = _selected.Value;
            _spriteBatch.Draw(_pixel,
                new Rectangle(sx * CellSize, sy * CellSize, CellSize, CellSize),
                Color.White * 0.3f); // полупрозрачная подсветка поверх
        }


        _spriteBatch.End();

        base.Draw(gameTime);
    }
}