using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DrawArt_f
{
    public partial class MainWindow : Window
    {
        private DrawingAttributes defaultDrawingAttributes;
        private double currentThickness;
        private List<Stroke> removedStrokes = new List<Stroke>();

        public MainWindow()
        {
            InitializeComponent();
            defaultDrawingAttributes = new DrawingAttributes

            {
                Color = Colors.Black,
                Width = 2,
                Height = 2
            };

            inkCanvas.DefaultDrawingAttributes = defaultDrawingAttributes;
            colorPicker.SelectedColor = Colors.Black;
            currentThickness = 2;
        }

        private void newFile_Click(object sender, RoutedEventArgs e)
        {
            inkCanvas.Strokes.Clear();
            inkCanvas.Background = new SolidColorBrush(Colors.White);
            thicknessSlider.Value = 2;
            inkCanvas.RenderTransform = new ScaleTransform(1, 1);
            inkCanvas.RenderTransformOrigin = new Point(0, 0);
            scrollViewer.ScrollToHorizontalOffset(0);
            scrollViewer.ScrollToVerticalOffset(0);
            zoomSlider.Value = 1;
        }

        private void saveFile_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "PNG Image|*.png|JPEG Image|*.jpg|Bitmap Image|*.bmp";
            if (saveFileDialog.ShowDialog() == true)
            {
                var width = (int)inkCanvas.ActualWidth;
                var height = (int)inkCanvas.ActualHeight;
                var rtb = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Default);
                rtb.Render(inkCanvas);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                using (var fileStream = new FileStream(saveFileDialog.FileName, FileMode.Create))
                {
                    encoder.Save(fileStream);
                }
            }
        }

        private void btnUndo_Click(object sender, RoutedEventArgs e)
        {
            if (inkCanvas.Strokes.Count > 0)
            {
                Stroke lastStroke = inkCanvas.Strokes.Last();
                inkCanvas.Strokes.Remove(lastStroke);
                removedStrokes.Add(lastStroke);
            }
        }

        private void btnRedo_Click(object sender, RoutedEventArgs e)
        {
            if (removedStrokes.Count > 0)
            {
                Stroke lastRemovedStroke = removedStrokes.Last();
                inkCanvas.Strokes.Add(lastRemovedStroke);
                removedStrokes.Remove(lastRemovedStroke);
            }
        }

        private void ColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (e.NewValue.HasValue)
            {
                inkCanvas.DefaultDrawingAttributes.Color = e.NewValue.Value;
            }
        }

        private void thicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (inkCanvas != null)
            {
                double newThickness = e.NewValue;
                inkCanvas.DefaultDrawingAttributes.Width = newThickness;
                inkCanvas.DefaultDrawingAttributes.Height = newThickness;
                currentThickness = newThickness;

                if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint)
                {
                    inkCanvas.EraserShape = new EllipseStylusShape(newThickness, newThickness);
                    currentThickness = newThickness;
                }
            }
        }



        private void btnNavigate_Click(object sender, RoutedEventArgs e)
        {
            if (inkCanvas.EditingMode != InkCanvasEditingMode.None)
            {
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
                inkCanvas.Cursor = Cursors.Hand;
            }
            else
            {
                inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                inkCanvas.Cursor = Cursors.Pen;
            }
        }

        private Point? lastDragPoint;

        private void inkCanvas_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (inkCanvas.EditingMode == InkCanvasEditingMode.None)
            {
                lastDragPoint = e.GetPosition(scrollViewer);
                inkCanvas.CaptureMouse();
            }
        }

        private void inkCanvas_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (inkCanvas.EditingMode == InkCanvasEditingMode.None)
            {
                inkCanvas.ReleaseMouseCapture();
                lastDragPoint = null;
            }
        }

        private void inkCanvas_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (inkCanvas.EditingMode == InkCanvasEditingMode.None && lastDragPoint.HasValue)
            {
                Point currentPosition = e.GetPosition(scrollViewer);
                Vector delta = currentPosition - lastDragPoint.Value;

                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - delta.X);
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - delta.Y);

                lastDragPoint = currentPosition;
            }
        }

        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            inkCanvas.EditingMode = InkCanvasEditingMode.Select;
        }

        private void btnBrush_Click(object sender, RoutedEventArgs e)
        {
            inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
        }

        private void btnEraser_Click(object sender, RoutedEventArgs e)
        {
            inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
            inkCanvas.EraserShape = new EllipseStylusShape(thicknessSlider.Value, thicknessSlider.Value);
        }

        private void btnFill_Click(object sender, RoutedEventArgs e)
        {
            inkCanvas.EditingMode = InkCanvasEditingMode.None;
            inkCanvas.MouseUp += InkCanvas_MouseUp;
        }

        private void InkCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Point clickPosition = e.GetPosition(inkCanvas);
            Color fillColor = inkCanvas.DefaultDrawingAttributes.Color;
            FloodFill(clickPosition, fillColor);
            inkCanvas.MouseUp -= InkCanvas_MouseUp;
        }

        private void FloodFill(Point startPoint, Color fillColor)
        {
            RenderTargetBitmap rtb = new RenderTargetBitmap((int)inkCanvas.ActualWidth, (int)inkCanvas.ActualHeight, 96d, 96d, PixelFormats.Default);
            rtb.Render(inkCanvas);

            int width = rtb.PixelWidth;
            int height = rtb.PixelHeight;
            int startX = (int)startPoint.X;
            int startY = (int)startPoint.Y;

            byte[] pixels = new byte[width * height * 4];
            rtb.CopyPixels(pixels, width * 4, 0);

            int startIndex = (startY * width + startX) * 4;
            Color startColor = Color.FromArgb(pixels[startIndex + 3], pixels[startIndex + 2], pixels[startIndex + 1], pixels[startIndex]);

            if (startColor == fillColor)
            {
                return;
            }

            Queue<Point> queue = new Queue<Point>();
            queue.Enqueue(startPoint);

            while (queue.Count > 0)
            {
                Point currentPoint = queue.Dequeue();
                int x = (int)currentPoint.X;
                int y = (int)currentPoint.Y;

                int index = (y * width + x) * 4;

                while (x > 0 && pixels[index - 4] == startColor.B)
                {
                    x--;
                    index -= 4;
                }

                bool spanUp = false;
                bool spanDown = false;

                while (x < width && pixels[index] == startColor.B)
                {
                    pixels[index + 3] = fillColor.A;
                    pixels[index + 2] = fillColor.R;
                    pixels[index + 1] = fillColor.G;
                    pixels[index] = fillColor.B;

                    if (!spanUp && y > 0 && pixels[index - width * 4] == startColor.B)
                    {
                        queue.Enqueue(new Point(x, y - 1));
                        spanUp = true;
                    }
                    else if (spanUp && y > 0 && pixels[index - width * 4] != startColor.B)
                    {
                        spanUp = false;
                    }
                    if (!spanDown && y < height - 1 && pixels[index + width * 4] == startColor.B)
                    {
                        queue.Enqueue(new Point(x, y + 1));
                        spanDown = true;
                    }
                    else if (spanDown && y < height - 1 && pixels[index + width * 4] != startColor.B)
                    {
                        spanDown = false;
                    }

                    x++;
                    index += 4;
                }
            }

            WriteableBitmap wb = new WriteableBitmap(width, height, 96d, 96d, PixelFormats.Pbgra32, null);
            wb.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);

            ImageBrush ib = new ImageBrush(wb);
            inkCanvas.Background = ib;
        }

        private void zoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double zoom = Math.Min(e.NewValue, 6);
            inkCanvas.RenderTransform = new ScaleTransform(zoom, zoom);
        }

        private void btnResetZoom_Click(object sender, RoutedEventArgs e)
        {
            zoomSlider.Value = 1;
        }

        private void inkCanvas_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                double delta = e.Delta / 120d;
                double newScale = Math.Max(Math.Min(inkCanvas.RenderTransform.Value.M11 + delta * 0.2, 6), 0.3);

                Point mousePosition = e.GetPosition(inkCanvas);
                inkCanvas.RenderTransformOrigin = new Point(mousePosition.X / inkCanvas.ActualWidth, mousePosition.Y / inkCanvas.ActualHeight);
                inkCanvas.RenderTransform = new ScaleTransform(newScale, newScale);
                zoomSlider.Value = newScale;
                e.Handled = true;
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.Z:
                        btnUndo_Click(sender, e);
                        break;
                    case Key.Y:
                        btnRedo_Click(sender, e);
                        break;
                    case Key.S:
                        saveFile_Click(sender, e);
                        break;
                    case Key.N:
                        newFile_Click(sender, e);
                        break;
                }
            }
        }
    }
}
