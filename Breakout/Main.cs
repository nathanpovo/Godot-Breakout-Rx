using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Breakout.GameObjects;
using Godot;

namespace Breakout;

public partial class Main : Node2D
{
    [Export]
    public PackedScene BrickScene { get; set; } = default!;

    private const float PaddleSpeed = 200;
    private const float BallSpeed = 250;

    private readonly Subject<double> onProcess = new();
    private readonly Subject<double> onPhysicsProcess = new();

    private readonly CompositeDisposable disposables = new();

    private readonly State initialState = new()
    {
        Score = 0
    };

    private CharacterBody2D ball = default!;
    private CharacterBody2D paddle = default!;

    public Main()
    {
        disposables.Add(onProcess);
        disposables.Add(onPhysicsProcess);

        var paddleInput = onProcess.Select(
                _ =>
                {
                    Vector2 direction = Vector2.Zero;

                    if (Input.IsActionPressed("ui_left"))
                    {
                        direction += Vector2.Left;
                    }

                    if (Input.IsActionPressed("ui_right"))
                    {
                        direction += Vector2.Right;
                    }

                    return direction;
                }
            )
            .DistinctUntilChanged()
            .Publish();

        disposables.Add(paddleInput.Connect());

        var paddleConnection = onPhysicsProcess.WithLatestFrom(paddleInput)
            .Subscribe(x =>
            {
                (double deltaTime, Vector2 inputDirection) = x;

                Vector2 paddleMotion = inputDirection * PaddleSpeed * Convert.ToSingle(deltaTime);
                paddle.MoveAndCollide(paddleMotion);
            });

        disposables.Add(paddleConnection);

        // var updateConnection =  onProcess.CombineLatest(paddleInput, paddle)
        //     .Subscribe(Update);
        //
        // disposables.Add(updateConnection);

        var objectUpdateHandlerConnection = onPhysicsProcess
            .Scan(initialState, ObjectUpdateHandler)
            .Subscribe();

        disposables.Add(objectUpdateHandlerConnection);
    }

    private State ObjectUpdateHandler(State currentState, double deltaTime)
    {
        int score = currentState.Score;

        KinematicCollision2D? collision = ball.MoveAndCollide(ball.Velocity * Convert.ToSingle(deltaTime));

        if (collision?.GetCollider() is { } collider)
        {
            // Bounce back
            var newVelocity = ball.Velocity.Bounce(collision.GetNormal());

            if (collider is Paddle)
            {
                var xx = collision.GetPosition().X - paddle.GlobalPosition.X;
                var right = xx > 0;
                var left = xx < 0;

                var movingToTheRight = ball.Velocity.X > 0;

                if (right)
                {
                    // coming in from the right
                    if (movingToTheRight)
                    {
                        // Do nothing
                    }
                    else
                    {
                        // Flip the X velocity
                        newVelocity.X = -newVelocity.X;
                    }
                }

                if (left)
                {
                    // coming in from the right
                    if (movingToTheRight)
                    {
                        // Flip the X velocity
                        newVelocity.X = -newVelocity.X;
                    }
                    else
                    {
                        // Do nothing
                    }
                }
            }

            ball.Velocity = newVelocity;

            if (collider is Brick brick)
            {
                score += 10;
                brick.Destroy();
            }
        }

        return new State
        {
            Score = score,
        };
    }

    private void Update((double DeltaTime, int InputDirection, double PaddlePosition) x)
    {
    }

    public override void _Ready()
    {
        ball = GetNode<Ball>("Ball");
        paddle = GetNode<Paddle>("Paddle");

        var bricksStartPosition = GetNode<Marker2D>("Bricks/StartPosition").Position;
        var bricks = GetNode<Node>("Bricks");

        const int bricksVerticalCount = 8;
        const int bricksHorizontalCount = 14;

        const float bricksVerticalSpacing = 4.5f;
        // const float bricksHorizontalSpacing = 5.5f;
        const float bricksHorizontalSpacing = 5f;

        const float brickWidth = 36;
        const float brickHeight = 10;

        for (int row = 0; row < bricksVerticalCount; row++)
        {
            for (int column = 0; column < bricksHorizontalCount; column++)
            {
                var brick = BrickScene.Instantiate<Node2D>();

                brick.Position = new Vector2
                {
                    X = bricksStartPosition.X + (brickWidth + bricksHorizontalSpacing) * column,
                    Y = bricksStartPosition.Y + (brickHeight + bricksVerticalSpacing) * row,
                };

                bricks.AddChild(brick);
            }
        }

        var ballSpawnLocation = GetNode<PathFollow2D>("Path2D/PathFollow2D");
        Random random = new ();
        ballSpawnLocation.ProgressRatio = random.NextSingle();

        ball.Position = ballSpawnLocation.Position;

        // If the ball has spawned in the second half of the screen: move to the left
        var horizontalVelocity = ballSpawnLocation.ProgressRatio > 0.5
            ? random.NextSingle() * Vector2.Left
            : random.NextSingle() * Vector2.Right;

        ball.Velocity = (Vector2.Down + horizontalVelocity) * BallSpeed;
    }

    public override void _Process(double delta)
        => onProcess.OnNext(delta);

    public override void _PhysicsProcess(double delta)
        => onPhysicsProcess.OnNext(delta);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            disposables.Dispose();
        }

        base.Dispose(disposing);
    }
}

public readonly record struct State(int Score);
