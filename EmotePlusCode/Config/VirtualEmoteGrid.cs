using Godot;

namespace EmotePlus.EmotePlusCode.Config;

// Keep layout extent independent of the materialized controls, so scrolling and filtering cannot shift the columns.
internal partial class VirtualEmoteGrid : Control
{
    internal const int Columns = 8;
    internal const float CellSize = 72f;
    internal const float ColumnGap = 8f;
    internal const float RowGap = 14f;
    internal const float GridWidth = Columns * CellSize + (Columns - 1) * ColumnGap;

    internal Func<int, Control> CreateCell { get; set; } = null!;
    internal Action<int, Control> UpdateCell { get; set; } = null!;
    private int[] _items = [];
    private readonly Dictionary<int, Control> _cells = [];
    private int? _dragSource;
    private int? _lastFocus;

    internal VirtualEmoteGrid()
    {
        CustomMinimumSize = new Vector2(GridWidth, 0f);
        SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        MouseFilter = MouseFilterEnum.Pass;
        FocusMode = FocusModeEnum.All;
        FocusEntered += () =>
        {
            var position = _lastFocus.HasValue ? Array.IndexOf(_items, _lastFocus.Value) : -1;
            if (_items.Length > 0) EnsureCell(Math.Max(0, position)).GrabFocus();
        };
    }

    internal void SetItems(int[] items)
    {
        _items = items;
        var rows = (items.Length + Columns - 1) / Columns;
        CustomMinimumSize = new Vector2(GridWidth, Math.Max(0f, rows * (CellSize + RowGap) - RowGap));
        // Do not populate synchronously: container sorting has not yet applied the new extent.
        foreach (var key in _cells.Keys.ToArray())
        {
            var position = Array.IndexOf(_items, key);
            if (position < 0) RemoveCell(key);
            else
            {
                _cells[key].Position = PositionOf(position);
                UpdateCell(key, _cells[key]);
            }
        }
    }

    internal void PinDragSource(int index) => _dragSource = index;

    public override void _Process(double delta)
    {
        if (!GetViewport().GuiIsDragging()) _dragSource = null;
        var clip = GetViewport().GetVisibleRect();
        for (var node = GetParent(); node != null; node = node.GetParent())
            if (node is Control { ClipContents: true } parent)
                clip = clip.Intersection(parent.GetGlobalTransformWithCanvas() * new Rect2(Vector2.Zero, parent.Size));

        var wanted = new HashSet<int>();
        if (IsVisibleInTree() && clip.HasArea() && _items.Length > 0)
        {
            var local = GetGlobalTransformWithCanvas().AffineInverse() * clip;
            if (local.End.X > 0f && local.Position.X < GridWidth && local.End.Y > 0f && local.Position.Y < CustomMinimumSize.Y)
            {
                var stride = CellSize + RowGap;
                // One extra row on each side prevents gaps while the native scroll container eases its position.
                var first = Math.Clamp((int)MathF.Floor(local.Position.Y / stride) - 1, 0, (_items.Length + Columns - 1) / Columns);
                var last = Math.Clamp((int)MathF.Ceiling(local.End.Y / stride) + 1, 0, (_items.Length + Columns - 1) / Columns);
                for (var position = first * Columns; position < Math.Min(last * Columns, _items.Length); position++)
                {
                    wanted.Add(_items[position]);
                    EnsureCell(position);
                }
            }
        }
        foreach (var (key, cell) in _cells.ToArray())
            if (!wanted.Contains(key) && (!IsVisibleInTree() || !cell.HasFocus() && key != _dragSource)) RemoveCell(key);
    }

    private Control EnsureCell(int position)
    {
        var key = _items[position];
        if (_cells.TryGetValue(key, out var existing)) return existing;
        var cell = CreateCell(key);
        cell.Size = new Vector2(CellSize, CellSize);
        cell.Position = PositionOf(position);
        AddChild(cell);
        _cells.Add(key, cell);
        UpdateCell(key, cell);
        cell.FocusEntered += () => _lastFocus = key;
        cell.GuiInput += input => Navigate(key, cell, input);
        return cell;
    }

    private void Navigate(int key, Control cell, InputEvent input)
    {
        var offset = input.IsActionPressed("ui_left", true) ? -1 : input.IsActionPressed("ui_right", true) ? 1
            : input.IsActionPressed("ui_up", true) ? -Columns : input.IsActionPressed("ui_down", true) ? Columns : 0;
        if (offset == 0) return;
        var position = Array.IndexOf(_items, key);
        var next = position + offset;
        if (next >= 0 && next < _items.Length)
        {
            EnsureCell(next).GrabFocus(); // Native config scrolling follows controller focus, even beyond the visible rows.
            cell.AcceptEvent();
        }
        else
        {
            var path = offset < 0 ? FocusNeighborTop : FocusNeighborBottom;
            if (!path.IsEmpty && GetNodeOrNull<Control>(path) is { } neighbor && neighbor != this)
            {
                neighbor.GrabFocus();
                cell.AcceptEvent();
            }
        }
    }

    private static Vector2 PositionOf(int position) => new(position % Columns * (CellSize + ColumnGap),
        position / Columns * (CellSize + RowGap));

    private void RemoveCell(int key)
    {
        var cell = _cells[key];
        if (cell is TextureButton button) button.TextureNormal = null;
        RemoveChild(cell);
        cell.QueueFree();
        _cells.Remove(key);
    }
}
