using Godot;

namespace Breakout.GameObjects;

public partial class Brick : StaticBody2D
{
    public void Destroy()
    {
        QueueFree();
    }
}
