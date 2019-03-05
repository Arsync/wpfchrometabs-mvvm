using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ChromeTabs
{
    public class TabShape : Shape
    {
        public TabShape()
        {
            Stretch = Stretch.Fill;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            if (double.IsPositiveInfinity(constraint.Width) || double.IsPositiveInfinity(constraint.Height))
                return Size.Empty;

            // We will size ourselves to fit the available space
            return constraint;
        }
        protected override Geometry DefiningGeometry => GetGeometry();

        private Geometry GetGeometry()
        {
            var width = DesiredSize.Width - StrokeThickness;

            double height = 25;

            var x1 = width - 15;
            var x2 = width - 10;
            var x3 = width - 5;
            var x4 = width - 2.5;
            var x5 = width;

            return Geometry.Parse(string.Format(CultureInfo.InvariantCulture,
                "M0,{5} C2.5,{5} 5,0 10,0 15,0 {0},0 {1},0 {2},0 {3},{5} {4},{5}", x1, x2, x3, x4, x5, height));
        }
    }
}
