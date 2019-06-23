using UnityEngine;

namespace GraphVisualizer
{
    // Interface for rendering a tree layout to screen.
    public interface IGraphRenderer
    {
        void Draw(IGraphLayout graphLayout, Rect drawingArea);
        void Draw(IGraphLayout graphLayout, Rect drawingArea, GraphSettings graphSettings);
    }
}
