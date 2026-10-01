using UnityEngine;
using UnityEngine.UIElements;

namespace GamblingSeriousGame.UI
{
    public enum DockIconKind { Home, Interventions, MyPet, Settings, Resources, Play, Pause }

    /// <summary>Small vector icons, also visible in UI Builder without a controller.</summary>
    [UxmlElement]
    public partial class DockIcon : VisualElement
    {
        private DockIconKind kind;
        [UxmlAttribute]
        public DockIconKind Kind
        {
            get => kind;
            set { kind = value; MarkDirtyRepaint(); }
        }

        public DockIcon()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            float scale = Mathf.Min(contentRect.width, contentRect.height) / 24f;
            Vector2 Point(float x, float y) => new Vector2(contentRect.x + x * scale, contentRect.y + y * scale);
            painter.fillColor = resolvedStyle.color;
            painter.strokeColor = resolvedStyle.color;
            painter.lineWidth = 1.8f * scale;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;

            void Line(params float[] points)
            {
                painter.BeginPath();
                painter.MoveTo(Point(points[0], points[1]));
                for (int i = 2; i < points.Length; i += 2) painter.LineTo(Point(points[i], points[i + 1]));
                painter.Stroke();
            }
            void Polygon(params float[] points)
            {
                painter.BeginPath();
                painter.MoveTo(Point(points[0], points[1]));
                for (int i = 2; i < points.Length; i += 2) painter.LineTo(Point(points[i], points[i + 1]));
                painter.ClosePath();
                painter.Fill();
            }
            void Circle(float x, float y, float radius)
            {
                painter.BeginPath();
                painter.Arc(Point(x, y), radius * scale, 0, 360);
                painter.Fill();
            }

            switch (Kind)
            {
                case DockIconKind.Play:
                    Polygon(7, 3, 21, 12, 7, 21);
                    break;
                case DockIconKind.Pause:
                    Polygon(5, 3, 10, 3, 10, 21, 5, 21);
                    Polygon(14, 3, 19, 3, 19, 21, 14, 21);
                    break;
                case DockIconKind.Home:
                    Polygon(3, 10, 12, 3, 21, 10, 21, 20, 15, 20, 15, 13, 9, 13, 9, 20, 3, 20);
                    break;
                case DockIconKind.Interventions:
                    painter.BeginPath();
                    painter.MoveTo(Point(12, 21));
                    painter.BezierCurveTo(Point(-5, 10), Point(5, -2), Point(12, 7));
                    painter.BezierCurveTo(Point(19, -2), Point(29, 10), Point(12, 21));
                    painter.ClosePath();
                    painter.Fill();
                    break;
                case DockIconKind.MyPet:
                    Circle(5, 10, 2.3f); Circle(9, 5.5f, 2.5f);
                    Circle(15, 5.5f, 2.5f); Circle(19, 10, 2.3f);
                    painter.BeginPath();
                    painter.MoveTo(Point(12, 11));
                    painter.BezierCurveTo(Point(8, 11), Point(3, 19), Point(8, 21));
                    painter.BezierCurveTo(Point(11, 22), Point(11, 19), Point(14, 21));
                    painter.BezierCurveTo(Point(22, 23), Point(17, 11), Point(12, 11));
                    painter.Fill();
                    break;
                case DockIconKind.Resources:
                    Line(12, 6, 8, 4, 2, 4, 2, 19, 8, 19, 12, 21, 16, 19, 22, 19, 22, 4, 16, 4, 12, 6, 12, 21);
                    Line(5, 9, 8, 9); Line(5, 13, 8, 13);
                    Line(16, 9, 19, 9); Line(16, 13, 19, 13);
                    break;
                case DockIconKind.Settings:
                    // A toothed ring with a transparent centre.
                    painter.BeginPath();
                    for (int i = 0; i < 32; i++)
                    {
                        float angle = i * Mathf.PI / 16;
                        float radius = i % 4 == 0 || i % 4 == 3 ? 9.5f : 7.5f;
                        var point = Point(12 + Mathf.Cos(angle) * radius, 12 + Mathf.Sin(angle) * radius);
                        if (i == 0) painter.MoveTo(point); else painter.LineTo(point);
                    }
                    painter.ClosePath();
                    painter.MoveTo(Point(15, 12));
                    painter.Arc(Point(12, 12), 3 * scale, 0, 360);
                    painter.ClosePath();
                    painter.Fill(FillRule.OddEven);
                    break;
            }
        }
    }
}
